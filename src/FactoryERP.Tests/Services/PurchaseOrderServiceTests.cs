using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class PurchaseOrderServiceTests
{
    [Fact]
    public async Task ReceivePurchaseAndSupplierPayment_UpdatesCppStockBalanceAndAccounting()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierService = new SupplierService(factory, accounting);
        var materialService = new RawMaterialService(factory);
        var purchaseService = new PurchaseOrderService(factory, settings, accounting);

        var supplier = await supplierService.CreateAsync(new Supplier
        {
            Name = "Química Caribe SRL",
            Rnc = "130123456"
        });

        var material = await materialService.CreateAsync(new RawMaterial
        {
            Sku = "MP-KER-01",
            Name = "Queratina Hidrolizada",
            UnitOfMeasure = UnitOfMeasure.Milliliter,
            CurrentStock = 100m,
            AverageCost = 10m
        });

        var po = await purchaseService.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id,
            PaymentMethod.Credit,
            DateTime.UtcNow,
            "Compra a crédito",
            [new(material.Id, 100m, 20m, IsTaxable: true)]));

        po.Status.Should().Be(PurchaseOrderStatus.Received);
        po.SubTotal.Should().Be(2000m);
        po.TaxAmount.Should().Be(360m);
        po.Total.Should().Be(2360m);

        var refreshedMat = await materialService.GetByIdAsync(material.Id);
        refreshedMat!.CurrentStock.Should().Be(200m);
        refreshedMat.AverageCost.Should().Be(15m); // (100*10 + 100*20)/200

        var refreshedSup = await supplierService.GetByIdAsync(supplier.Id);
        refreshedSup!.CurrentBalance.Should().Be(2360m);

        await supplierService.RegisterPaymentAsync(supplier.Id, 1000m, PaymentMethod.BankTransfer, "TRF-01");
        refreshedSup = await supplierService.GetByIdAsync(supplier.Id);
        refreshedSup!.CurrentBalance.Should().Be(1360m);

        var journal = new JournalEntryService(factory);
        var entries = await journal.GetAllAsync();
        entries.Should().HaveCount(2);
        entries.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
        entries.SelectMany(e => e.Lines).Should().Contain(l => l.Account!.Code == AccountCodes.RawMaterialInventory && l.Debit == 2000m);
    }

    [Fact]
    public async Task ReceivePurchase_CashPurchaseAndInvalidQuantities_BehavesStrictly()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierService = new SupplierService(factory, accounting);
        var materialService = new RawMaterialService(factory, accounting);
        var purchaseService = new PurchaseOrderService(factory, settings, accounting);

        var supplier = await supplierService.CreateAsync(new Supplier { Name = "Suplidor Contado" });
        var mat = await materialService.CreateAsync(new RawMaterial { Sku = "MP-EX-1", Name = "Agua Desmineralizada", UnitOfMeasure = UnitOfMeasure.Liter });

        Func<Task> emptyItems = () => purchaseService.ReceivePurchaseAsync(new(supplier.Id, PaymentMethod.Cash, null, "", []));
        await emptyItems.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> zeroQty = () => purchaseService.ReceivePurchaseAsync(new(supplier.Id, PaymentMethod.Cash, null, "", [new(mat.Id, 0m, 10m)]));
        await zeroQty.Should().ThrowAsync<ArgumentException>();

        var po = await purchaseService.ReceivePurchaseAsync(new(supplier.Id, PaymentMethod.Cash, null, "Contado exento", [new(mat.Id, 50m, 10m, IsTaxable: false)]));
        po.TaxAmount.Should().Be(0m);
        po.Total.Should().Be(500m);
        po.AmountPaid.Should().Be(500m);
        (await supplierService.GetByIdAsync(supplier.Id))!.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task ReceivePurchase_ZeroUnitCostSample_UpdatesStockWithoutAccountingFailure()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierService = new SupplierService(factory, accounting);
        var materialService = new RawMaterialService(factory, accounting);
        var purchaseService = new PurchaseOrderService(factory, settings, accounting);

        var supplier = await supplierService.CreateAsync(new Supplier { Name = "Suplidor Muestras" });
        var mat = await materialService.CreateAsync(new RawMaterial { Sku = "MP-SMP-1", Name = "Muestra Fragancia", UnitOfMeasure = UnitOfMeasure.Milliliter });

        var po = await purchaseService.ReceivePurchaseAsync(new(supplier.Id, PaymentMethod.Cash, null, "Muestra gratis", [new(mat.Id, 25m, 0m, IsTaxable: true)]));
        po.Total.Should().Be(0m);
        (await materialService.GetByIdAsync(mat.Id))!.CurrentStock.Should().Be(25m);
    }
}
