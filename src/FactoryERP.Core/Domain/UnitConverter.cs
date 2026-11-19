using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Domain;

/// <summary>
/// Conversor determinístico entre unidades compatibles (masa, volumen y conteo).
/// </summary>
public static class UnitConverter
{
    public static bool AreCompatible(UnitOfMeasure from, UnitOfMeasure to)
        => GetDimension(from) == GetDimension(to);

    public static decimal Convert(decimal quantity, UnitOfMeasure from, UnitOfMeasure to)
    {
        if (from == to) return quantity;

        if (!AreCompatible(from, to))
        {
            throw new InvalidOperationException(
                $"No es posible convertir unidades incompatibles de {from} a {to}.");
        }

        decimal inBase = quantity * GetFactorToBase(from);
        return Math.Round(inBase / GetFactorToBase(to), 4, MidpointRounding.AwayFromZero);
    }

    public static string GetShortLabel(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Gram => "g",
        UnitOfMeasure.Kilogram => "kg",
        UnitOfMeasure.Milliliter => "ml",
        UnitOfMeasure.Liter => "L",
        UnitOfMeasure.Unit => "ud",
        _ => unit.ToString()
    };

    private static string GetDimension(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Gram or UnitOfMeasure.Kilogram => "Mass",
        UnitOfMeasure.Milliliter or UnitOfMeasure.Liter => "Volume",
        UnitOfMeasure.Unit => "Count",
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    private static decimal GetFactorToBase(UnitOfMeasure unit) => unit switch
    {
        UnitOfMeasure.Gram => 1m,
        UnitOfMeasure.Kilogram => 1000m,
        UnitOfMeasure.Milliliter => 1m,
        UnitOfMeasure.Liter => 1000m,
        UnitOfMeasure.Unit => 1m,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };
}
