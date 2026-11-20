using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;

namespace FactoryERP.Infrastructure.Services;

internal sealed record BatchDeduction(FinishedBatch Batch, int QuantityTaken, int BalanceAfter);

internal sealed record SaleAllocationSummary(
    List<SaleItem> SaleItems,
    List<BatchDeduction> Deductions,
    decimal SubTotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal TotalCogs);

internal static class FefoBatchAllocator
{
    public static SaleAllocationSummary Allocate(
        IReadOnlyList<CreateSaleItemRequest> requestItems,
        IReadOnlyDictionary<int, Product> products,
        List<FinishedBatch> availableBatches,
        CustomerType customerType,
        decimal maxDiscount,
        decimal defaultTaxRate)
    {
        var saleItems = new List<SaleItem>();
        var deductions = new List<BatchDeduction>();
        decimal subTotal = 0m, totalDiscount = 0m, totalTax = 0m, totalCogs = 0m;

        foreach (var reqItem in requestItems.OrderBy(i => i.ProductId))
        {
            if (!products.TryGetValue(reqItem.ProductId, out var product) || !product.IsActive)
                throw new InvalidOperationException($"Producto ID {reqItem.ProductId} no disponible.");

            decimal unitPrice = reqItem.UnitPriceOverride ?? product.GetPriceForCustomerType(customerType);
            decimal taxRate = product.IsTaxable ? (product.TaxRate > 0 ? product.TaxRate : defaultTaxRate) : 0m;
            var calc = PricingCalculator.CalculateSaleLine(
                reqItem.Quantity, unitPrice, reqItem.DiscountPercent, maxDiscount, product.IsTaxable, taxRate);

            DateTime utcToday = DateTime.UtcNow.Date;
            var productBatches = availableBatches.Where(b => b.ProductId == product.Id && b.IsSellable(utcToday)).ToList();
            int available = productBatches.Sum(b => b.QuantityRemaining);
            if (available < reqItem.Quantity)
                throw new InvalidOperationException(
                    $"Stock vendible insuficiente de '{product.Name}' (lotes aprobados y vigentes). Disponible: {available}, Solicitado: {reqItem.Quantity}.");

            int remainingToPick = reqItem.Quantity;
            decimal allocatedDisc = 0m, allocatedTax = 0m;

            foreach (var batch in productBatches)
            {
                if (remainingToPick == 0) break;
                int take = Math.Min(remainingToPick, batch.QuantityRemaining);
                batch.QuantityRemaining -= take;
                remainingToPick -= take;
                deductions.Add(new BatchDeduction(batch, take, batch.QuantityRemaining));

                bool isLastBatchForLine = remainingToPick == 0;
                decimal ratio = take / (decimal)reqItem.Quantity;
                decimal partDisc = isLastBatchForLine
                    ? calc.DiscountAmount - allocatedDisc
                    : Math.Round(calc.DiscountAmount * ratio, 2, MidpointRounding.AwayFromZero);
                decimal partTax = isLastBatchForLine
                    ? calc.TaxAmount - allocatedTax
                    : Math.Round(calc.TaxAmount * ratio, 2, MidpointRounding.AwayFromZero);

                allocatedDisc += partDisc;
                allocatedTax += partTax;
                decimal partGross = Math.Round(take * unitPrice, 2, MidpointRounding.AwayFromZero);

                saleItems.Add(new SaleItem
                {
                    ProductId = product.Id,
                    FinishedBatchId = batch.Id,
                    Quantity = take,
                    UnitPrice = unitPrice,
                    DiscountAmount = partDisc,
                    TaxRate = taxRate,
                    TaxAmount = partTax,
                    LineTotal = partGross - partDisc + partTax,
                    UnitCost = batch.UnitCost
                });

                totalCogs += Math.Round(take * batch.UnitCost, 2, MidpointRounding.AwayFromZero);
            }

            subTotal += calc.GrossAmount;
            totalDiscount += calc.DiscountAmount;
            totalTax += calc.TaxAmount;
        }

        return new SaleAllocationSummary(saleItems, deductions, subTotal, totalDiscount, totalTax, totalCogs);
    }
}
