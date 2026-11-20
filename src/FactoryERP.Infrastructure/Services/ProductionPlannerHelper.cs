using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class ProductionPlannerHelper
{
    public static ProductionSimulationDto BuildSimulation(
        ProductFormula formula,
        int plannedQuantity,
        decimal lossMarginPercent)
    {
        if (!formula.IsActive)
            throw new InvalidOperationException($"La fórmula '{formula.Name}' está inactiva y no puede usarse.");
        if (formula.BaseYieldQuantity <= 0)
            throw new InvalidOperationException("El rendimiento base de la fórmula debe ser mayor a cero.");

        decimal scaleFactor = plannedQuantity / (decimal)formula.BaseYieldQuantity;
        int expectedResulting = ProductionCostCalculator.CalculateExpectedResultingQuantity(plannedQuantity, lossMarginPercent);
        int expectedLoss = Math.Max(0, plannedQuantity - expectedResulting);

        var requirements = new List<ProductionMaterialRequirementDto>();
        foreach (var group in formula.Items.GroupBy(i => i.RawMaterialId).OrderBy(g => g.Key))
        {
            var mat = group.First().RawMaterial
                ?? throw new InvalidOperationException($"Materia prima ID {group.Key} no encontrada.");
            if (!mat.IsActive)
                throw new InvalidOperationException($"La materia prima '{mat.Name}' está inactiva.");

            decimal requiredInMatUnit = group.Sum(item =>
                UnitConverter.Convert(item.QuantityRequired * scaleFactor, item.UnitOfMeasure, mat.UnitOfMeasure));
            requiredInMatUnit = Math.Round(requiredInMatUnit, 4, MidpointRounding.AwayFromZero);

            decimal estCost = Math.Round(requiredInMatUnit * mat.AverageCost, 2, MidpointRounding.AwayFromZero);
            bool sufficient = mat.CurrentStock >= requiredInMatUnit;

            requirements.Add(new ProductionMaterialRequirementDto(
                mat.Id,
                mat.Sku,
                mat.Name,
                mat.UnitOfMeasure,
                requiredInMatUnit,
                mat.CurrentStock,
                mat.AverageCost,
                estCost,
                sufficient));
        }

        decimal estMaterialCost = requirements.Sum(r => r.EstimatedTotalCost);
        decimal estOverhead = Math.Round(formula.OverheadCostPerBatch * scaleFactor, 2, MidpointRounding.AwayFromZero);
        decimal estTotal = estMaterialCost + estOverhead;
        decimal estUnit = expectedResulting > 0
            ? Math.Round(estTotal / expectedResulting, 4, MidpointRounding.AwayFromZero)
            : 0m;

        return new ProductionSimulationDto(
            formula.ProductId,
            formula.Product?.Name ?? string.Empty,
            formula.Id,
            formula.Name,
            formula.Version,
            plannedQuantity,
            lossMarginPercent,
            expectedLoss,
            expectedResulting,
            estMaterialCost,
            estOverhead,
            estTotal,
            estUnit,
            requirements.All(r => r.HasSufficientStock),
            requirements);
    }

    public static ProductionOrder BuildDraftOrder(
        ProductionSimulationDto sim, CreateProductionOrderRequest request, string batchCode, string token)
    {
        var order = new ProductionOrder { OrderNumber = $"OP-{DateTime.UtcNow:yyyyMMdd}-{token}" };
        ApplyDraftUpdate(order, sim, new UpdateProductionOrderRequest(
            0, request.ProductFormulaId, request.PlannedQuantity, request.OverrideLossMarginPercent,
            request.PlannedDate, request.ExpirationDate, batchCode, request.Notes), batchCode);
        return order;
    }

    public static void ApplyDraftUpdate(
        ProductionOrder order, ProductionSimulationDto sim, UpdateProductionOrderRequest request, string batchCode)
    {
        order.ProductId = sim.ProductId;
        order.ProductFormulaId = sim.FormulaId;
        order.BatchCode = batchCode;
        order.PlannedDate = request.PlannedDate ?? DateTime.UtcNow;
        order.ExpirationDate = request.ExpirationDate ?? DateTime.UtcNow.AddMonths(18);
        order.PlannedQuantity = sim.PlannedQuantity;
        order.ExpectedLossMarginPercent = sim.ExpectedLossMarginPercent;
        order.ExpectedResultingQuantity = sim.ExpectedResultingQuantity;
        order.EstimatedMaterialCost = sim.EstimatedMaterialCost;
        order.EstimatedOverheadCost = sim.EstimatedOverheadCost;
        order.Notes = TruncateNotes(request.Notes);
        order.Materials = sim.Materials.Select(m => new ProductionOrderMaterial
        {
            RawMaterialId = m.RawMaterialId,
            PlannedQuantity = m.RequiredQuantity,
            ActualQuantityUsed = m.RequiredQuantity,
            UnitCostAtConsumption = m.UnitCost,
            TotalCost = m.EstimatedTotalCost
        }).ToList();
    }

    internal static string TruncateNotes(string? notes, int maxLength = 400)
    {
        string trimmed = (notes ?? string.Empty).Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static async Task EnsureBatchCodeUniqueAsync(AppDbContext context, string batchCode, int? excludeOrderId = null)
    {
        bool exists = await context.FinishedBatches.AnyAsync(b => b.BatchCode == batchCode)
            || await context.ProductionOrders.AnyAsync(
                o => (!excludeOrderId.HasValue || o.Id != excludeOrderId.Value)
                     && o.BatchCode == batchCode
                     && o.Status != ProductionOrderStatus.Cancelled);
        if (exists)
            throw new InvalidOperationException($"Ya existe una orden o lote registrado con el código '{batchCode}'.");
    }
}
