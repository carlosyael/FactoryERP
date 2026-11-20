using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class SaleService : ISaleService
{
    private static readonly SemaphoreSlim SaleGate = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ISystemSettingService _settings;
    private readonly IManufacturingAccountingService _accounting;
    private readonly ITaxDocumentProvider _taxDocumentProvider;

    public SaleService(
        IDbContextFactory<AppDbContext> contextFactory,
        ISystemSettingService settings,
        IManufacturingAccountingService accounting,
        ITaxDocumentProvider taxDocumentProvider)
    {
        _contextFactory = contextFactory;
        _settings = settings;
        _accounting = accounting;
        _taxDocumentProvider = taxDocumentProvider;
    }

    public async Task<List<Sale>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Items).ThenInclude(i => i.FinishedBatch)
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id).ToListAsync();
    }

    public async Task<Sale?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Items).ThenInclude(i => i.FinishedBatch)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Sale> CreateSaleAsync(CreateSaleRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("La venta debe incluir al menos un producto.");

        decimal maxDiscount = await _settings.GetDecimalAsync(SettingKeys.SalesMaxDiscountPercentage, 30m);
        decimal defaultTaxRate = await _settings.GetDecimalAsync(SettingKeys.FiscalDefaultTaxRate, 0.18m);

        await SaleGate.WaitAsync();
        try
        {
            Sale sale;
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                using var tx = await context.Database.BeginTransactionAsync();
                var customer = await context.Customers.FindAsync(request.CustomerId)
                    ?? throw new InvalidOperationException("Cliente no encontrado.");

                var productIds = request.Items.Select(i => i.ProductId).Distinct().OrderBy(id => id).ToList();
                var products = await context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
                var batches = await context.FinishedBatches
                    .Where(b => productIds.Contains(b.ProductId) && b.QuantityRemaining > 0)
                    .OrderBy(b => b.ExpirationDate ?? DateTime.MaxValue)
                    .ThenBy(b => b.ProductionDate)
                    .ThenBy(b => b.Id)
                    .ToListAsync();

                var summary = FefoBatchAllocator.Allocate(
                    request.Items, products, batches, customer.CustomerType, maxDiscount, defaultTaxRate);

                decimal total = (summary.SubTotal - summary.TotalDiscount) + summary.TotalTax;
                if (request.PaymentMethod == PaymentMethod.Credit)
                {
                    decimal availableCredit = customer.CreditLimit - customer.CurrentBalance;
                    if (total > availableCredit)
                        throw new InvalidOperationException(
                            $"Crédito insuficiente para '{customer.Name}'. Disponible: RD$ {availableCredit:N2}, Total: RD$ {total:N2}.");
                    customer.CurrentBalance += total;
                }

                sale = new Sale
                {
                    SaleNumber = $"VTA-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
                    SaleDate = request.SaleDate ?? DateTime.UtcNow,
                    CustomerId = customer.Id,
                    PaymentMethod = request.PaymentMethod,
                    SubTotal = summary.SubTotal,
                    DiscountAmount = summary.TotalDiscount,
                    TaxAmount = summary.TotalTax,
                    Total = total,
                    TotalCogs = summary.TotalCogs,
                    Notes = request.Notes.Trim(),
                    Items = summary.SaleItems
                };

                var taxResult = await _taxDocumentProvider.IssueForSaleAsync(sale);
                sale.ElectronicInvoiceStatus = taxResult.Status;
                sale.TaxDocumentNumber = taxResult.DocumentNumber;

                context.Sales.Add(sale);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }

            if (sale.Total > 0m || sale.TotalCogs > 0m)
            {
                await _accounting.PostSaleAsync(sale);
            }
            return (await GetByIdAsync(sale.Id))!;
        }
        finally
        {
            SaleGate.Release();
        }
    }
}
