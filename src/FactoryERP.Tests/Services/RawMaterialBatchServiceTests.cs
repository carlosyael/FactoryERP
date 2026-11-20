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
    }
}
