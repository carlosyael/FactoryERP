using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class ProductionExecutionHelper
{
    public static async Task<decimal> StartOrderAsync(AppDbContext context, ProductionOrder order, string? performedBy = null)
    {
        if (order.Status != ProductionOrderStatus.Draft)
            throw new InvalidOperationException("Solo las órdenes en borrador pueden iniciarse.");
        if (order.ProductFormula is { IsActive: false })
            throw new InvalidOperationException($"La fórmula '{order.ProductFormula.Name}' está inactiva y no puede iniciarse.");
        if (order.OutputRawMaterial is { IsActive: false })
            throw new InvalidOperationException($"El semielaborado '{order.OutputRawMaterial.Name}' está inactivo y no puede iniciarse.");

        var matIds = order.Materials.Select(m => m.RawMaterialId).OrderBy(id => id).ToList();
        var materials = await context.RawMaterials.Where(m => matIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        decimal matCost = 0m;
        DateTime utcToday = DateTime.UtcNow.Date;
        foreach (var item in order.Materials.OrderBy(m => m.RawMaterialId))
        {
            if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
            if (!mat.IsActive)
                throw new InvalidOperationException($"La materia prima '{mat.Name}' ({mat.Sku}) está inactiva y no puede iniciarse.");

            string? batchCode = await RawMaterialBatchAllocator.AllocateFefoAsync(
                context, order, mat, item.PlannedQuantity, utcToday);
            mat.CurrentStock = Math.Round(mat.CurrentStock - item.PlannedQuantity, 4, MidpointRounding.AwayFromZero);
            item.ActualQuantityUsed = item.PlannedQuantity;
            item.UnitCostAtConsumption = mat.AverageCost;
            item.TotalCost = Math.Round(item.PlannedQuantity * mat.AverageCost, 2, MidpointRounding.AwayFromZero);
            matCost += item.TotalCost;
            InventoryKardexRecorder.RecordRawMaterial(
                context, mat, InventoryMovementType.ProductionConsumption, -item.PlannedQuantity, item.UnitCostAtConsumption,
                order.OrderNumber, $"Inicio lote {order.BatchCode}", performedBy, batchCode: batchCode);
        }

        order.ActualMaterialCost = matCost;
        order.ActualOverheadCost = order.EstimatedOverheadCost;
        order.Status = ProductionOrderStatus.InProgress;
        order.StartedAt = DateTime.UtcNow;
        return matCost;
    }

    public static async Task<decimal> CompleteOrderAsync(
        AppDbContext context, ProductionOrder order, CompleteProductionRequest request,
        decimal tolerance, bool requireQualityApproval = false, string? performedBy = null)
    {
        if (order.Status != ProductionOrderStatus.InProgress)
            throw new InvalidOperationException("Solo las órdenes en proceso pueden completarse.");

        decimal initialWipMaterialCost = order.ActualMaterialCost;
        if (request.ActualMaterialQuantities is { Count: > 0 })
        {
            await ReconcileActualMaterialsAsync(context, order, request.ActualMaterialQuantities, performedBy);
        }

        decimal overheadCost = Math.Round(request.ActualOverheadCost ?? order.ActualOverheadCost, 2, MidpointRounding.AwayFromZero);
        if (overheadCost < 0m) throw new ArgumentException("El costo indirecto real no puede ser negativo.");

        var costResult = ProductionCostCalculator.CalculateCompletionCosts(
            order.PlannedQuantity, request.ActualYieldQuantity, order.ExpectedLossMarginPercent,
            tolerance, order.ActualMaterialCost, overheadCost);

        order.ActualOverheadCost = overheadCost;
        order.ActualYieldQuantity = request.ActualYieldQuantity;
        order.ActualLossQuantity = costResult.ActualLossQuantity;
        order.ActualLossPercent = costResult.ActualLossPercent;
        order.AbnormalLossCost = costResult.AbnormalLossCost;
        order.CapitalizedInventoryCost = costResult.CapitalizedInventoryCost;
        order.ActualUnitCost = costResult.ActualUnitCost;
        order.ExpirationDate = request.ExpirationDate ?? order.ExpirationDate;
        if (!string.IsNullOrWhiteSpace(request.Notes)) order.Notes = ProductionPlannerHelper.TruncateNotes(request.Notes);
        order.Status = ProductionOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;

        if (request.ActualYieldQuantity <= 0) return initialWipMaterialCost;

        await ProductionOutputRecorder.RecordCompletionOutputAsync(
            context, order, request.ActualYieldQuantity, costResult.ActualUnitCost, requireQualityApproval, performedBy);
        return initialWipMaterialCost;
    }

    private static async Task ReconcileActualMaterialsAsync(
        AppDbContext context, ProductionOrder order, IReadOnlyDictionary<int, decimal> actualQuantities, string? performedBy = null)
    {
        var matIds = order.Materials.Select(m => m.RawMaterialId).Distinct().ToList();
        var materials = await context.RawMaterials.Where(m => matIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        DateTime utcToday = DateTime.UtcNow.Date;

        foreach (var item in order.Materials.OrderBy(m => m.RawMaterialId))
        {
            if (!actualQuantities.TryGetValue(item.RawMaterialId, out decimal rawQty)) continue;
            if (rawQty < 0m) throw new ArgumentException("La cantidad real consumida no puede ser negativa.");

            decimal roundedActual = Math.Round(rawQty, 4, MidpointRounding.AwayFromZero);
            decimal deltaQty = Math.Round(roundedActual - item.ActualQuantityUsed, 4, MidpointRounding.AwayFromZero);
            if (deltaQty != 0m)
            {
                if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                    throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
                if (deltaQty > 0m && !mat.IsActive)
                    throw new InvalidOperationException($"La materia prima '{mat.Name}' ({mat.Sku}) está inactiva y no admite consumo adicional.");

                string? batchCode = deltaQty > 0m
                    ? await RawMaterialBatchAllocator.AllocateFefoAsync(context, order, mat, deltaQty, utcToday)
                    : await RawMaterialBatchAllocator.RestoreAllocatedAsync(context, order, mat, Math.Abs(deltaQty));

                mat.CurrentStock = Math.Round(mat.CurrentStock - deltaQty, 4, MidpointRounding.AwayFromZero);
                item.ActualQuantityUsed = roundedActual;
                item.TotalCost = Math.Round(roundedActual * item.UnitCostAtConsumption, 2, MidpointRounding.AwayFromZero);
                InventoryKardexRecorder.RecordRawMaterial(
                    context, mat, InventoryMovementType.ProductionMaterialReconciliation, -deltaQty,
                    item.UnitCostAtConsumption, order.OrderNumber,
                    deltaQty > 0m ? $"Consumo adicional lote {order.BatchCode}" : $"Devolución sobrante lote {order.BatchCode}",
                    performedBy, batchCode: batchCode);
            }
        }

        order.ActualMaterialCost = Math.Round(order.Materials.Sum(m => m.TotalCost), 2, MidpointRounding.AwayFromZero);
    }

    public static async Task<bool> CancelOrderAsync(AppDbContext context, ProductionOrder order, string reason, string? performedBy = null)
    {
        if (order.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Cancelled)
            throw new InvalidOperationException("La orden ya fue completada o cancelada.");

        bool hadWip = order.Status == ProductionOrderStatus.InProgress;
        if (hadWip)
        {
            var matIds = order.Materials.Select(m => m.RawMaterialId).OrderBy(id => id).ToList();
            var materials = await context.RawMaterials.Where(m => matIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
            foreach (var item in order.Materials.OrderBy(m => m.RawMaterialId))
            {
                if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                    throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
                string? batchCode = await RawMaterialBatchAllocator.RestoreAllocatedAsync(context, order, mat, item.ActualQuantityUsed);
                mat.CurrentStock = Math.Round(
                    mat.CurrentStock + item.ActualQuantityUsed, 4, MidpointRounding.AwayFromZero);
                InventoryKardexRecorder.RecordRawMaterial(
                    context, mat, InventoryMovementType.ProductionCancellationReturn, item.ActualQuantityUsed,
                    item.UnitCostAtConsumption, order.OrderNumber,
                    string.IsNullOrWhiteSpace(reason) ? $"Cancelación orden {order.OrderNumber}" : reason.Trim(),
                    performedBy, batchCode: batchCode);
            }
        }

        order.Status = ProductionOrderStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
            order.Notes = ProductionPlannerHelper.TruncateNotes($"{order.Notes} [Cancelada: {reason.Trim()}]");

        return hadWip;
    }
}
