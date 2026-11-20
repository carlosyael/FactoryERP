using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class ManufacturingAccountingService : IManufacturingAccountingService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ManufacturingAccountingService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<JournalEntry> PostPurchaseReceiptAsync(PurchaseOrder purchaseOrder, string? userId = null)
    {
        var lines = new List<(string Code, decimal Debit, decimal Credit, string Desc)>();
        if (purchaseOrder.SubTotal > 0m)
            lines.Add((AccountCodes.RawMaterialInventory, purchaseOrder.SubTotal, 0m, $"Compra insumos {purchaseOrder.OrderNumber}"));
        if (purchaseOrder.TaxAmount > 0m)
            lines.Add((AccountCodes.TaxCreditAdvance, purchaseOrder.TaxAmount, 0m, $"ITBIS compra {purchaseOrder.OrderNumber}"));

        string settlement = purchaseOrder.PaymentMethod switch
        {
            PaymentMethod.Cash => AccountCodes.Cash,
            PaymentMethod.Credit => AccountCodes.AccountsPayable,
            _ => AccountCodes.Bank
        };
        lines.Add((settlement, 0m, purchaseOrder.Total, $"Pago/CxP {purchaseOrder.OrderNumber}"));
        return PostAsync(purchaseOrder.PurchaseDate, $"Recepción de compra {purchaseOrder.OrderNumber}", purchaseOrder.OrderNumber, userId, lines);
    }

    public Task<JournalEntry> PostSupplierPaymentAsync(SupplierPayment payment, string? userId = null)
    {
        string cashOrBank = payment.PaymentMethod == PaymentMethod.Cash ? AccountCodes.Cash : AccountCodes.Bank;
        return PostAsync(payment.PaymentDate, $"Pago a suplidor #{payment.SupplierId}", payment.Reference, userId,
        [
            (AccountCodes.AccountsPayable, payment.Amount, 0m, $"Abono a suplidor {payment.Reference}"),
            (cashOrBank, 0m, payment.Amount, $"Desembolso pago suplidor {payment.Reference}")
        ]);
    }

    public Task<JournalEntry> PostProductionStartAsync(ProductionOrder order, decimal materialCost, string? userId = null)
    {
        decimal roundedMat = Math.Round(materialCost, 2, MidpointRounding.AwayFromZero);
        return PostAsync(order.StartedAt ?? DateTime.UtcNow, $"Inicio fabricación {order.OrderNumber}", order.OrderNumber, userId,
        [
            (AccountCodes.WorkInProgressInventory, roundedMat, 0m, $"Inicio orden {order.OrderNumber} (Lote {order.BatchCode})"),
            (AccountCodes.RawMaterialInventory, 0m, roundedMat, $"Consumo materiales {order.OrderNumber}")
        ]);
    }

    public Task<JournalEntry> PostProductionCompletionAsync(
        ProductionOrder order, string? userId = null, decimal? wipMaterialCleared = null)
    {
        decimal wipClear = Math.Round(wipMaterialCleared ?? order.ActualMaterialCost, 2, MidpointRounding.AwayFromZero);
        decimal matVariance = Math.Round(order.ActualMaterialCost - wipClear, 2, MidpointRounding.AwayFromZero);
        decimal overheadApplied = Math.Round(order.ActualOverheadCost, 2, MidpointRounding.AwayFromZero);
        var lines = new List<(string, decimal, decimal, string)>();

        if (order.CapitalizedInventoryCost > 0m)
        {
            string targetAccount = order.IsSemiFinishedOutput ? AccountCodes.RawMaterialInventory : AccountCodes.FinishedGoodsInventory;
            string entryDesc = order.IsSemiFinishedOutput ? $"Ingreso granel/semielaborado lote {order.BatchCode}" : $"Ingreso producto terminado lote {order.BatchCode}";
            lines.Add((targetAccount, order.CapitalizedInventoryCost, 0m, entryDesc));
        }
        if (order.AbnormalLossCost > 0m)
            lines.Add((AccountCodes.AbnormalProductionLoss, order.AbnormalLossCost, 0m, $"Merma anormal orden {order.OrderNumber}"));
        if (matVariance < 0m)
            lines.Add((AccountCodes.RawMaterialInventory, -matVariance, 0m, $"Devolución sobrante materiales orden {order.OrderNumber}"));
        if (wipClear > 0m)
            lines.Add((AccountCodes.WorkInProgressInventory, 0m, wipClear, $"Liquidación WIP materiales orden {order.OrderNumber}"));
        if (matVariance > 0m)
            lines.Add((AccountCodes.RawMaterialInventory, 0m, matVariance, $"Consumo adicional materiales orden {order.OrderNumber}"));
        if (overheadApplied > 0m)
            lines.Add((AccountCodes.ManufacturingOverheadApplied, 0m, overheadApplied, $"CIF real aplicado orden {order.OrderNumber}"));

        return PostAsync(order.CompletedAt ?? DateTime.UtcNow, $"Cierre fabricación {order.OrderNumber}", order.OrderNumber, userId, lines);
    }

    public Task<JournalEntry> PostProductionCancellationAsync(ProductionOrder order, string? userId = null)
    {
        decimal mat = Math.Round(order.ActualMaterialCost, 2, MidpointRounding.AwayFromZero);
        return PostAsync(DateTime.UtcNow, $"Cancelación fabricación {order.OrderNumber}", order.OrderNumber, userId,
        [
            (AccountCodes.RawMaterialInventory, mat, 0m, $"Devolución materiales por cancelación {order.OrderNumber}"),
            (AccountCodes.WorkInProgressInventory, 0m, mat, $"Reverso WIP cancelación {order.OrderNumber}")
        ]);
    }

    public Task<JournalEntry> PostSaleAsync(Sale sale, string? userId = null)
    {
        decimal netRevenue = Math.Round(sale.SubTotal - sale.DiscountAmount, 2, MidpointRounding.AwayFromZero);
        string debitCode = sale.PaymentMethod switch
        {
            PaymentMethod.Cash => AccountCodes.Cash,
            PaymentMethod.Credit => AccountCodes.AccountsReceivable,
            _ => AccountCodes.Bank
        };

        var lines = new List<(string, decimal, decimal, string)>
        {
            (debitCode, sale.Total, 0m, $"Cobro/CxC venta {sale.SaleNumber}"),
            (AccountCodes.ProductSalesRevenue, 0m, netRevenue, $"Ingreso venta {sale.SaleNumber}")
        };
        if (sale.TaxAmount > 0m)
            lines.Add((AccountCodes.TaxPayable, 0m, sale.TaxAmount, $"ITBIS venta {sale.SaleNumber}"));
        if (sale.TotalCogs > 0m)
        {
            lines.Add((AccountCodes.CostOfGoodsSold, sale.TotalCogs, 0m, $"Costo de venta {sale.SaleNumber}"));
            lines.Add((AccountCodes.FinishedGoodsInventory, 0m, sale.TotalCogs, $"Salida inventario {sale.SaleNumber}"));
        }

        return PostAsync(sale.SaleDate, $"Venta comercial {sale.SaleNumber}", sale.SaleNumber, userId, lines);
    }

    public Task<JournalEntry> PostCustomerPaymentAsync(CustomerPayment payment, string? userId = null)
    {
        string cashOrBank = payment.PaymentMethod == PaymentMethod.Cash ? AccountCodes.Cash : AccountCodes.Bank;
        return PostAsync(payment.PaymentDate, $"Cobro a cliente #{payment.CustomerId}", payment.Reference, userId,
        [
            (cashOrBank, payment.Amount, 0m, $"Cobro cliente {payment.Reference}"),
            (AccountCodes.AccountsReceivable, 0m, payment.Amount, $"Abono CxC {payment.Reference}")
        ]);
    }

    public Task<JournalEntry> PostRawMaterialAdjustmentAsync(RawMaterial material, decimal quantityDelta, string reason, string? userId = null)
    {
        decimal amount = Math.Round(Math.Abs(quantityDelta) * material.AverageCost, 2, MidpointRounding.AwayFromZero);
        return PostInventoryAdjustmentAsync(
            AccountCodes.RawMaterialInventory, amount, quantityDelta > 0m,
            $"Ajuste positivo {material.Sku}: {reason}", $"Contrapartida ajuste positivo {material.Sku}",
            $"Ajuste negativo {material.Sku}: {reason}", $"Salida inventario por ajuste {material.Sku}",
            $"Ajuste inventario MP {material.Sku}", material.Sku, userId);
    }

    public Task<JournalEntry> PostFinishedBatchAdjustmentAsync(FinishedBatch batch, int quantityDelta, string reason, string? userId = null)
    {
        decimal amount = Math.Round(Math.Abs(quantityDelta) * batch.UnitCost, 2, MidpointRounding.AwayFromZero);
        return PostInventoryAdjustmentAsync(
            AccountCodes.FinishedGoodsInventory, amount, quantityDelta > 0,
            $"Ajuste positivo lote {batch.BatchCode}: {reason}", $"Contrapartida ajuste positivo lote {batch.BatchCode}",
            $"Baja/merma lote {batch.BatchCode}: {reason}", $"Salida inventario lote {batch.BatchCode}",
            $"Ajuste inventario PT {batch.BatchCode}", batch.BatchCode, userId);
    }

    private Task<JournalEntry> PostInventoryAdjustmentAsync(
        string assetCode, decimal amount, bool isPositive,
        string posDebitDesc, string posCreditDesc, string negDebitDesc, string negCreditDesc,
        string concept, string reference, string? userId)
    {
        if (amount <= 0m)
            throw new InvalidOperationException("El ajuste no tiene valor contable mayor a cero.");

        List<(string, decimal, decimal, string)> lines = isPositive
            ? [(assetCode, amount, 0m, posDebitDesc), (AccountCodes.RetainedEarnings, 0m, amount, posCreditDesc)]
            : [(AccountCodes.AbnormalProductionLoss, amount, 0m, negDebitDesc), (assetCode, 0m, amount, negCreditDesc)];
        return PostAsync(DateTime.UtcNow, concept, reference, userId, lines);
    }

    private Task<JournalEntry> PostAsync(
        DateTime date, string concept, string reference, string? userId,
        List<(string Code, decimal Debit, decimal Credit, string Desc)> lines)
        => AccountingEntryPoster.CreatePostedEntryAsync(_contextFactory, date, concept, reference, userId, lines);
}
