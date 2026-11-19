using FactoryERP.Core.Constants;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class JournalEntryServiceTests
{
    [Fact]
    public async Task CreateAsync_UnbalancedEntry_ThrowsInvalidOperationException()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var cash = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.Cash);
        var capital = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.ShareCapital);

        var journal = new JournalEntryService(factory);
        Func<Task> act = () => journal.CreateAsync(new CreateJournalEntryRequest(
            DateTime.UtcNow,
            "Asiento descuadrado",
            "REF-1",
            true,
            [
                new(cash.Id, 1000m, 0m),
                new(capital.Id, 0m, 900m)
            ]));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ControlAccount_ThrowsInvalidOperationException()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var control = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.CurrentAssetsControl);
        var capital = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.ShareCapital);

        var journal = new JournalEntryService(factory);
        Func<Task> act = () => journal.CreateAsync(new CreateJournalEntryRequest(
            DateTime.UtcNow,
            "Uso indebido cuenta control",
            "REF-2",
            true,
            [
                new(control.Id, 500m, 0m),
                new(capital.Id, 0m, 500m)
            ]));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreatePostAndVoid_BalancedEntry_UpdatesStatusCorrectly()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var cash = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.Cash);
        var capital = (await chart.GetAllAsync()).First(a => a.Code == AccountCodes.ShareCapital);

        var journal = new JournalEntryService(factory);
        var draft = await journal.CreateAsync(new CreateJournalEntryRequest(
            DateTime.UtcNow,
            "Aporte inicial",
            "CAP-01",
            false,
            [
                new(cash.Id, 50000m, 0m),
                new(capital.Id, 0m, 50000m)
            ]));

        draft.Status.Should().Be(JournalEntryStatus.Draft);
        await journal.PostAsync(draft.Id);
        var posted = await journal.GetByIdAsync(draft.Id);
        posted!.Status.Should().Be(JournalEntryStatus.Posted);

        await journal.VoidAsync(draft.Id);
        var voided = await journal.GetByIdAsync(draft.Id);
        voided!.Status.Should().Be(JournalEntryStatus.Voided);
    }
}
