using FactoryERP.Core.Domain;
using FluentAssertions;

namespace FactoryERP.Tests.Domain;

public class PricingCalculatorTests
{
    [Fact]
    public void CalculateSaleLine_TaxableWithItbis18AndDiscount_CalculatesExactTotals()
    {
        var result = PricingCalculator.CalculateSaleLine(
            quantity: 2,
            unitPrice: 500m,
            discountPercent: 10m,
            maxDiscountPercent: 30m,
            isTaxable: true,
            taxRate: 0.18m);

        result.GrossAmount.Should().Be(1000m);
        result.DiscountAmount.Should().Be(100m);
        result.NetSubTotal.Should().Be(900m);
        result.TaxAmount.Should().Be(162m);
        result.LineTotal.Should().Be(1062m);
    }

    [Fact]
    public void CalculateSaleLine_ExemptItem_AppliesZeroTax()
    {
        var result = PricingCalculator.CalculateSaleLine(
            quantity: 3,
            unitPrice: 200m,
            discountPercent: 0m,
            maxDiscountPercent: 30m,
            isTaxable: false,
            taxRate: 0.18m);

        result.TaxAmount.Should().Be(0m);
        result.LineTotal.Should().Be(600m);
    }

    [Fact]
    public void CalculateSaleLine_DiscountAboveMax_ThrowsArgumentException()
    {
        Action act = () => PricingCalculator.CalculateSaleLine(1, 100m, 35m, 30m, true, 0.18m);
        act.Should().Throw<ArgumentException>();
    }
}
