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
}
