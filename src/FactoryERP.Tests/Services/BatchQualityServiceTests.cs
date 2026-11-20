using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class BatchQualityServiceTests
{
    private sealed class StubCurrentUser(string? userId, string? userName) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserName => userName;
    }

    [Fact]
    public async Task ProductionWithRequireQc_PlacesBatchInQuarantine_BlocksCommercialChainUntilApproved()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        await settings.UpdateSettingAsync(SettingKeys.ProductionRequireQualityApproval, "true");

        var accounting = new ManufacturingAccountingService(factory);
        var matSvc = new RawMaterialService(factory, accounting);
        var prodSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);
        var qualitySvc = new BatchQualityService(factory, accounting, new StubCurrentUser(null, "inspector.qc"));
        var catalogSvc = new CatalogService(factory, settings);
        var customerSvc = new CustomerService(factory, accounting);
        var saleSvc = new SaleService(factory, settings, accounting, new PendingElectronicInvoiceProvider());

        var mat = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "QC-M1", Name = "Base Shampoo", UnitOfMeasure = UnitOfMeasure.Gram, CurrentStock = 5000m, AverageCost = 1m
        });
        var prod = await prodSvc.CreateAsync(new Product
        {
            Sku = "QC-P1", Name = "Shampoo QC", RetailPrice = 600m, ShowInPublicCatalog = true
        });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod.Id, Name = "Receta QC", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m,
            Items = [new() { RawMaterialId = mat.Id, QuantityRequired = 1000m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });
        var customer = await customerSvc.CreateAsync(new Customer { Name = "Cliente QC", CustomerType = CustomerType.Retail });

        var draft = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 0m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(12), "LOT-QC-1"));
        await orderSvc.StartProductionAsync(draft.Id);
        await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(draft.Id, 10, 0m, null));

        var batches = await prodSvc.GetBatchesAsync(prod.Id);
        var batch = batches.Should().ContainSingle().Subject;
        batch.QualityStatus.Should().Be(BatchQualityStatus.Quarantine);

        (await catalogSvc.GetInternalCatalogAsync()).First(x => x.ProductId == prod.Id).AvailableStock.Should().Be(0);
        (await catalogSvc.GetPublicCatalogAsync()).First(x => x.ProductId == prod.Id).InStock.Should().BeFalse();

        Func<Task> quarantineSale = () => saleSvc.CreateSaleAsync(new CreateSaleRequest(
            customer.Id, PaymentMethod.Cash, null, "", [new(prod.Id, 1)]));
        await quarantineSale.Should().ThrowAsync<InvalidOperationException>();

        var rejected = await qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(batch.Id, BatchQualityStatus.Rejected, "pH fuera de rango"));
        rejected.QualityStatus.Should().Be(BatchQualityStatus.Rejected);
        rejected.QualityCheckedBy.Should().Be("inspector.qc");
        rejected.QualityCheckedAt.Should().NotBeNull();
        await quarantineSale.Should().ThrowAsync<InvalidOperationException>();

        var approved = await qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(batch.Id, BatchQualityStatus.Approved, "Aprobado en laboratorio"));
        approved.QualityStatus.Should().Be(BatchQualityStatus.Approved);

        (await catalogSvc.GetInternalCatalogAsync()).First(x => x.ProductId == prod.Id).AvailableStock.Should().Be(10);
        var sale = await saleSvc.CreateSaleAsync(new CreateSaleRequest(
            customer.Id, PaymentMethod.Cash, null, "", [new(prod.Id, 4)]));
        sale.Items.Should().ContainSingle(i => i.FinishedBatchId == batch.Id && i.Quantity == 4);
    }

    [Fact]
    public async Task ExpiredBatch_IsBlockedFromSaleCatalogAndCountedAsLowSellableStock()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();
        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var prodSvc = new ProductService(factory);
        var catalogSvc = new CatalogService(factory, settings);
        var dashboardSvc = new DashboardService(factory);
        var customerSvc = new CustomerService(factory, accounting);
        var saleSvc = new SaleService(factory, settings, accounting, new PendingElectronicInvoiceProvider());

        var prod = await prodSvc.CreateAsync(new Product
        {
            Sku = "EXP-P1", Name = "Acondicionador Vencido", RetailPrice = 400m, MinimumStockLevel = 5, ShowInPublicCatalog = true
        });
        var customer = await customerSvc.CreateAsync(new Customer { Name = "Cliente Exp", CustomerType = CustomerType.Retail });

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.FinishedBatches.Add(new FinishedBatch
            {
                ProductId = prod.Id,
                BatchCode = "LOT-EXP-01",
                ProductionDate = DateTime.UtcNow.AddMonths(-12),
                ExpirationDate = DateTime.UtcNow.Date.AddDays(-2),
                InitialQuantity = 15,
                QuantityRemaining = 15,
                UnitCost = 100m,
                QualityStatus = BatchQualityStatus.Approved
            });
            await ctx.SaveChangesAsync();
        }

        (await catalogSvc.GetInternalCatalogAsync()).First(x => x.ProductId == prod.Id).AvailableStock.Should().Be(0);
        (await dashboardSvc.GetKpisAsync()).LowStockProductsCount.Should().Be(1);

        Func<Task> expiredSale = () => saleSvc.CreateSaleAsync(new CreateSaleRequest(
            customer.Id, PaymentMethod.Cash, null, "", [new(prod.Id, 1)]));
        await expiredSale.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AdjustBatchQuantity_PostsBalancedWriteOffAndPositiveAccountingEntries()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();
        var accounting = new ManufacturingAccountingService(factory);
        var prodSvc = new ProductService(factory);
        var qualitySvc = new BatchQualityService(factory, accounting, new StubCurrentUser(null, "admin"));
        var journalSvc = new JournalEntryService(factory);

        var prod = await prodSvc.CreateAsync(new Product { Sku = "ADJ-P1", Name = "Mascarilla", RetailPrice = 700m });
        int batchId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var batch = new FinishedBatch
            {
                ProductId = prod.Id,
                BatchCode = "LOT-ADJ-1",
                InitialQuantity = 10,
                QuantityRemaining = 10,
                UnitCost = 100m,
                QualityStatus = BatchQualityStatus.Approved
            };
            ctx.FinishedBatches.Add(batch);
            await ctx.SaveChangesAsync();
            batchId = batch.Id;
        }

        Func<Task> negAct = () => qualitySvc.AdjustBatchQuantityAsync(new AdjustBatchQuantityRequest(batchId, -1, "Inválido"));
        await negAct.Should().ThrowAsync<ArgumentException>();

        Func<Task> noReasonAct = () => qualitySvc.AdjustBatchQuantityAsync(new AdjustBatchQuantityRequest(batchId, 8, "  "));
        await noReasonAct.Should().ThrowAsync<ArgumentException>();

        var afterWriteOff = await qualitySvc.AdjustBatchQuantityAsync(
            new AdjustBatchQuantityRequest(batchId, 7, "Muestras y frascos dañados"));
        afterWriteOff.QuantityRemaining.Should().Be(7);

        var afterSurplus = await qualitySvc.AdjustBatchQuantityAsync(
            new AdjustBatchQuantityRequest(batchId, 9, "Reconteo físico"));
        afterSurplus.QuantityRemaining.Should().Be(9);

        var entries = await journalSvc.GetAllAsync();
        entries.Should().HaveCount(2);
        entries.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
        entries[1].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.AbnormalProductionLoss && l.Debit == 300m);
        entries[1].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.FinishedGoodsInventory && l.Credit == 300m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.FinishedGoodsInventory && l.Debit == 200m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.RetainedEarnings && l.Credit == 200m);
    }

    [Fact]
    public async Task QualityRules_AndConcurrentSaleVsQualityUpdate_EnforceStrictGuards()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();
        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var prodSvc = new ProductService(factory);
        var qualitySvc = new BatchQualityService(factory, accounting, new StubCurrentUser(null, "qc.lead"));
        var customerSvc = new CustomerService(factory, accounting);
        var saleSvc = new SaleService(factory, settings, accounting, new PendingElectronicInvoiceProvider());

        var prod = await prodSvc.CreateAsync(new Product { Sku = "QC-STR-1", Name = "Crema Estricto", RetailPrice = 500m });
        var customer = await customerSvc.CreateAsync(new Customer { Name = "Cliente Concurrencia", CustomerType = CustomerType.Retail });

        int expiredId;
        int liveId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var expiredBatch = new FinishedBatch
            {
                ProductId = prod.Id,
                BatchCode = "LOT-EXPIRED-CHK",
                ExpirationDate = DateTime.UtcNow.Date.AddDays(-1),
                InitialQuantity = 5,
                QuantityRemaining = 5,
                UnitCost = 100m,
                QualityStatus = BatchQualityStatus.Quarantine
            };
            var liveBatch = new FinishedBatch
            {
                ProductId = prod.Id,
                BatchCode = "LOT-RACE-1",
                ExpirationDate = DateTime.UtcNow.Date.AddMonths(6),
                InitialQuantity = 10,
                QuantityRemaining = 10,
                UnitCost = 100m,
                QualityStatus = BatchQualityStatus.Approved
            };
            ctx.FinishedBatches.AddRange(expiredBatch, liveBatch);
            await ctx.SaveChangesAsync();
            expiredId = expiredBatch.Id;
            liveId = liveBatch.Id;
        }

        Func<Task> approveExpired = () => qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(expiredId, BatchQualityStatus.Approved, "Intentar aprobar vencido"));
        await approveExpired.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> rejectNoNotes = () => qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(liveId, BatchQualityStatus.Rejected, "   "));
        await rejectNoNotes.Should().ThrowAsync<ArgumentException>();

        Func<Task> invalidStatus = () => qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(liveId, (BatchQualityStatus)99, "Inválido"));
        await invalidStatus.Should().ThrowAsync<ArgumentOutOfRangeException>();

        Func<Task> noOpAdjust = () => qualitySvc.AdjustBatchQuantityAsync(
            new AdjustBatchQuantityRequest(liveId, 10, "Sin cambio"));
        await noOpAdjust.Should().ThrowAsync<InvalidOperationException>();

        var tSale = Task.Run(async () =>
        {
            try
            {
                await saleSvc.CreateSaleAsync(new CreateSaleRequest(customer.Id, PaymentMethod.Cash, null, "", [new(prod.Id, 6)]));
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });
        var tQuarantine = Task.Run(() => qualitySvc.UpdateQualityStatusAsync(
            new UpdateBatchQualityRequest(liveId, BatchQualityStatus.Quarantine, "Retención concurrente")));

        await Task.WhenAll(tSale, tQuarantine);
        bool saleSucceeded = await tSale;
        var refreshed = (await prodSvc.GetBatchesAsync(prod.Id)).First(b => b.Id == liveId);
        refreshed.QualityStatus.Should().Be(BatchQualityStatus.Quarantine);
        refreshed.QuantityRemaining.Should().Be(saleSucceeded ? 4 : 10);
    }
}
