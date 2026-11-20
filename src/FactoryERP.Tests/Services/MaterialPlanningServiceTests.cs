using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class MaterialPlanningServiceTests
{
    [Fact]
    public async Task PurchaseSuggestions_AndFormulaShortages_AccountForExpiredStockDraftReservationsAndMinimums()
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
        var planningSvc = new MaterialPlanningService(factory);

        var supplier = await supSvc.CreateAsync(new Supplier { Name = "Química MRP" });
        var matOil = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "MRP-OIL", Name = "Aceite Coco", UnitOfMeasure = UnitOfMeasure.Milliliter,
            MinimumStockLevel = 200m, AverageCost = 2m
        });
        var matBottle = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "MRP-BOT", Name = "Botella", UnitOfMeasure = UnitOfMeasure.Unit,
            MinimumStockLevel = 5m, AverageCost = 30m
        });
        var matBulk = await matSvc.CreateAsync(new RawMaterial
        {
            Sku = "MRP-BULK", Name = "Granel Shampoo", Category = RawMaterialCategory.SemiFinished,
            UnitOfMeasure = UnitOfMeasure.Liter, MinimumStockLevel = 10m
        });

        var utcToday = DateTime.UtcNow.Date;
        // matOil: 500 vencidos + 300 vigentes (costo 2.50 en ultima compra)
        await poSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday.AddDays(-10), "Lote viejo",
            [new(matOil.Id, 500m, 2m, false, "LMP-MRP-EXP", utcToday.AddDays(-1))]));
        await poSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Cash, utcToday, "Lote vigente",
            [new(matOil.Id, 300m, 2.50m, false, "LMP-MRP-OK", utcToday.AddDays(60)),
             new(matBottle.Id, 50m, 30m, false, "LMP-BOT-OK", null)]));

        var prod = await prodSvc.CreateAsync(new Product { Sku = "MRP-PT", Name = "Shampoo Coco", RetailPrice = 800m });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = prod.Id, Name = "Shampoo Coco v1", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 0m,
            Items =
            [
                new() { RawMaterialId = matOil.Id, QuantityRequired = 400m, UnitOfMeasure = UnitOfMeasure.Milliliter },
                new() { RawMaterialId = matBottle.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit }
            ]
        });

        // Borrador reserva 400 ml de matOil y 10 botellas -> target matOil = 400 + 200 min = 600 vs 300 usable => faltan 300 ml
        await orderSvc.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 10, 0m, utcToday, utcToday.AddMonths(12), "LOT-MRP-DR1"));

        var suggestions = await planningSvc.GetPurchaseSuggestionsAsync(onlyWithShortage: true, excludeManufacturedInHouse: true);
        suggestions.Should().ContainSingle(s => s.RawMaterialId == matOil.Id);
        var oilSug = suggestions.Single();
        oilSug.PhysicalStock.Should().Be(800m);
        oilSug.ExpiredStock.Should().Be(500m);
        oilSug.UsableStock.Should().Be(300m);
        oilSug.ReservedByDrafts.Should().Be(400m);
        oilSug.NetAvailableStock.Should().Be(-100m);
        oilSug.SuggestedPurchaseQuantity.Should().Be(300m);
        oilSug.ReferenceUnitCost.Should().Be(2.50m);
        oilSug.EstimatedPurchaseSubTotal.Should().Be(750m);
        suggestions.Should().NotContain(s => s.RawMaterialId == matBulk.Id);

        // Si se pide simular 20 uds (requiere 800 ml oil y 20 botellas), GetShortagesForFormulaAsync sugiere solo 500 ml de aceite
        var sim20 = await orderSvc.SimulateAsync(formula.Id, 20);
        sim20.CanProduce.Should().BeFalse();
        sim20.Materials.Single(m => m.RawMaterialId == matOil.Id).ReservedByDrafts.Should().Be(400m);

        var formulaShortages = await planningSvc.GetShortagesForFormulaAsync(formula.Id, 20);
        formulaShortages.Should().ContainSingle(s => s.RawMaterialId == matOil.Id && s.SuggestedPurchaseQuantity == 500m);
    }
}
