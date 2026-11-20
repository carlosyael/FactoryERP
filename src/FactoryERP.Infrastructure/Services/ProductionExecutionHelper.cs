using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class ProductionExecutionHelper
{
    public static async Task<decimal> StartOrderAsync(AppDbContext context, ProductionOrder order)
    {
        if (order.Status != ProductionOrderStatus.Draft)
            throw new InvalidOperationException("Solo las órdenes en borrador pueden iniciarse.");

        var matIds = order.Materials.Select(m => m.RawMaterialId).OrderBy(id => id).ToList();
        var materials = await context.RawMaterials.Where(m => matIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        decimal matCost = 0m;
        foreach (var item in order.Materials.OrderBy(m => m.RawMaterialId))
        {
            if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
            if (mat.CurrentStock < item.PlannedQuantity)
                throw new InvalidOperationException(
                    $"Stock insuficiente de '{mat.Name}'. Requerido: {item.PlannedQuantity:N2}, Disponible: {mat.CurrentStock:N2}.");

            mat.CurrentStock = Math.Round(mat.CurrentStock - item.PlannedQuantity, 4, MidpointRounding.AwayFromZero);
            item.ActualQuantityUsed = item.PlannedQuantity;
            item.UnitCostAtConsumption = mat.AverageCost;
            item.TotalCost = Math.Round(item.PlannedQuantity * mat.AverageCost, 2, MidpointRounding.AwayFromZero);
            matCost += item.TotalCost;
        }

        order.ActualMaterialCost = matCost;
        order.ActualOverheadCost = order.EstimatedOverheadCost;
        order.Status = ProductionOrderStatus.InProgress;
        order.StartedAt = DateTime.UtcNow;
        return matCost;
    }

    public static async Task<decimal> CompleteOrderAsync(
        AppDbContext context, ProductionOrder order, CompleteProductionRequest request, decimal tolerance)
    {
        if (order.Status != ProductionOrderStatus.InProgress)
            throw new InvalidOperationException("Solo las órdenes en proceso pueden completarse.");

        decimal initialWipMaterialCost = order.ActualMaterialCost;
        if (request.ActualMaterialQuantities is { Count: > 0 })
        {
            await ReconcileActualMaterialsAsync(context, order, request.ActualMaterialQuantities);
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
        if (!string.IsNullOrWhiteSpace(request.Notes)) order.Notes = request.Notes.Trim();
        order.Status = ProductionOrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;

        if (request.ActualYieldQuantity <= 0) return initialWipMaterialCost;

        var product = await context.Products.FindAsync(order.ProductId)
            ?? throw new InvalidOperationException("Producto terminado no encontrado.");
        int existingStock = await context.FinishedBatches
            .Where(b => b.ProductId == order.ProductId && b.QuantityRemaining > 0)
            .SumAsync(b => b.QuantityRemaining);

        product.Cost = ProductionCostCalculator.CalculateWeightedAverageCost(
            existingStock, product.Cost, request.ActualYieldQuantity, costResult.ActualUnitCost);

        context.FinishedBatches.Add(new FinishedBatch
        {
            ProductId = order.ProductId,
            ProductionOrderId = order.Id,
            BatchCode = order.BatchCode,
            ProductionDate = DateTime.UtcNow,
            ExpirationDate = order.ExpirationDate,
            InitialQuantity = request.ActualYieldQuantity,
            QuantityRemaining = request.ActualYieldQuantity,
            UnitCost = costResult.ActualUnitCost
        });
        return initialWipMaterialCost;
    }

    private static async Task ReconcileActualMaterialsAsync(
        AppDbContext context, ProductionOrder order, IReadOnlyDictionary<int, decimal> actualQuantities)
    {
        var matIds = order.Materials.Select(m => m.RawMaterialId).Distinct().ToList();
        var materials = await context.RawMaterials.Where(m => matIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

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
                if (deltaQty > 0m && mat.CurrentStock < deltaQty)
                    throw new InvalidOperationException(
                        $"Stock insuficiente de '{mat.Name}' para consumo adicional. Faltante: {deltaQty:N2}, Disponible: {mat.CurrentStock:N2}.");

                mat.CurrentStock = Math.Round(mat.CurrentStock - deltaQty, 4, MidpointRounding.AwayFromZero);
                item.ActualQuantityUsed = roundedActual;
                item.TotalCost = Math.Round(roundedActual * item.UnitCostAtConsumption, 2, MidpointRounding.AwayFromZero);
            }
        }

        order.ActualMaterialCost = Math.Round(order.Materials.Sum(m => m.TotalCost), 2, MidpointRounding.AwayFromZero);
    }

    public static async Task<bool> CancelOrderAsync(AppDbContext context, ProductionOrder order, string reason)
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
                mat.CurrentStock = Math.Round(
                    mat.CurrentStock + item.ActualQuantityUsed, 4, MidpointRounding.AwayFromZero);
            }
        }

        order.Status = ProductionOrderStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
            order.Notes = $"{order.Notes} [Cancelada: {reason.Trim()}]".Trim();

        return hadWip;
    }
}
