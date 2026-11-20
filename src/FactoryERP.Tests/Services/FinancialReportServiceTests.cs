using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class FinancialReportServiceTests
{
    [Fact]
    public async Task Reports_ReflectCapitalPurchaseProductionSaleAndBalanceEquation()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var accounts = (await chart.GetAllAsync()).ToDictionary(a => a.Code, a => a.Id);

        var journal = new JournalEntryService(factory);
        var accounting = new ManufacturingAccountingService(factory);
        var reports = new FinancialReportService(factory);

        var now = DateTime.UtcNow;
        await journal.CreateAsync(new CreateJournalEntryRequest(
            now,
            "Capital inicial",
            "CAP-1",
            true,
            [
                new(accounts[AccountCodes.Cash], 100000m, 0m),
                new(accounts[AccountCodes.ShareCapital], 0m, 100000m)
            ]));

        await accounting.PostSaleAsync(new Sale
        {
            SaleNumber = "V-100",
            SaleDate = now,
            PaymentMethod = PaymentMethod.Cash,
            SubTotal = 10000m,
            DiscountAmount = 0m,
            TaxAmount = 1800m,
            Total = 11800m,
            TotalCogs = 4000m
        });

        await journal.RecordExpenseAsync(AccountCodes.OperatingExpenses, AccountCodes.Cash, 1500m, "Envíos locales", "EXP-1", now);

        var trial = await reports.GetTrialBalanceAsync(now.AddDays(-1), now.AddDays(1));
        var income = await reports.GetIncomeStatementAsync(now.AddDays(-1), now.AddDays(1));
        var balance = await reports.GetBalanceSheetAsync(now.AddDays(1));

        trial.IsBalanced.Should().BeTrue();
        trial.TotalDebit.Should().Be(trial.TotalCredit);

        income.Revenue.Should().Be(10000m);
        income.CostOfGoodsSold.Should().Be(4000m);
        income.GrossProfit.Should().Be(6000m);
        income.OperatingExpenses.Should().Be(1500m);
        income.NetIncome.Should().Be(4500m);

        balance.IsBalanced.Should().BeTrue();
        (balance.TotalLiabilities + balance.TotalEquity).Should().Be(balance.TotalAssets);
    }

    [Fact]
    public async Task Reports_CustomCostGroupAccount_ReflectsInGrossProfitAndPreservesBalanceSheet()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var freightAcc = await chart.CreateAsync(new AccountingAccount
        {
            Code = "5199",
            Name = "Fletes de Fábrica",
            Group = AccountGroup.Costos
        });
        var accounts = (await chart.GetAllAsync()).ToDictionary(a => a.Code, a => a.Id);

        var journal = new JournalEntryService(factory);
        var reports = new FinancialReportService(factory);
        var now = DateTime.UtcNow;

        await journal.CreateAsync(new CreateJournalEntryRequest(now, "Aporte capital", "CAP-C1", true,
            [new(accounts[AccountCodes.Cash], 20000m, 0m), new(accounts[AccountCodes.ShareCapital], 0m, 20000m)]));
        await journal.CreateAsync(new CreateJournalEntryRequest(now, "Flete de entrega", "FLT-1", true,
            [new(freightAcc.Id, 1200m, 0m), new(accounts[AccountCodes.Cash], 0m, 1200m)]));

        var income = await reports.GetIncomeStatementAsync(now.AddDays(-1), now.AddDays(1));
        var balance = await reports.GetBalanceSheetAsync(now.AddDays(1));

        income.GrossProfit.Should().Be(-1200m);
        income.NetIncome.Should().Be(-1200m);
        balance.IsBalanced.Should().BeTrue();
        balance.TotalAssets.Should().Be(18800m);
        (balance.TotalLiabilities + balance.TotalEquity).Should().Be(18800m);
    }
}
