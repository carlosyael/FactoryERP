using FactoryERP.Core.Domain;
using FluentAssertions;

namespace FactoryERP.Tests.Domain;

public class ProductionCostCalculatorTests
{
    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 5, 95)]
    [InlineData(15, 10, 13)] // 1.5 redondea AwayFromZero a 2 => 15 - 2 = 13
    public void CalculateExpectedResultingQuantity_ValidInputs_ReturnsExpected(
        int planned, decimal lossPercent, int expectedResult)
    {
        int result = ProductionCostCalculator.CalculateExpectedResultingQuantity(planned, lossPercent);
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-5, 5)]
    [InlineData(10, -1)]
    [InlineData(10, 100)]
    public void CalculateExpectedResultingQuantity_InvalidBoundaries_ThrowsArgumentException(
        int planned, decimal lossPercent)
    {
        Action act = () => ProductionCostCalculator.CalculateExpectedResultingQuantity(planned, lossPercent);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateCompletionCosts_WithinNormalTolerance_CapitalizesFullCostWithoutAbnormalLoss()
    {
        var res = ProductionCostCalculator.CalculateCompletionCosts(
            plannedQuantity: 100,
            actualYieldQuantity: 94, // 6% merma <= 5% + 2% tolerancia
            expectedLossMarginPercent: 5m,
            abnormalLossTolerancePercent: 2m,
            totalMaterialCost: 9000m,
            actualOverheadCost: 400m);

        res.ActualLossQuantity.Should().Be(6);
        res.ActualLossPercent.Should().Be(6.00m);
        res.AbnormalLossCost.Should().Be(0m);
        res.CapitalizedInventoryCost.Should().Be(9400m);
        res.ActualUnitCost.Should().Be(100m);
    }

    [Fact]
    public void CalculateCompletionCosts_ExceedsTolerance_SplitsAbnormalLossAndPreservesInvariant()
    {
        var res = ProductionCostCalculator.CalculateCompletionCosts(
            plannedQuantity: 100,
            actualYieldQuantity: 80, // 20% merma > (5% + 2% = 7%) => exceso 13%
            expectedLossMarginPercent: 5m,
            abnormalLossTolerancePercent: 2m,
            totalMaterialCost: 9000m,
            actualOverheadCost: 1000m);

        res.ActualLossQuantity.Should().Be(20);
        res.ActualLossPercent.Should().Be(20.00m);
        res.AbnormalLossCost.Should().Be(1300m);
        res.CapitalizedInventoryCost.Should().Be(8700m);
        (res.CapitalizedInventoryCost + res.AbnormalLossCost).Should().Be(res.TotalBatchCost);
        res.ActualUnitCost.Should().Be(108.75m);
    }

    [Fact]
    public void CalculateCompletionCosts_ZeroActualYield_RecordsTotalCostAsAbnormalLoss()
    {
        var res = ProductionCostCalculator.CalculateCompletionCosts(
            plannedQuantity: 50,
            actualYieldQuantity: 0,
            expectedLossMarginPercent: 5m,
            abnormalLossTolerancePercent: 2m,
            totalMaterialCost: 2500m,
            actualOverheadCost: 500m);

        res.ActualLossPercent.Should().Be(100m);
        res.CapitalizedInventoryCost.Should().Be(0m);
        res.AbnormalLossCost.Should().Be(3000m);
        res.ActualUnitCost.Should().Be(0m);
    }

    [Fact]
    public void CalculateWeightedAverageCost_CombinesExistingAndAddedQuantities()
    {
        decimal cpp = ProductionCostCalculator.CalculateWeightedAverageCost(10m, 100m, 10m, 120m);
        cpp.Should().Be(110m);
    }
}
