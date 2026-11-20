using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class ChartOfAccountsServiceTests
{
    [Fact]
    public async Task SeedStandardChartAsync_SeedsIndustrialAccountsIdempotently()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new ChartOfAccountsService(factory);

        int firstSeed = await service.SeedStandardChartAsync();
        int secondSeed = await service.SeedStandardChartAsync();
        var accounts = await service.GetAllAsync();

        firstSeed.Should().BeGreaterThan(15);
        secondSeed.Should().Be(0);
        accounts.Select(a => a.Code).Should().Contain([
            AccountCodes.RawMaterialInventory,
            AccountCodes.WorkInProgressInventory,
            AccountCodes.FinishedGoodsInventory,
            AccountCodes.AbnormalProductionLoss,
            AccountCodes.ProductSalesRevenue,
            AccountCodes.CostOfGoodsSold
        ]);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ThrowsInvalidOperationException()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new ChartOfAccountsService(factory);
        await service.SeedStandardChartAsync();

        Func<Task> act = () => service.CreateAsync(new AccountingAccount
        {
            Code = AccountCodes.Cash,
            Name = "Caja Duplicada",
            Group = AccountGroup.Activo
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAndUpdate_RejectsEmptyCodeOrName_AndUpdatesNameAndStatus()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new ChartOfAccountsService(factory);

        Func<Task> emptyCodeCreate = () => service.CreateAsync(new AccountingAccount { Code = "  ", Name = "Válida", Group = AccountGroup.Gastos });
        await emptyCodeCreate.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyNameCreate = () => service.CreateAsync(new AccountingAccount { Code = "6199", Name = " ", Group = AccountGroup.Gastos });
        await emptyNameCreate.Should().ThrowAsync<ArgumentException>();

        var created = await service.CreateAsync(new AccountingAccount { Code = "6199", Name = "Gasto Misceláneo", Group = AccountGroup.Gastos });
        created.Name = "Gastos Generales Planta";
        created.IsActive = false;
        await service.UpdateAsync(created);

        var reloaded = await service.GetByIdAsync(created.Id);
        reloaded!.Name.Should().Be("Gastos Generales Planta");
        reloaded.IsActive.Should().BeFalse();

        created.Name = "";
        Func<Task> emptyNameUpdate = () => service.UpdateAsync(created);
        await emptyNameUpdate.Should().ThrowAsync<ArgumentException>();
    }
}
