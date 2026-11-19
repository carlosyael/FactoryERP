using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class RawMaterialServiceTests
{
    [Fact]
    public async Task CreateAndAdjustStock_PreventsDuplicateSkuAndNegativeStock()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new RawMaterialService(factory);

        var created = await service.CreateAsync(new RawMaterial
        {
            Sku = "MP-ARG-01",
            Name = "Aceite de Argán",
            Category = RawMaterialCategory.OilButter,
            UnitOfMeasure = UnitOfMeasure.Milliliter,
            CurrentStock = 1000m,
            MinimumStockLevel = 200m,
            AverageCost = 2.50m
        });

        created.Id.Should().BeGreaterThan(0);

        Func<Task> dupAct = () => service.CreateAsync(new RawMaterial
        {
            Sku = "MP-ARG-01",
            Name = "Duplicado",
            UnitOfMeasure = UnitOfMeasure.Milliliter
        });
        await dupAct.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> negAct = () => service.AdjustStockAsync(created.Id, -1m, "Inválido");
        await negAct.Should().ThrowAsync<ArgumentException>();

        await service.AdjustStockAsync(created.Id, 1250m, "Conteo físico");
        var updated = await service.GetByIdAsync(created.Id);
        updated!.CurrentStock.Should().Be(1250m);
    }
}
