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

        lines.Add((ResolveCreditSettlementCode(purchaseOrder.PaymentMethod), 0m, purchaseOrder.Total, $"Pago/CxP {purchaseOrder.OrderNumber}"));
        return CreatePostedEntryAsync(purchaseOrder.PurchaseDate, $"Recepción de compra {purchaseOrder.OrderNumber}", purchaseOrder.OrderNumber, userId, lines);
    }

    public Task<JournalEntry> PostSupplierPaymentAsync(SupplierPayment payment, string? userId = null)
    {
        string cashOrBank = payment.PaymentMethod == PaymentMethod.Cash ? AccountCodes.Cash : AccountCodes.Bank;
        var lines = new List<(string, decimal, decimal, string)>
        {
            (AccountCodes.AccountsPayable, payment.Amount, 0m, $"Abono a suplidor {payment.Reference}"),
            (cashOrBank, 0m, payment.Amount, $"Desembolso pago suplidor {payment.Reference}")
        };
        return CreatePostedEntryAsync(payment.PaymentDate, $"Pago a suplidor #{payment.SupplierId}", payment.Reference, userId, lines);
    }

    public Task<JournalEntry> PostProductionStartAsync(ProductionOrder order, decimal materialCost, string? userId = null)
    {
        decimal roundedMat = Math.Round(materialCost, 2, MidpointRounding.AwayFromZero);
        var lines = new List<(string, decimal, decimal, string)>
        {
            (AccountCodes.WorkInProgressInventory, roundedMat, 0m, $"Inicio orden {order.OrderNumber} (Lote {order.BatchCode})"),
            (AccountCodes.RawMaterialInventory, 0m, roundedMat, $"Consumo materiales {order.OrderNumber}")
        };
        return CreatePostedEntryAsync(order.StartedAt ?? DateTime.UtcNow, $"Inicio fabricación {order.OrderNumber}", order.OrderNumber, userId, lines);
    }

    public Task<JournalEntry> PostProductionCompletionAsync(ProductionOrder order, string? userId = null)
    {
        decimal wipClear = Math.Round(order.ActualMaterialCost, 2, MidpointRounding.AwayFromZero);
        decimal overheadApplied = Math.Round(order.ActualOverheadCost, 2, MidpointRounding.AwayFromZero);
        var lines = new List<(string, decimal, decimal, string)>();

        if (order.CapitalizedInventoryCost > 0m)
            lines.Add((AccountCodes.FinishedGoodsInventory, order.CapitalizedInventoryCost, 0m, $"Ingreso producto terminado lote {order.BatchCode}"));
        if (order.AbnormalLossCost > 0m)
            lines.Add((AccountCodes.AbnormalProductionLoss, order.AbnormalLossCost, 0m, $"Merma anormal orden {order.OrderNumber}"));
        if (wipClear > 0m)
            lines.Add((AccountCodes.WorkInProgressInventory, 0m, wipClear, $"Liquidación WIP materiales orden {order.OrderNumber}"));
        if (overheadApplied > 0m)
            lines.Add((AccountCodes.ManufacturingOverheadApplied, 0m, overheadApplied, $"CIF real aplicado orden {order.OrderNumber}"));

        return CreatePostedEntryAsync(order.CompletedAt ?? DateTime.UtcNow, $"Cierre fabricación {order.OrderNumber}", order.OrderNumber, userId, lines);
    }

    public Task<JournalEntry> PostProductionCancellationAsync(ProductionOrder order, string? userId = null)
    {
        decimal mat = Math.Round(order.ActualMaterialCost, 2, MidpointRounding.AwayFromZero);
        var lines = new List<(string, decimal, decimal, string)>
        {
            (AccountCodes.RawMaterialInventory, mat, 0m, $"Devolución materiales por cancelación {order.OrderNumber}"),
            (AccountCodes.WorkInProgressInventory, 0m, mat, $"Reverso WIP cancelación {order.OrderNumber}")
        };
        return CreatePostedEntryAsync(DateTime.UtcNow, $"Cancelación fabricación {order.OrderNumber}", order.OrderNumber, userId, lines);
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

        return CreatePostedEntryAsync(sale.SaleDate, $"Venta comercial {sale.SaleNumber}", sale.SaleNumber, userId, lines);
    }

    public Task<JournalEntry> PostCustomerPaymentAsync(CustomerPayment payment, string? userId = null)
    {
        string cashOrBank = payment.PaymentMethod == PaymentMethod.Cash ? AccountCodes.Cash : AccountCodes.Bank;
        var lines = new List<(string, decimal, decimal, string)>
        {
            (cashOrBank, payment.Amount, 0m, $"Cobro cliente {payment.Reference}"),
            (AccountCodes.AccountsReceivable, 0m, payment.Amount, $"Abono CxC {payment.Reference}")
        };
        return CreatePostedEntryAsync(payment.PaymentDate, $"Cobro a cliente #{payment.CustomerId}", payment.Reference, userId, lines);
    }

    private static string ResolveCreditSettlementCode(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => AccountCodes.Cash,
        PaymentMethod.Credit => AccountCodes.AccountsPayable,
        _ => AccountCodes.Bank
    };

    private async Task<JournalEntry> CreatePostedEntryAsync(
        DateTime date, string concept, string reference, string? userId,
        List<(string Code, decimal Debit, decimal Credit, string Desc)> rawLines)
    {
        var nonZero = rawLines.Where(l => l.Debit > 0m || l.Credit > 0m).ToList();
        if (nonZero.Count < 2)
            throw new InvalidOperationException("No se puede generar un asiento contable con menos de dos líneas válidas.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var codes = nonZero.Select(l => l.Code).Distinct().ToList();
        var accountMap = await context.AccountingAccounts.Where(a => codes.Contains(a.Code)).ToDictionaryAsync(a => a.Code);

        var entryLines = new List<JournalEntryLine>();
        foreach (var (code, debit, credit, desc) in nonZero)
        {
            if (!accountMap.TryGetValue(code, out var account))
                throw new InvalidOperationException($"Cuenta contable requerida '{code}' no existe en el plan de cuentas.");

            entryLines.Add(new JournalEntryLine
            {
                AccountId = account.Id,
                Debit = Math.Round(debit, 2, MidpointRounding.AwayFromZero),
                Credit = Math.Round(credit, 2, MidpointRounding.AwayFromZero),
                Description = desc
            });
        }

        decimal totalDebit = entryLines.Sum(l => l.Debit);
        decimal totalCredit = entryLines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            throw new InvalidOperationException($"Asiento automático descuadrado ({concept}): D={totalDebit:N2} vs C={totalCredit:N2}");

        string? validUserId = (!string.IsNullOrWhiteSpace(userId) && await context.Users.AnyAsync(u => u.Id == userId)) ? userId : null;
        var entry = new JournalEntry
        {
            EntryNumber = $"AST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Date = date,
            Concept = concept,
            Reference = reference,
            Status = JournalEntryStatus.Posted,
            PostedAt = DateTime.UtcNow,
            CreatedByUserId = validUserId,
            Lines = entryLines
        };

        context.JournalEntries.Add(entry);
        await context.SaveChangesAsync();

        return await context.JournalEntries.AsNoTracking()
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstAsync(j => j.Id == entry.Id);
    }
}
