using FactoryERP.Core.Domain;
using FluentAssertions;

namespace FactoryERP.Tests.Domain;

public class PricingCalculatorTests
{
    [Fact]
    public void CalculateSaleLine_TaxableWithItbis18AndDiscount_CalculatesExactTotals()
    {
        var result = PricingCalculator.CalculateSaleLine(2, 500m, 10m, 30m, true, 0.18m);

        result.GrossAmount.Should().Be(1000m);
        result.DiscountAmount.Should().Be(100m);
        result.NetSubTotal.Should().Be(900m);
        result.TaxAmount.Should().Be(162m);
        result.LineTotal.Should().Be(1062m);
    }

    [Fact]
    public void CalculateSaleLine_ExemptItem_AppliesZeroTax()
    {
        var result = PricingCalculator.CalculateSaleLine(3, 200m, 0m, 30m, false, 0.18m);
        result.TaxAmount.Should().Be(0m);
        result.LineTotal.Should().Be(600m);
    }

    [Fact]
    public void CalculateSaleLine_DiscountAtExactMaximumBoundary_Succeeds()
    {
        var result = PricingCalculator.CalculateSaleLine(1, 100m, 30m, 30m, true, 0.18m);
        result.DiscountAmount.Should().Be(30m);
        result.NetSubTotal.Should().Be(70m);
        result.TaxAmount.Should().Be(12.60m);
        result.LineTotal.Should().Be(82.60m);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(-1, 100, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 100, -0.01)]
    [InlineData(1, 100, 30.01)]
    public void CalculateSaleLine_InvalidBoundaries_ThrowsArgumentException(
        int qty, decimal unitPrice, decimal discount)
    {
        Action act = () => PricingCalculator.CalculateSaleLine(qty, unitPrice, discount, 30m, true, 0.18m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateSaleLine_RoundingAwayFromZero_RoundsHalfCentUp()
    {
        // 1 * 10.03 * 0.18 = 1.8054 -> 1.81; 1 * 10.25 * 0.18 = 1.845 -> 1.85
        var result = PricingCalculator.CalculateSaleLine(1, 10.25m, 0m, 30m, true, 0.18m);
        result.TaxAmount.Should().Be(1.85m);
        result.LineTotal.Should().Be(12.10m);
    }
}
