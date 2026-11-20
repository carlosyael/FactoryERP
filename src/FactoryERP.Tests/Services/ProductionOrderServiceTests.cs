using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class ProductionOrderServiceTests
{
    private static async Task<(
        Product Product,
        ProductFormula Formula,
        RawMaterial BaseMat,
        RawMaterial BottleMat,
        ProductionOrderService Service,
        RawMaterialService MaterialService,
        ProductService ProductService,
        JournalEntryService JournalService)> SetupAsync(DbContextSqliteFactory factory, decimal baseStockKg = 10m, decimal bottleStock = 50m)
    {
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var settings = new SystemSettingService(factory, cache);
        var accounting = new ManufacturingAccountingService(factory);
        var materialService = new RawMaterialService(factory);
        var productService = new ProductService(factory);
        var formulaService = new FormulaService(factory);
        var prodService = new ProductionOrderService(factory, settings, accounting);
        var journalService = new JournalEntryService(factory);

        var product = await productService.CreateAsync(new Product
        {
            Sku = "CAP-MAS-01",
            Name = "Mascarilla Keratina 16 oz",
            RetailPrice = 900m,
            Cost = 0m
        });

        var baseMat = await materialService.CreateAsync(new RawMaterial
        {
            Sku = "MP-BASE-01",
            Name = "Base Capilar",
            UnitOfMeasure = UnitOfMeasure.Kilogram,
            CurrentStock = baseStockKg,
            AverageCost = 500m
        });

        var bottleMat = await materialService.CreateAsync(new RawMaterial
        {
            Sku = "EMP-ENV-01",
            Name = "Envase 16 oz",
            UnitOfMeasure = UnitOfMeasure.Unit,
            CurrentStock = bottleStock,
            AverageCost = 40m
        });

        var formula = await formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id,
            Name = "Mascarilla v1",
            Version = "1.0",
            BaseYieldQuantity = 10,
            StandardLossMarginPercent = 5m,
            OverheadCostPerBatch = 200m,
            Items =
            [
                new() { RawMaterialId = baseMat.Id, QuantityRequired = 4000m, UnitOfMeasure = UnitOfMeasure.Gram }, // 4 kg por 10 uds => 0.4 kg/ud = 200/ud
                new() { RawMaterialId = bottleMat.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit } // 1 ud por ud = 40/ud
            ]
        });

        return (product, formula, baseMat, bottleMat, prodService, materialService, productService, journalService);
    }

    [Fact]
    public async Task SimulateStartAndComplete_ComputesLossYieldBatchCostAndAccounting()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, formula, baseMat, bottleMat, prodService, matSvc, prodSvc, journalSvc) = await SetupAsync(factory);

        // Simular 20 unidades (requiere 8 kg base = 4,000 + 20 envases = 800; overhead escalado = 400 => total = 5,200)
        var sim = await prodService.SimulateAsync(formula.Id, 20, overrideLossMarginPercent: 10m);
        sim.CanProduce.Should().BeTrue();
        sim.ExpectedLossMarginPercent.Should().Be(10m);
        sim.ExpectedResultingQuantity.Should().Be(18);
        sim.EstimatedMaterialCost.Should().Be(4800m);
        sim.EstimatedOverheadCost.Should().Be(400m);
        sim.EstimatedTotalCost.Should().Be(5200m);

        var draft = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 20, 10m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(18), "LOT-2026-01", "Lote prueba"));

        var inProgress = await prodService.StartProductionAsync(draft.Id);
        inProgress.Status.Should().Be(ProductionOrderStatus.InProgress);

        var matAfterStart = await matSvc.GetByIdAsync(baseMat.Id);
        var botAfterStart = await matSvc.GetByIdAsync(bottleMat.Id);
        matAfterStart!.CurrentStock.Should().Be(2m); // 10 - 8
        botAfterStart!.CurrentStock.Should().Be(30m); // 50 - 20

        // Cerrar orden con 15 unidades reales => 5 perdidas = 25% > (10% + 2% = 12%) => 13% merma anormal
        var completed = await prodService.CompleteProductionAsync(new CompleteProductionRequest(
            draft.Id,
            ActualYieldQuantity: 15,
            ActualOverheadCost: 400m,
            ExpirationDate: DateTime.UtcNow.AddMonths(18)));

        completed.Status.Should().Be(ProductionOrderStatus.Completed);
        completed.ActualYieldQuantity.Should().Be(15);
        completed.ActualLossQuantity.Should().Be(5);
        completed.ActualLossPercent.Should().Be(25m);
        completed.AbnormalLossCost.Should().Be(676m); // 5200 * 0.13
        completed.CapitalizedInventoryCost.Should().Be(4524m); // 5200 - 676
        completed.ActualUnitCost.Should().Be(301.6m); // 4524 / 15

        var batches = await prodSvc.GetBatchesAsync(product.Id);
        batches.Should().ContainSingle(b => b.BatchCode == "LOT-2026-01" && b.QuantityRemaining == 15);

        var refreshedProduct = await prodSvc.GetByIdAsync(product.Id);
        refreshedProduct!.Cost.Should().Be(301.6m);

        var entries = await journalSvc.GetAllAsync();
        entries.Should().HaveCount(2); // Start + Complete
        entries.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
        entries.SelectMany(e => e.Lines).Should().Contain(l => l.Account!.Code == AccountCodes.AbnormalProductionLoss && l.Debit == 676m);
    }

    [Fact]
    public async Task StartProduction_InsufficientStock_ThrowsWithoutMutatingInventory()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (_, formula, baseMat, _, prodService, matSvc, _, _) = await SetupAsync(factory, baseStockKg: 2m);

        var sim = await prodService.SimulateAsync(formula.Id, 10);
        sim.CanProduce.Should().BeFalse();

        var draft = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, null, null, null, "LOT-FAIL"));
        Func<Task> act = () => prodService.StartProductionAsync(draft.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        var matAfter = await matSvc.GetByIdAsync(baseMat.Id);
        matAfter!.CurrentStock.Should().Be(2m);
    }

    [Fact]
    public async Task CancelInProgressProduction_RestoresRawMaterialStockAndPostsWipReversal()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (_, formula, baseMat, bottleMat, prodService, matSvc, _, journalSvc) = await SetupAsync(factory);

        var draft = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, null, null, null, "LOT-CAN"));
        await prodService.StartProductionAsync(draft.Id);
        var cancelled = await prodService.CancelProductionAsync(draft.Id, "Cancelación de prueba");

        cancelled.Status.Should().Be(ProductionOrderStatus.Cancelled);
        (await matSvc.GetByIdAsync(baseMat.Id))!.CurrentStock.Should().Be(10m);
        (await matSvc.GetByIdAsync(bottleMat.Id))!.CurrentStock.Should().Be(50m);

        var entries = await journalSvc.GetAllAsync();
        entries.Should().HaveCount(2); // Start + Reversal
    }

    [Fact]
    public async Task ConcurrentStartProduction_CompetesForLimitedMaterials_OnlyOneSucceedsAndNoNegativeStock()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (_, formula, baseMat, _, prodService, matSvc, _, _) = await SetupAsync(factory, baseStockKg: 5m, bottleStock: 30m);

        // Cada orden requiere 4 kg; con 5 kg disponibles solo puede arrancar 1 orden.
        var o1 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, null, null, null, "LOT-C1"));
        var o2 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, null, null, null, "LOT-C2"));

        var tasks = new[]
        {
            Task.Run(async () => { try { await prodService.StartProductionAsync(o1.Id); return true; } catch (InvalidOperationException) { return false; } }),
            Task.Run(async () => { try { await prodService.StartProductionAsync(o2.Id); return true; } catch (InvalidOperationException) { return false; } })
        };

        bool[] outcomes = await Task.WhenAll(tasks);
        outcomes.Count(x => x).Should().Be(1);
        (await matSvc.GetByIdAsync(baseMat.Id))!.CurrentStock.Should().Be(1m);
    }

    [Fact]
    public async Task ProductionStateTransitions_DuplicateBatchZeroYieldAndDraftCancel_WorkStrictly()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, formula, _, _, prodService, _, prodSvc, journalSvc) = await SetupAsync(factory);

        var draft1 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, 5m, null, null, "LOT-UNIQ-1"));

        Func<Task> dupBatch = () => prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, 5m, null, null, "LOT-UNIQ-1"));
        await dupBatch.Should().ThrowAsync<InvalidOperationException>();

        // Completar borrador antes de iniciar debe fallar
        Func<Task> completeDraft = () => prodService.CompleteProductionAsync(new CompleteProductionRequest(draft1.Id, 9, null, null));
        await completeDraft.Should().ThrowAsync<InvalidOperationException>();

        // Cancelar borrador no genera asiento contable
        var cancelledDraft = await prodService.CancelProductionAsync(draft1.Id, "No requerida");
        cancelledDraft.Status.Should().Be(ProductionOrderStatus.Cancelled);
        (await journalSvc.GetAllAsync()).Should().BeEmpty();

        // Iniciar orden ya cancelada debe fallar
        Func<Task> startCancelled = () => prodService.StartProductionAsync(draft1.Id);
        await startCancelled.Should().ThrowAsync<InvalidOperationException>();

        // Cierre con 0 unidades resultantes => 100% pérdida anormal y 0 lotes creados
        var draft2 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, 5m, null, null, "LOT-FAIL-0"));
        await prodService.StartProductionAsync(draft2.Id);
        var zeroYield = await prodService.CompleteProductionAsync(new CompleteProductionRequest(draft2.Id, 0, 200m, null));

        zeroYield.Status.Should().Be(ProductionOrderStatus.Completed);
        zeroYield.ActualYieldQuantity.Should().Be(0);
        zeroYield.AbnormalLossCost.Should().Be(zeroYield.ActualMaterialCost + 200m);
        zeroYield.CapitalizedInventoryCost.Should().Be(0m);
        (await prodSvc.GetBatchesAsync(product.Id)).Should().BeEmpty();

        // Cancelar orden completada debe fallar
        Func<Task> cancelCompleted = () => prodService.CancelProductionAsync(draft2.Id, "Tarde");
        await cancelCompleted.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ProductionLifecycle_ZeroCostMaterialsAndOverhead_DoesNotFailPostCommitAccounting()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var matSvc = new RawMaterialService(factory);
        var prodSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var orderSvc = new ProductionOrderService(factory, settings, accounting);

        var prod = await prodSvc.CreateAsync(new Product { Sku = "ZERO-P1", Name = "Muestra Capilar", RetailPrice = 100m });
        var mat = await matSvc.CreateAsync(new RawMaterial { Sku = "ZERO-M1", Name = "Muestra Base", UnitOfMeasure = UnitOfMeasure.Gram, CurrentStock = 500m, AverageCost = 0m });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod.Id,
            Name = "Receta Muestra",
            Version = "1.0",
            BaseYieldQuantity = 5,
            StandardLossMarginPercent = 0m,
            OverheadCostPerBatch = 0m,
            Items = [new() { RawMaterialId = mat.Id, QuantityRequired = 100m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });

        var o1 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 5, 0m, null, null, "LOT-Z-1"));
        await orderSvc.StartProductionAsync(o1.Id);
        var completed = await orderSvc.CompleteProductionAsync(new CompleteProductionRequest(o1.Id, 5, 0m, null));
        completed.Status.Should().Be(ProductionOrderStatus.Completed);

        var o2 = await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 5, 0m, null, null, "LOT-Z-2"));
        await orderSvc.StartProductionAsync(o2.Id);
        var cancelled = await orderSvc.CancelProductionAsync(o2.Id, "Prueba costo cero");
        cancelled.Status.Should().Be(ProductionOrderStatus.Cancelled);
        (await matSvc.GetByIdAsync(mat.Id))!.CurrentStock.Should().Be(400m);
    }

    [Fact]
    public async Task CompleteProduction_WithActualMaterialQuantities_ReconcilesStockCostAndWip()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (product, formula, baseMat, bottleMat, prodService, matSvc, _, journalSvc) =
            await SetupAsync(factory, baseStockKg: 20m, bottleStock: 50m);

        // Orden 1: consumo extra de base (+0.5 kg = +250) y devolucion de envases (-2 = -80) => +170 neto
        var draft1 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 0m, null, null, "LOT-ACT-1"));
        await prodService.StartProductionAsync(draft1.Id);

        var actuals1 = new Dictionary<int, decimal> { [baseMat.Id] = 4.5m, [bottleMat.Id] = 8m };
        var comp1 = await prodService.CompleteProductionAsync(new CompleteProductionRequest(
            draft1.Id, 10, 200m, null, "Cierre ajustado", actuals1));

        comp1.ActualMaterialCost.Should().Be(2570m);
        comp1.ActualUnitCost.Should().Be(277m);
        (await matSvc.GetByIdAsync(baseMat.Id))!.CurrentStock.Should().Be(15.5m);
        (await matSvc.GetByIdAsync(bottleMat.Id))!.CurrentStock.Should().Be(42m);

        var entries1 = await journalSvc.GetAllAsync();
        entries1.Should().HaveCount(2);
        entries1.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
        var wipNet1 = entries1.SelectMany(e => e.Lines)
            .Where(l => l.Account!.Code == AccountCodes.WorkInProgressInventory)
            .Sum(l => l.Debit - l.Credit);
        wipNet1.Should().Be(0m);
        entries1[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.RawMaterialInventory && l.Credit == 170m);

        // Orden 2: devolucion neta de materiales (-1 kg base = -500) y bloqueo si pide mas del stock disponible
        var draft2 = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 0m, null, null, "LOT-ACT-2"));
        await prodService.StartProductionAsync(draft2.Id);

        Func<Task> overConsume = () => prodService.CompleteProductionAsync(new CompleteProductionRequest(
            draft2.Id, 10, 200m, null, null, new Dictionary<int, decimal> { [baseMat.Id] = 100m }));
        await overConsume.Should().ThrowAsync<InvalidOperationException>();

        var comp2 = await prodService.CompleteProductionAsync(new CompleteProductionRequest(
            draft2.Id, 10, 200m, null, null, new Dictionary<int, decimal> { [baseMat.Id] = 3m }));
        comp2.ActualMaterialCost.Should().Be(1900m);
        (await matSvc.GetByIdAsync(baseMat.Id))!.CurrentStock.Should().Be(12.5m);

        var entries2 = await journalSvc.GetAllAsync();
        entries2[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.RawMaterialInventory && l.Debit == 500m);
        entries2.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateDraft_AndInactiveFormulaOrMaterial_BehaveStrictly()
    {
        using var factory = DbContextSqliteFactory.Create();
        var (_, formula, baseMat, _, prodService, matSvc, _, _) = await SetupAsync(factory);
        var formulaSvc = new FormulaService(factory);

        var draft = await prodService.CreateDraftAsync(new CreateProductionOrderRequest(
            formula.Id, 10, 5m, null, null, "LOT-UPD-1", "Inicial"));

        var updated = await prodService.UpdateDraftAsync(new UpdateProductionOrderRequest(
            draft.Id, formula.Id, 20, 10m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(12), "LOT-UPD-2", "Actualizado"));

        updated.PlannedQuantity.Should().Be(20);
        updated.ExpectedResultingQuantity.Should().Be(18);
        updated.BatchCode.Should().Be("LOT-UPD-2");
        updated.Notes.Should().Be("Actualizado");
        updated.Materials.Should().Contain(m => m.RawMaterialId == baseMat.Id && m.PlannedQuantity == 8m);

        await prodService.StartProductionAsync(draft.Id);
        Func<Task> editStarted = () => prodService.UpdateDraftAsync(new UpdateProductionOrderRequest(
            draft.Id, formula.Id, 10, 5m, null, null, "LOT-UPD-2"));
        await editStarted.Should().ThrowAsync<InvalidOperationException>();

        baseMat.IsActive = false;
        await matSvc.UpdateAsync(baseMat);
        Func<Task> inactiveMatSim = () => prodService.SimulateAsync(formula.Id, 10);
        await inactiveMatSim.Should().ThrowAsync<InvalidOperationException>();

        baseMat.IsActive = true;
        await matSvc.UpdateAsync(baseMat);
        formula.IsActive = false;
        await formulaSvc.UpdateAsync(formula);
        Func<Task> inactiveFormulaSim = () => prodService.SimulateAsync(formula.Id, 10);
        await inactiveFormulaSim.Should().ThrowAsync<InvalidOperationException>();
    }
}
