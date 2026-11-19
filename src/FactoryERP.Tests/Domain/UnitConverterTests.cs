using FactoryERP.Core.Domain;
using FactoryERP.Core.Enums;
using FluentAssertions;

namespace FactoryERP.Tests.Domain;

public class UnitConverterTests
{
    [Theory]
    [InlineData(1000, UnitOfMeasure.Gram, UnitOfMeasure.Kilogram, 1)]
    [InlineData(2.5, UnitOfMeasure.Kilogram, UnitOfMeasure.Gram, 2500)]
    [InlineData(500, UnitOfMeasure.Milliliter, UnitOfMeasure.Liter, 0.5)]
    [InlineData(1.25, UnitOfMeasure.Liter, UnitOfMeasure.Milliliter, 1250)]
    [InlineData(10, UnitOfMeasure.Unit, UnitOfMeasure.Unit, 10)]
    public void Convert_CompatibleUnits_ReturnsExpectedQuantity(
        decimal qty, UnitOfMeasure from, UnitOfMeasure to, decimal expected)
    {
        decimal result = UnitConverter.Convert(qty, from, to);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(UnitOfMeasure.Gram, UnitOfMeasure.Milliliter)]
    [InlineData(UnitOfMeasure.Kilogram, UnitOfMeasure.Unit)]
    [InlineData(UnitOfMeasure.Liter, UnitOfMeasure.Unit)]
    public void Convert_IncompatibleUnits_ThrowsInvalidOperationException(UnitOfMeasure from, UnitOfMeasure to)
    {
        Action act = () => UnitConverter.Convert(10m, from, to);
        act.Should().Throw<InvalidOperationException>();
    }
}
