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
}
