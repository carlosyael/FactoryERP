using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Infrastructure.Services;

internal static class ProductionPlannerHelper
{
    public static ProductionSimulationDto BuildSimulation(
        ProductFormula formula,
        int plannedQuantity,
        decimal lossMarginPercent)
    {
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
}
