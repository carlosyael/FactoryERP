namespace FactoryERP.Core.Domain;

public sealed record ProductionCompletionCostResult(
    int ActualLossQuantity,
    decimal ActualLossPercent,
    decimal TotalBatchCost,
    decimal CapitalizedInventoryCost,
    decimal AbnormalLossCost,
    decimal ActualUnitCost);

/// <summary>
/// Cálculos puros de rendimiento, merma normal/anormal (NIC 2) y costo promedio ponderado.
/// </summary>
public static class ProductionCostCalculator
{
    public static int CalculateExpectedResultingQuantity(int plannedQuantity, decimal lossMarginPercent)
    {
        if (plannedQuantity <= 0)
            throw new ArgumentException("La cantidad a producir debe ser mayor a cero.", nameof(plannedQuantity));

        if (lossMarginPercent < 0m || lossMarginPercent >= 100m)
            throw new ArgumentException("El margen de pérdida debe estar entre 0% y 99.99%.", nameof(lossMarginPercent));

        decimal expectedLoss = Math.Round(plannedQuantity * (lossMarginPercent / 100m), 0, MidpointRounding.AwayFromZero);
        return Math.Max(0, plannedQuantity - (int)expectedLoss);
    }

    public static ProductionCompletionCostResult CalculateCompletionCosts(
        int plannedQuantity,
        int actualYieldQuantity,
        decimal expectedLossMarginPercent,
        decimal abnormalLossTolerancePercent,
        decimal totalMaterialCost,
        decimal actualOverheadCost)
    {
        if (plannedQuantity <= 0)
            throw new ArgumentException("La cantidad planificada debe ser mayor a cero.", nameof(plannedQuantity));

        if (actualYieldQuantity < 0)
            throw new ArgumentException("La cantidad resultante no puede ser negativa.", nameof(actualYieldQuantity));

        if (totalMaterialCost < 0m || actualOverheadCost < 0m)
            throw new ArgumentException("Los costos de fabricación no pueden ser negativos.");

        int actualLossQuantity = Math.Max(0, plannedQuantity - actualYieldQuantity);
        decimal actualLossPercent = Math.Round((actualLossQuantity / (decimal)plannedQuantity) * 100m, 2, MidpointRounding.AwayFromZero);
        decimal totalBatchCost = Math.Round(totalMaterialCost + actualOverheadCost, 2, MidpointRounding.AwayFromZero);

        if (actualYieldQuantity == 0)
        {
            return new ProductionCompletionCostResult(
                actualLossQuantity, actualLossPercent, totalBatchCost, 0m, totalBatchCost, 0m);
        }

        decimal allowedLossPercent = Math.Min(100m, Math.Max(0m, expectedLossMarginPercent + abnormalLossTolerancePercent));
        decimal abnormalLossCost = 0m;

        if (actualLossPercent > allowedLossPercent)
        {
            decimal excessRatio = Math.Clamp((actualLossPercent - allowedLossPercent) / 100m, 0m, 1m);
            abnormalLossCost = Math.Round(totalBatchCost * excessRatio, 2, MidpointRounding.AwayFromZero);
        }

        decimal capitalizedCost = Math.Round(totalBatchCost - abnormalLossCost, 2, MidpointRounding.AwayFromZero);
        decimal unitCost = Math.Round(capitalizedCost / actualYieldQuantity, 4, MidpointRounding.AwayFromZero);

        return new ProductionCompletionCostResult(
            actualLossQuantity,
            actualLossPercent,
            totalBatchCost,
            capitalizedCost,
            abnormalLossCost,
            unitCost);
    }

    public static decimal CalculateWeightedAverageCost(
        decimal currentQuantity,
        decimal currentAverageCost,
        decimal addedQuantity,
        decimal addedUnitCost)
    {
        if (addedQuantity <= 0m) return Math.Max(0m, currentAverageCost);
        if (currentQuantity <= 0m) return Math.Round(Math.Max(0m, addedUnitCost), 4, MidpointRounding.AwayFromZero);

        decimal totalQuantity = currentQuantity + addedQuantity;
        if (totalQuantity <= 0m) return 0m;

        decimal totalValue = (currentQuantity * currentAverageCost) + (addedQuantity * addedUnitCost);
        return Math.Round(totalValue / totalQuantity, 4, MidpointRounding.AwayFromZero);
    }
}
