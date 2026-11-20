using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class InventoryKardexServiceTests
{
    [Fact]
    public async Task EndToEndMovements_RecordsPurchaseAdjustmentProductionSaleAndBatchWriteOffWithFilters()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierSvc = new SupplierService(factory, accounting);
        var matSvc = new RawMaterialService(factory, accounting);
        var purchaseSvc = new PurchaseOrderService(factory, settings, accounting);
        var productSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);
        var qualitySvc = new BatchQualityService(factory, accounting);
        var customerSvc = new CustomerService(factory, accounting);
        var saleSvc = new SaleService(factory, settings, accounting, new PendingElectronicInvoiceProvider());
        var kardexSvc = new InventoryKardexService(factory);

        var supplier = await supplierSvc.CreateAsync(new Supplier { Name = "Química Proveedor" });
        var baseMat = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "KDX-MP-1", Name = "Base Capilar", UnitOfMeasure = UnitOfMeasure.Kilogram, CurrentStock = 0m, AverageCost = 0m
        });
        var product = await productSvc.CreateAsync(new Product
        {
            Sku = "KDX-PT-1", Name = "Shampoo Kardex", RetailPrice = 900m, IsTaxable = false
        });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Receta KDX", Version = "1.0", BaseYieldQuantity = 10,
            StandardLossMarginPercent = 0m, OverheadCostPerBatch = 100m,
            Items = [new() { RawMaterialId = baseMat.Id, QuantityRequired = 4000m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });
        var customer = await customerSvc.CreateAsync(new Customer { Name = "Cliente KDX", CustomerType = CustomerType.Retail });

        // 1. Compra: +10 kg @ 500
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, DateTime.UtcNow, "Compra KDX",
            [new(baseMat.Id, 10m, 500m, false)]));

        // 2. Ajuste positivo MP: 10 -> 12 kg (+2)
        await matSvc.AdjustStockAsync(baseMat.Id, 12m, "Conteo físico positivo");

        // 3. Producción OP1: inicio (-4 kg -> 8), cierre usando 4.5 kg (-0.5 kg -> 7.5) y produce 10 uds
        var op1 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 0m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(12), "LOT-KDX-1"));
        await orderSvc.StartProductionAsync(op1.Id);
        await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(
            op1.Id, 10, 100m, null, "Cierre KDX", new Dictionary<int, decimal> { [baseMat.Id] = 4.5m }));

        // 4. Producción OP2 cancelada tras inicio: -4 -> 3.5, cancelación +4 -> 7.5
        var op2 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 0m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(12), "LOT-KDX-2"));
        await orderSvc.StartProductionAsync(op2.Id);
        await orderSvc.CancelProductionAsync(op2.Id, "Cancelación prueba KDX");

        // 5. Venta de 3 uds -> saldo lote 10 -> 7
        await saleSvc.CreateSaleAsync(new CreateSaleRequest(
            customer.Id, PaymentMethod.Cash, DateTime.UtcNow, "Venta KDX", [new(product.Id, 3)]));

        // 6. Baja de lote PT de 7 -> 5 (-2)
        var batch = (await productSvc.GetBatchesAsync(product.Id)).Single();
        await qualitySvc.AdjustBatchQuantityAsync(new AdjustBatchQuantityRequest(batch.Id, 5, "Frascos rotos"));

        var all = await kardexSvc.GetMovementsAsync();
        all.Should().HaveCount(9);

        var mpMovements = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(ItemType: InventoryItemType.RawMaterial, RawMaterialId: baseMat.Id));
        mpMovements.Should().HaveCount(6);
        mpMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.PurchaseReceipt && m.QuantityDelta == 10m && m.BalanceAfter == 10m && m.TotalValue == 5000m);
        mpMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.RawMaterialAdjustment && m.QuantityDelta == 2m && m.BalanceAfter == 12m);
        mpMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.ProductionConsumption && m.QuantityDelta == -4m && m.BalanceAfter == 8m && m.Reference == op1.OrderNumber);
        mpMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.ProductionMaterialReconciliation && m.QuantityDelta == -0.5m && m.BalanceAfter == 7.5m);
        mpMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.ProductionCancellationReturn && m.QuantityDelta == 4m && m.BalanceAfter == 7.5m && m.Reference == op2.OrderNumber);

        var ptMovements = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(ItemType: InventoryItemType.FinishedGood, ProductId: product.Id));
        ptMovements.Should().HaveCount(3);
        ptMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.ProductionBatchOutput && m.QuantityDelta == 10m && m.BalanceAfter == 10m && m.BatchCode == "LOT-KDX-1");
        ptMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.SaleDispatch && m.QuantityDelta == -3m && m.BalanceAfter == 7m);
        ptMovements.Should().ContainSingle(m => m.MovementType == InventoryMovementType.FinishedBatchAdjustment && m.QuantityDelta == -2m && m.BalanceAfter == 5m);

        var searchByBatch = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(Search: "LOT-KDX-1"));
        searchByBatch.Should().HaveCountGreaterThanOrEqualTo(3);

        var todayOnly = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(
            MovementType: InventoryMovementType.SaleDispatch,
            StartDate: DateTime.UtcNow.Date.AddDays(-1),
            EndDate: DateTime.UtcNow.Date.AddDays(1)));
        todayOnly.Should().ContainSingle();
    }

    private sealed class StubUser(string? userId, string? userName) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? UserName => userName;
    }

    [Fact]
    public async Task OpeningStockUserAttributionAndConcurrentMutations_PreserveAccurateKardexBalances()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var operatorUser = new StubUser(null, "operador.planta");
        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierSvc = new SupplierService(factory, accounting);
        var matSvc = new RawMaterialService(factory, accounting, operatorUser);
        var purchaseSvc = new PurchaseOrderService(factory, settings, accounting, operatorUser);
        var productSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting, operatorUser);
        var kardexSvc = new InventoryKardexService(factory);

        var supplier = await supplierSvc.CreateAsync(new Supplier { Name = "Suplidor K2" });
        var mat = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "K2-MP-1", Name = "Aceite Inicial", UnitOfMeasure = UnitOfMeasure.Milliliter, CurrentStock = 1000m, AverageCost = 2m
        });
        var product = await productSvc.CreateAsync(new Product { Sku = "K2-P1", Name = "Sérum", RetailPrice = 700m });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Receta K2", Version = "1.0", BaseYieldQuantity = 5, StandardLossMarginPercent = 0m,
            Items = [new() { RawMaterialId = mat.Id, QuantityRequired = 500m, UnitOfMeasure = UnitOfMeasure.Milliliter }]
        });
        var draft = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 5, 0m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(12), "LOT-K2-1"));

        var buyTask = Task.Run(() => purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, DateTime.UtcNow, "Compra concurrente", [new(mat.Id, 600m, 2m, false)])));
        var startTask = Task.Run(() => orderSvc.StartProductionAsync(draft.Id));
        await Task.WhenAll(buyTask, startTask);

        var refreshedMat = await matSvc.GetByIdAsync(mat.Id);
        refreshedMat!.CurrentStock.Should().Be(1100m); // 1000 + 600 - 500

        var movements = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(RawMaterialId: mat.Id));
        movements.Should().HaveCount(3);
        movements.Should().ContainSingle(m => m.Notes == "Inventario inicial" && m.QuantityDelta == 1000m && m.BalanceAfter == 1000m);
        movements.Should().OnlyContain(m => m.PerformedBy == "operador.planta");
        movements.Sum(m => m.QuantityDelta).Should().Be(1100m);
    }
}
