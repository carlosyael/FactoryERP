using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class RawMaterialBatchServiceTests
{
    [Fact]
    public async Task RawMaterialBatches_FefoAllocationExpiredBlockReconciliationCancellationAndRecall_WorkEndToEnd()
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
        var batchSvc = new RawMaterialBatchService(factory);

        var supplier = await supplierSvc.CreateAsync(new Supplier { Name = "Distribuidora Aceites" });
        var mat = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "MP-LOT-1", Name = "Aceite de Coco", UnitOfMeasure = UnitOfMeasure.Milliliter, CurrentStock = 0m, AverageCost = 0m
        });
        var prod = await productSvc.CreateAsync(new Product { Sku = "PT-LOT-1", Name = "Elixir Capilar", RetailPrice = 950m });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod.Id, Name = "Receta Elixir", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m,
            Items = [new() { RawMaterialId = mat.Id, QuantityRequired = 1000m, UnitOfMeasure = UnitOfMeasure.Milliliter }]
        });

        var utcToday = DateTime.UtcNow.Date;
        // Compra 3 lotes: uno ya vencido (500ml), otro proximo a vencer (400ml, +10d), otro lejano (800ml, +90d)
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday.AddDays(-20), "Lote vencido",
            [new(mat.Id, 500m, 1m, false, "LMP-EXP", utcToday.AddDays(-1))]));
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday.AddDays(-5), "Lote proximo",
            [new(mat.Id, 400m, 1m, false, "LMP-SOON", utcToday.AddDays(10))]));
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday, "Lote lejano",
            [new(mat.Id, 800m, 1m, false, "LMP-LATER", utcToday.AddDays(90))]));

        // Stock fisico = 1700, pero vigente disponible = 1200 (500ml vencidos excluidos)
        var simTooLarge = await orderSvc.SimulateAsync(formula.Id, 15);
        simTooLarge.CanProduce.Should().BeFalse();
        simTooLarge.Materials.Single().AvailableStock.Should().Be(1200m);

        var opTooLarge = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 15, 0m, utcToday, utcToday.AddMonths(12), "LOT-TOO-BIG"));
        Func<Task> startExpired = () => orderSvc.StartProductionAsync(opTooLarge.Id);
        await startExpired.Should().ThrowAsync<InvalidOperationException>();

        // Producir 6 uds (600 ml): FEFO toma 400ml de LMP-SOON y 200ml de LMP-LATER
        var op1 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 6, 0m, utcToday, utcToday.AddMonths(12), "LOT-PT-FEFO"));
        var started1 = await orderSvc.StartProductionAsync(op1.Id);
        started1.BatchAllocations.Should().HaveCount(2);

        var batchesAfterStart = await batchSvc.GetBatchesAsync(mat.Id);
        var expBatch = batchesAfterStart.Single(b => b.BatchCode == "LMP-EXP");
        var soonBatch = batchesAfterStart.Single(b => b.BatchCode == "LMP-SOON");
        var laterBatch = batchesAfterStart.Single(b => b.BatchCode == "LMP-LATER");
        expBatch.QuantityRemaining.Should().Be(500m);
        soonBatch.QuantityRemaining.Should().Be(0m);
        laterBatch.QuantityRemaining.Should().Be(600m);

        // Completar consumiendo 700ml reales (+100ml extra de LMP-LATER -> 500ml restantes)
        await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(
            op1.Id, 6, 0m, null, null, new Dictionary<int, decimal> { [mat.Id] = 700m }));
        (await batchSvc.GetBatchesAsync(mat.Id)).Single(b => b.Id == laterBatch.Id).QuantityRemaining.Should().Be(500m);

        // Iniciar OP2 (300ml de LMP-LATER -> 200ml) y luego cancelar -> restaura exacto a LMP-LATER (500ml)
        var op2 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 3, 0m, utcToday, utcToday.AddMonths(12), "LOT-PT-CAN"));
        await orderSvc.StartProductionAsync(op2.Id);
        (await batchSvc.GetBatchesAsync(mat.Id)).Single(b => b.Id == laterBatch.Id).QuantityRemaining.Should().Be(200m);
        await orderSvc.CancelProductionAsync(op2.Id, "Cancelación FEFO");
        (await batchSvc.GetBatchesAsync(mat.Id)).Single(b => b.Id == laterBatch.Id).QuantityRemaining.Should().Be(500m);

        // Recall inverso sobre LMP-SOON y LMP-LATER
        var recallSoon = await batchSvc.GetBatchTraceabilityAsync(soonBatch.Id);
        recallSoon.Should().NotBeNull();
        recallSoon!.AffectedOrders.Should().ContainSingle(o =>
            o.OrderNumber == op1.OrderNumber &&
            o.OutputBatchCode == "LOT-PT-FEFO" &&
            o.QuantityConsumed == 400m &&
            o.FinishedBatchRemaining == 6);

        var recallLater = await batchSvc.GetBatchTraceabilityAsync(laterBatch.Id);
        recallLater!.AffectedOrders.Should().ContainSingle(o =>
            o.OrderNumber == op1.OrderNumber &&
            o.QuantityConsumed == 300m);

        var onlyWithStock = await batchSvc.GetBatchesAsync(mat.Id, onlyWithStock: true);
        onlyWithStock.Should().HaveCount(2);

        // Kardex debe reflejar ambos lotes en el consumo FEFO multi-lote ("LMP-SOON, LMP-LATER")
        var kardex = await new InventoryKardexService(factory).GetMovementsAsync(
            new InventoryMovementFilter(RawMaterialId: mat.Id, MovementType: InventoryMovementType.ProductionConsumption));
        kardex.Should().ContainSingle(m => m.Reference == op1.OrderNumber && m.BatchCode.Contains("LMP-SOON") && m.BatchCode.Contains("LMP-LATER"));
    }

    [Fact]
    public async Task AdjustBatchQuantity_AndRawMaterialDownwardAdjustment_KeepBatchesUsableStockKardexAndAccountingInSync()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supplierSvc = new SupplierService(factory, accounting);
        var matSvc = new RawMaterialService(factory, accounting);
        var purchaseSvc = new PurchaseOrderService(factory, settings, accounting);
        var batchSvc = new RawMaterialBatchService(factory, accounting);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);
        var kardexSvc = new InventoryKardexService(factory);
        var journalSvc = new JournalEntryService(factory);

        var supplier = await supplierSvc.CreateAsync(new Supplier { Name = "Química Norte" });
        var mat = await matSvc.CreateAsync(new RawMaterial { Sku = "MP-ADJ-1", Name = "Extracto Romero", UnitOfMeasure = UnitOfMeasure.Milliliter });
        var prod = await new ProductService(factory).CreateAsync(new Product { Sku = "PT-ADJ-1", Name = "Tónico Romero", RetailPrice = 600m });
        var formula = await new FormulaService(factory).CreateAsync(new ProductFormula
        {
            ProductId = prod.Id, Name = "Tónico v1", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m,
            Items = [new() { RawMaterialId = mat.Id, QuantityRequired = 1000m, UnitOfMeasure = UnitOfMeasure.Milliliter }]
        });

        var utcToday = DateTime.UtcNow.Date;
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday.AddDays(-15), "Lote vencido",
            [new(mat.Id, 500m, 2m, false, "LMP-EXP-1", utcToday.AddDays(-2))]));
        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday, "Lote vigente",
            [new(mat.Id, 400m, 2m, false, "LMP-OK-1", utcToday.AddDays(60))]));

        var batches = await batchSvc.GetBatchesAsync(mat.Id);
        var expBatch = batches.Single(b => b.BatchCode == "LMP-EXP-1");

        // Validaciones negativas y delta cero
        Func<Task> negQty = () => batchSvc.AdjustBatchQuantityAsync(new(expBatch.Id, -1m, "Negativo"));
        await negQty.Should().ThrowAsync<ArgumentException>();
        Func<Task> zeroDelta = () => batchSvc.AdjustBatchQuantityAsync(new(expBatch.Id, 500m, "Sin cambio"));
        await zeroDelta.Should().ThrowAsync<InvalidOperationException>();

        // Baja del lote vencido (500 -> 0): stock pasa de 900 a 400 y disponible vigente sigue siendo 400 (sin doble descuento)
        await batchSvc.AdjustBatchQuantityAsync(new(expBatch.Id, 0m, "Baja por vencimiento"));
        (await matSvc.GetByIdAsync(mat.Id))!.CurrentStock.Should().Be(400m);
        (await batchSvc.GetBatchesAsync(mat.Id, onlyWithStock: true)).Should().ContainSingle(b => b.BatchCode == "LMP-OK-1" && b.QuantityRemaining == 400m);
        (await orderSvc.SimulateAsync(formula.Id, 4)).CanProduce.Should().BeTrue();

        var movs = await kardexSvc.GetMovementsAsync(new InventoryMovementFilter(RawMaterialId: mat.Id, MovementType: InventoryMovementType.RawMaterialAdjustment));
        movs.Should().Contain(m => m.BatchCode == "LMP-EXP-1" && m.QuantityDelta == -500m);

        var entries = await journalSvc.GetAllAsync();
        entries.SelectMany(e => e.Lines).Should().Contain(l => l.Account!.Code == AccountCodes.AbnormalProductionLoss && l.Debit == 1000m);

        // Ajuste negativo directo en RawMaterialService (400 -> 250): drena el lote abierto para mantener consistencia lote vs material
        await matSvc.AdjustStockAsync(mat.Id, 250m, "Conteo físico con faltante");
        (await batchSvc.GetBatchesAsync(mat.Id, onlyWithStock: true)).Should().ContainSingle(b => b.BatchCode == "LMP-OK-1" && b.QuantityRemaining == 250m);
        (await orderSvc.SimulateAsync(formula.Id, 3)).Materials.Single().AvailableStock.Should().Be(250m);
    }
}
