using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class BulkManufacturingTests
{
    [Fact]
    public async Task TwoStageBulkToPackaging_FlowsAccountingFefoMultiPresentationAndTwoStageRecall()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var supSvc = new SupplierService(factory, accounting);
        var matSvc = new RawMaterialService(factory, accounting);
        var poSvc = new PurchaseOrderService(factory, settings, accounting);
        var prodSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);
        var batchSvc = new RawMaterialBatchService(factory, accounting);
        var reportsSvc = new FinancialReportService(factory);

        var supplier = await supSvc.CreateAsync(new Supplier { Name = "Química Base" });
        var oilMat = await matSvc.CreateAsync(new RawMaterial { Sku = "B-OIL", Name = "Aceite Keratina", UnitOfMeasure = UnitOfMeasure.Milliliter });
        var waterMat = await matSvc.CreateAsync(new RawMaterial { Sku = "B-WAT", Name = "Agua Desmineralizada", UnitOfMeasure = UnitOfMeasure.Milliliter });
        var bulkShampoo = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "BLK-SHP", Name = "Shampoo Base Granel", Category = RawMaterialCategory.SemiFinished, UnitOfMeasure = UnitOfMeasure.Liter
        });
        var bottle8 = await matSvc.CreateAsync(new RawMaterial { Sku = "EMP-8", Name = "Envase 8 oz", Category = RawMaterialCategory.Packaging, UnitOfMeasure = UnitOfMeasure.Unit });
        var bottle16 = await matSvc.CreateAsync(new RawMaterial { Sku = "EMP-16", Name = "Envase 16 oz", Category = RawMaterialCategory.Packaging, UnitOfMeasure = UnitOfMeasure.Unit });

        var utcToday = DateTime.UtcNow.Date;
        await poSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday, "Compra insumos base",
            [
                new(oilMat.Id, 2000m, 2m, false, "LMP-B-OIL", utcToday.AddDays(120)),
                new(waterMat.Id, 50000m, 0.10m, false, "LMP-B-WAT", utcToday.AddDays(180)),
                new(bottle8.Id, 100m, 20m, false, "LMP-EMP-8", null),
                new(bottle16.Id, 100m, 35m, false, "LMP-EMP-16", null)
            ]));

        // Etapa 1: Fabricacion de 50 L a granel (1000 ml aceite = 2,000 + 49,000 ml agua = 4,900 + CIF 1,000 => 7,900 / 50 = 158 RD$/L)
        var bulkFormula = await formulaSvc.CreateAsync(new ProductFormula
        {
            OutputRawMaterialId = bulkShampoo.Id, Name = "Base Shampoo Granel", Version = "1.0",
            BaseYieldQuantity = 50, StandardLossMarginPercent = 0m, OverheadCostPerBatch = 1000m,
            Items =
            [
                new() { RawMaterialId = oilMat.Id, QuantityRequired = 1000m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = waterMat.Id, QuantityRequired = 49000m, UnitOfMeasure = UnitOfMeasure.Milliliter }
            ]
        });

        var opBulk = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(
            bulkFormula.Id, 50, 0m, utcToday, utcToday.AddMonths(12), "LOT-BULK-50"));
        await orderSvc.StartProductionAsync(opBulk.Id);
        var doneBulk = await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(opBulk.Id, 50, 1000m, null));

        doneBulk.IsSemiFinishedOutput.Should().BeTrue();
        doneBulk.ActualUnitCost.Should().Be(158m);
        (await matSvc.GetByIdAsync(bulkShampoo.Id))!.CurrentStock.Should().Be(50m);
        (await matSvc.GetByIdAsync(bulkShampoo.Id))!.AverageCost.Should().Be(158m);
        var bulkBatch = (await batchSvc.GetBatchesAsync(bulkShampoo.Id)).Single();
        bulkBatch.BatchCode.Should().Be("LOT-BULK-50");
        bulkBatch.SourceProductionOrderId.Should().Be(opBulk.Id);

        // Etapa 2: Envasado en 8 oz y 16 oz consumiendo el granel en mL -> L
        var prod8 = await prodSvc.CreateAsync(new Product { Sku = "PT-8OZ", Name = "Shampoo 8 oz", Presentation = "8 oz", RetailPrice = 450m });
        var prod16 = await prodSvc.CreateAsync(new Product { Sku = "PT-16OZ", Name = "Shampoo 16 oz", Presentation = "16 oz", RetailPrice = 750m });

        var f8 = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod8.Id, Name = "Envasado 8 oz", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m, OverheadCostPerBatch = 100m,
            Items =
            [
                new() { RawMaterialId = bulkShampoo.Id, QuantityRequired = 2500m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = bottle8.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit }
            ]
        });
        var f16 = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod16.Id, Name = "Envasado 16 oz", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m, OverheadCostPerBatch = 150m,
            Items =
            [
                new() { RawMaterialId = bulkShampoo.Id, QuantityRequired = 5000m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = bottle16.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit }
            ]
        });

        var op8 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(f8.Id, 10, 0m, utcToday, utcToday.AddMonths(12), "LOT-PT-8OZ"));
        await orderSvc.StartProductionAsync(op8.Id);
        var done8 = await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(op8.Id, 10, 100m, null));
        done8.ActualUnitCost.Should().Be(69.5m); // (2.5*158 + 10*20 + 100) / 10

        var op16 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(f16.Id, 10, 0m, utcToday, utcToday.AddMonths(12), "LOT-PT-16OZ"));
        await orderSvc.StartProductionAsync(op16.Id);
        var done16 = await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(op16.Id, 10, 150m, null));
        done16.ActualUnitCost.Should().Be(129m); // (5*158 + 10*35 + 150) / 10

        (await matSvc.GetByIdAsync(bulkShampoo.Id))!.CurrentStock.Should().Be(42.5m);

        // Recall de 2 niveles desde LMP-B-OIL: muestra el lote granel Y los 2 productos terminados via granel
        var oilBatch = (await batchSvc.GetBatchesAsync(oilMat.Id)).Single();
        var recall = await batchSvc.GetBatchTraceabilityAsync(oilBatch.Id);
        recall!.AffectedOrders.Should().HaveCount(3);
        recall.AffectedOrders.Should().Contain(o => o.OrderNumber == opBulk.OrderNumber && o.IsSemiFinishedOutput && o.ViaBulkBatchCode == null);
        recall.AffectedOrders.Should().Contain(o => o.OrderNumber == op8.OrderNumber && o.ViaBulkBatchCode == "LOT-BULK-50" && o.ConsumedUnit == "L" && o.FinishedBatchRemaining == 10);
        recall.AffectedOrders.Should().Contain(o => o.OrderNumber == op16.OrderNumber && o.ViaBulkBatchCode == "LOT-BULK-50" && o.ConsumedUnit == "L" && o.FinishedBatchRemaining == 10);

        // Cuadre contable absoluto y WIP en cero
        var trial = await reportsSvc.GetTrialBalanceAsync(utcToday.AddDays(-1), utcToday.AddDays(1));
        trial.IsBalanced.Should().BeTrue();
        var wipRow = trial.Rows.Single(r => r.Code == AccountCodes.WorkInProgressInventory);
        wipRow.TotalDebit.Should().Be(wipRow.TotalCredit);
        var balance = await reportsSvc.GetBalanceSheetAsync(utcToday.AddDays(1));
        balance.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task SemiFinishedFormulas_RejectCircularDependenciesInvalidTargetsAndZeroYieldBulk()
    {
        using var factory = DbContextSqliteFactory.Create();
        await new ChartOfAccountsService(factory).SeedStandardChartAsync();

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var matSvc = new RawMaterialService(factory, accounting);
        var prodSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);
        var batchSvc = new RawMaterialBatchService(factory, accounting);

        var prod = await prodSvc.CreateAsync(new Product { Sku = "VAL-P", Name = "Prod Test", RetailPrice = 100m });
        var baseIng = await matSvc.CreateAsync(new RawMaterial { Sku = "VAL-ING", Name = "Ing", UnitOfMeasure = UnitOfMeasure.Liter, CurrentStock = 100m, AverageCost = 10m });
        var bulkA = await matSvc.CreateAsync(new RawMaterial { Sku = "VAL-A", Name = "Granel A", Category = RawMaterialCategory.SemiFinished, UnitOfMeasure = UnitOfMeasure.Liter });
        var bulkB = await matSvc.CreateAsync(new RawMaterial { Sku = "VAL-B", Name = "Granel B", Category = RawMaterialCategory.SemiFinished, UnitOfMeasure = UnitOfMeasure.Liter });

        Func<Task> bothTargets = () => formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod.Id, OutputRawMaterialId = bulkA.Id, Name = "Ambos", Version = "1.0", BaseYieldQuantity = 10,
            Items = [new() { RawMaterialId = baseIng.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Liter }]
        });
        await bothTargets.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> selfCycle = () => formulaSvc.CreateAsync(new ProductFormula
        {
            OutputRawMaterialId = bulkA.Id, Name = "Auto", Version = "1.0", BaseYieldQuantity = 10,
            Items = [new() { RawMaterialId = bulkA.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Liter }]
        });
        await selfCycle.Should().ThrowAsync<InvalidOperationException>();

        var fA = await formulaSvc.CreateAsync(new ProductFormula
        {
            OutputRawMaterialId = bulkA.Id, Name = "Formula A", Version = "1.0", BaseYieldQuantity = 10,
            Items = [new() { RawMaterialId = baseIng.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Liter }]
        });
        await formulaSvc.CreateAsync(new ProductFormula
        {
            OutputRawMaterialId = bulkB.Id, Name = "Formula B", Version = "1.0", BaseYieldQuantity = 10,
            Items = [new() { RawMaterialId = bulkA.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Liter }]
        });

        // Intentar que A consuma B ahora crea ciclo A -> B -> A
        var cloneA = await formulaSvc.CloneAsNewVersionAsync(fA.Id);
        cloneA.Items = [new() { RawMaterialId = bulkB.Id, QuantityRequired = 5m, UnitOfMeasure = UnitOfMeasure.Liter }];
        Func<Task> transitiveCycle = () => formulaSvc.UpdateAsync(cloneA);
        await transitiveCycle.Should().ThrowAsync<InvalidOperationException>();

        // Orden granel con yield 0 no genera lote MP y contabiliza pérdida anormal
        var opZero = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(fA.Id, 10, 0m, null, null, "LOT-BLK-ZERO"));
        await orderSvc.StartProductionAsync(opZero.Id);
        var zeroDone = await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(opZero.Id, 0, 50m, null));
        zeroDone.AbnormalLossCost.Should().Be(150m);
        (await batchSvc.GetBatchesAsync(bulkA.Id)).Should().BeEmpty();
    }
}
