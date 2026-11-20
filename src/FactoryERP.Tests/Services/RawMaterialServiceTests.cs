using FactoryERP.Core.Constants;
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
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var accounting = new ManufacturingAccountingService(factory);
        var service = new RawMaterialService(factory, accounting);

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

        Func<Task> emptyReasonAct = () => service.AdjustStockAsync(created.Id, 1100m, "   ");
        await emptyReasonAct.Should().ThrowAsync<ArgumentException>();

        await service.AdjustStockAsync(created.Id, 1250m, "Conteo físico positivo");
        await service.AdjustStockAsync(created.Id, 1150m, "Merma detectada en almacén");

        var updated = await service.GetByIdAsync(created.Id);
        updated!.CurrentStock.Should().Be(1150m);

        var journal = new JournalEntryService(factory);
        var entries = await journal.GetAllAsync();
        entries.Should().HaveCount(2);
        entries.All(e => e.Lines.Sum(l => l.Debit) == e.Lines.Sum(l => l.Credit)).Should().BeTrue();
        entries[1].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.RawMaterialInventory && l.Debit == 625m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.AbnormalProductionLoss && l.Debit == 250m);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateSkuOnAnotherMaterial_ThrowsInvalidOperationException()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new RawMaterialService(factory);

        var m1 = await service.CreateAsync(new RawMaterial { Sku = "MP-001", Name = "Material 1" });
        var m2 = await service.CreateAsync(new RawMaterial { Sku = "MP-002", Name = "Material 2" });

        m2.Sku = m1.Sku;
        Func<Task> act = () => service.UpdateAsync(m2);
        await act.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> emptyCreate = () => service.CreateAsync(new RawMaterial { Sku = "  ", Name = "Sin SKU" });
        await emptyCreate.Should().ThrowAsync<ArgumentException>();

        m2.Sku = "   ";
        Func<Task> emptyUpdate = () => service.UpdateAsync(m2);
        await emptyUpdate.Should().ThrowAsync<ArgumentException>();
    }
}
