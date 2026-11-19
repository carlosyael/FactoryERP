using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class SaleServiceTests
{
    private static async Task<(
        Product Product,
        Customer SalonCustomer,
        SaleService SaleService,
        ProductService ProductService,
        CustomerService CustomerService,
        JournalEntryService JournalService)> SetupAsync(DbContextSqliteFactory factory, int batch1Qty = 5, int batch2Qty = 10)
    {
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var taxProvider = new PendingElectronicInvoiceProvider();
        var productService = new ProductService(factory);
        var customerService = new CustomerService(factory, accounting);
        var saleService = new SaleService(factory, settings, accounting, taxProvider);
        var journalService = new JournalEntryService(factory);

        var product = await productService.CreateAsync(new Product
        {
            Sku = "CAP-SERUM-01",
            Name = "Sérum Capilar Brillo",
            RetailPrice = 800m,
            SalonPrice = 600m,
            WholesalePrice = 500m,
            Cost = 220m,
            IsTaxable = true,
            TaxRate = 0.18m
        });

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.FinishedBatches.AddRange(
                new FinishedBatch
                {
                    ProductId = product.Id,
                    BatchCode = "LOT-EXP-SOON",
                    ProductionDate = DateTime.UtcNow.AddDays(-30),
                    ExpirationDate = DateTime.UtcNow.AddMonths(3),
                    InitialQuantity = batch1Qty,
                    QuantityRemaining = batch1Qty,
                    UnitCost = 200m
                },
                new FinishedBatch
                {
                    ProductId = product.Id,
                    BatchCode = "LOT-EXP-LATER",
                    ProductionDate = DateTime.UtcNow.AddDays(-5),
                    ExpirationDate = DateTime.UtcNow.AddMonths(12),
                    InitialQuantity = batch2Qty,
                    QuantityRemaining = batch2Qty,
                    UnitCost = 240m
                });
            await ctx.SaveChangesAsync();
        }

        var salon = await customerService.CreateAsync(new Customer
        {
            Name = "Salón Glamour",
            RncCedula = "101555666",
            CustomerType = CustomerType.Salon,
            CreditLimit = 10000m
        });

        return (product, salon, saleService, productService, customerService, journalService);
    }

    [Fact]
    public async Task CreateSale_UsesSalonPricingFefoBatchesItbis18AndPostsAccounting()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, salon, saleService, productService, customerService, journalService) = await SetupAsync(factory, 5, 10);

        // Compra 8 unidades: 5 del lote 1 (200 c/u = 1000) + 3 del lote 2 (240 c/u = 720) => COGS = 1720
        // Precio Salón = 600 * 8 = 4800; ITBIS 18% = 864; Total = 5664
        var sale = await saleService.CreateSaleAsync(new CreateSaleRequest(
            salon.Id,
            PaymentMethod.Credit,
            DateTime.UtcNow,
            "Pedido salón",
            [new(product.Id, 8)]));

        sale.SubTotal.Should().Be(4800m);
        sale.TaxAmount.Should().Be(864m);
        sale.Total.Should().Be(5664m);
        sale.TotalCogs.Should().Be(1720m);
        sale.ElectronicInvoiceStatus.Should().Be(ElectronicInvoiceStatus.PendingImplementation);

        var batches = await productService.GetBatchesAsync(product.Id);
        batches.First(b => b.BatchCode == "LOT-EXP-SOON").QuantityRemaining.Should().Be(0);
        batches.First(b => b.BatchCode == "LOT-EXP-LATER").QuantityRemaining.Should().Be(7);

        var updatedSalon = await customerService.GetByIdAsync(salon.Id);
        updatedSalon!.CurrentBalance.Should().Be(5664m);

        var entries = await journalService.GetAllAsync();
        entries.Should().ContainSingle();
        entries[0].Lines.Sum(l => l.Debit).Should().Be(entries[0].Lines.Sum(l => l.Credit));
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.TaxPayable && l.Credit == 864m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.CostOfGoodsSold && l.Debit == 1720m);
    }

    [Fact]
    public async Task CreateSale_CreditLimitExceeded_ThrowsInvalidOperationException()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, salon, saleService, _, _, _) = await SetupAsync(factory, 10, 20);

        // 20 * 600 * 1.18 = 14,160 > CreditLimit 10,000
        Func<Task> act = () => saleService.CreateSaleAsync(new CreateSaleRequest(
            salon.Id, PaymentMethod.Credit, DateTime.UtcNow, "", [new(product.Id, 20)]));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConcurrentSales_CompetingForLastStock_PreventsNegativeStock()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, salon, saleService, productService, _, _) = await SetupAsync(factory, batch1Qty: 5, batch2Qty: 0);

        var tasks = new[]
        {
            Task.Run(async () => { try { await saleService.CreateSaleAsync(new(salon.Id, PaymentMethod.Cash, null, "", [new(product.Id, 4)])); return true; } catch (InvalidOperationException) { return false; } }),
            Task.Run(async () => { try { await saleService.CreateSaleAsync(new(salon.Id, PaymentMethod.Cash, null, "", [new(product.Id, 4)])); return true; } catch (InvalidOperationException) { return false; } })
        };

        bool[] outcomes = await Task.WhenAll(tasks);
        outcomes.Count(x => x).Should().Be(1);
        var batches = await productService.GetBatchesAsync(product.Id);
        batches.Sum(b => b.QuantityRemaining).Should().Be(1);
    }
}
