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

    [Fact]
    public async Task CreateAndUpdate_ValidatesBoundariesDuplicatesAndUpdatesItems()
    {
        using var factory = DbContextSqliteFactory.Create();
        var productService = new ProductService(factory);
        var materialService = new RawMaterialService(factory);
        var formulaService = new FormulaService(factory);

        var product = await productService.CreateAsync(new Product { Sku = "F-CAP-01", Name = "Acondicionador", RetailPrice = 600m });
        var mat1 = await materialService.CreateAsync(new RawMaterial { Sku = "F-MP-1", Name = "Base", UnitOfMeasure = UnitOfMeasure.Gram, AverageCost = 0.5m });
        var mat2 = await materialService.CreateAsync(new RawMaterial { Sku = "F-MP-2", Name = "Aceite", UnitOfMeasure = UnitOfMeasure.Milliliter, AverageCost = 1m });

        Func<Task> badLoss = () => formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Bad Loss", BaseYieldQuantity = 10, StandardLossMarginPercent = 100m,
            Items = [new() { RawMaterialId = mat1.Id, QuantityRequired = 100m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });
        await badLoss.Should().ThrowAsync<ArgumentException>();

        Func<Task> badOverhead = () => formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Bad CIF", BaseYieldQuantity = 10, OverheadCostPerBatch = -5m,
            Items = [new() { RawMaterialId = mat1.Id, QuantityRequired = 100m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });
        await badOverhead.Should().ThrowAsync<ArgumentException>();

        Func<Task> dupMaterials = () => formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Dup Mat", BaseYieldQuantity = 10,
            Items = [
                new() { RawMaterialId = mat1.Id, QuantityRequired = 100m, UnitOfMeasure = UnitOfMeasure.Gram },
                new() { RawMaterialId = mat1.Id, QuantityRequired = 50m, UnitOfMeasure = UnitOfMeasure.Gram }
            ]
        });
        await dupMaterials.Should().ThrowAsync<InvalidOperationException>();

        var created = await formulaService.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Fórmula v1", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 5m,
            Items = [new() { RawMaterialId = mat1.Id, QuantityRequired = 1000m, UnitOfMeasure = UnitOfMeasure.Gram }]
        });

        created.Version = "1.1";
        created.Items = [
            new() { RawMaterialId = mat1.Id, QuantityRequired = 900m, UnitOfMeasure = UnitOfMeasure.Gram },
            new() { RawMaterialId = mat2.Id, QuantityRequired = 100m, UnitOfMeasure = UnitOfMeasure.Milliliter }
        ];
        await formulaService.UpdateAsync(created);

        var updated = await formulaService.GetByIdAsync(created.Id);
        updated!.Version.Should().Be("1.1");
        updated.Items.Should().HaveCount(2);
        (await formulaService.CalculateBaseMaterialCostAsync(created.Id)).Should().Be(550m);
    }
}
