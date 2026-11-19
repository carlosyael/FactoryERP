namespace FactoryERP.Core.Domain;

public sealed record SaleLineCalculationResult(
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetSubTotal,
    decimal TaxAmount,
    decimal LineTotal);

/// <summary>
/// Cálculos comerciales puros para ventas, descuentos máximos e ITBIS (18%).
/// </summary>
public static class PricingCalculator
{
    public static SaleLineCalculationResult CalculateSaleLine(
        int quantity,
        decimal unitPrice,
        decimal discountPercent,
        decimal maxDiscountPercent,
        bool isTaxable,
        decimal taxRate)
    {
        if (quantity <= 0)
            throw new ArgumentException("La cantidad vendida debe ser mayor a cero.", nameof(quantity));

        if (unitPrice < 0m)
            throw new ArgumentException("El precio unitario no puede ser negativo.", nameof(unitPrice));

        if (discountPercent < 0m || discountPercent > maxDiscountPercent)
            throw new ArgumentException($"El descuento debe estar entre 0% y {maxDiscountPercent:0.##}%.", nameof(discountPercent));

        decimal gross = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        decimal discount = Math.Round(gross * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        decimal netSubTotal = gross - discount;
        decimal taxAmount = isTaxable
            ? Math.Round(netSubTotal * taxRate, 2, MidpointRounding.AwayFromZero)
            : 0m;
        decimal lineTotal = netSubTotal + taxAmount;

        return new SaleLineCalculationResult(gross, discount, netSubTotal, taxAmount, lineTotal);
    }
}
