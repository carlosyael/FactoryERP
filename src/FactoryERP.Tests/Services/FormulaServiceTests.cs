using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class FormulaServiceTests
{
    [Fact]
    public async Task CreateAndCalculateCost_ConvertsUnitsAndRejectsIncompatibleUnits()
    {
        using var factory = DbContextSqliteFactory.Create();
        var productService = new ProductService(factory);
        var materialService = new RawMaterialService(factory);
        var formulaService = new FormulaService(factory);

        var product = await productService.CreateAsync(new Product
        {
            Sku = "CAP-SH-16",
            Name = "Shampoo Hidratante",
            RetailPrice = 750m
        });

        var baseMat = await materialService.CreateAsync(new RawMaterial
        {
            Sku = "MP-BASE-KG",
            Name = "Base Shampoo",
            UnitOfMeasure = UnitOfMeasure.Kilogram,
            CurrentStock = 20m,
            AverageCost = 400m // 400 por kg = 0.40 por gramo
        });

        var bottle = await materialService.CreateAsync(new RawMaterial
        {
            Sku = "EMP-BOT-16",
            Name = "Botella 16 oz",
            UnitOfMeasure = UnitOfMeasure.Unit,
            CurrentStock = 100m,
            AverageCost = 35m
        });

        // Incompatible unit test (Gram on a Unit bottle)
        Func<Task> badAct = () => formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id,
            Name = "Inválida",
            BaseYieldQuantity = 10,
            Items = [new() { RawMaterialId = bottle.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });
        await badAct.Should().ThrowAsync<InvalidOperationException>();

        var formula = await formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id,
            Name = "Fórmula Base Shampoo",
            Version = "1.0",
            BaseYieldQuantity = 10,
            StandardLossMarginPercent = 5m,
            OverheadCostPerBatch = 200m,
            Items =
            [
                new() { RawMaterialId = baseMat.Id, QuantityRequired = 4000m, UnitOfMeasure = UnitOfMeasure.Gram }, // 4 kg * 400 = 1,600
                new() { RawMaterialId = bottle.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit } // 10 * 35 = 350
            ]
        });

        decimal baseCost = await formulaService.CalculateBaseMaterialCostAsync(formula.Id);
        baseCost.Should().Be(1950m);
    }
}
