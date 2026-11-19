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

    [Theory]
    [InlineData(UnitOfMeasure.Gram, UnitOfMeasure.Kilogram, true)]
    [InlineData(UnitOfMeasure.Milliliter, UnitOfMeasure.Liter, true)]
    [InlineData(UnitOfMeasure.Unit, UnitOfMeasure.Unit, true)]
    [InlineData(UnitOfMeasure.Gram, UnitOfMeasure.Liter, false)]
    [InlineData(UnitOfMeasure.Unit, UnitOfMeasure.Gram, false)]
    public void AreCompatible_EvaluatesDimensionMatch(UnitOfMeasure from, UnitOfMeasure to, bool expected)
    {
        UnitConverter.AreCompatible(from, to).Should().Be(expected);
    }

    [Theory]
    [InlineData(UnitOfMeasure.Gram, "g")]
    [InlineData(UnitOfMeasure.Kilogram, "kg")]
    [InlineData(UnitOfMeasure.Milliliter, "ml")]
    [InlineData(UnitOfMeasure.Liter, "L")]
    [InlineData(UnitOfMeasure.Unit, "ud")]
    public void GetShortLabel_ReturnsCanonicalAbbreviation(UnitOfMeasure unit, string expectedLabel)
    {
        UnitConverter.GetShortLabel(unit).Should().Be(expectedLabel);
    }
}
