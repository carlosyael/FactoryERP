using FactoryERP.Core.Constants;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class FinancialReportService : IFinancialReportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public FinancialReportService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        DateTime from = startDate.Date;
        DateTime to = endDate.Date.AddDays(1).AddTicks(-1);

        var accounts = await context.AccountingAccounts
            .AsNoTracking()
            .Where(a => !a.IsControlAccount)
            .OrderBy(a => a.Code)
            .ToListAsync();

        var totals = await context.JournalEntryLines
            .AsNoTracking()
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted &&
                        l.JournalEntry.Date >= from &&
                        l.JournalEntry.Date <= to)
            .GroupBy(l => l.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToDictionaryAsync(x => x.AccountId);

        var rows = new List<TrialBalanceRowDto>();
        foreach (var acc in accounts)
        {
            decimal debit = totals.TryGetValue(acc.Id, out var t) ? t.Debit : 0m;
            decimal credit = totals.TryGetValue(acc.Id, out var t2) ? t2.Credit : 0m;
            if (debit == 0m && credit == 0m) continue;

            decimal net = debit - credit;
            rows.Add(new TrialBalanceRowDto(
                acc.Id,
                acc.Code,
                acc.Name,
                acc.Group,
                debit,
                credit,
                net > 0m ? net : 0m,
                net < 0m ? Math.Abs(net) : 0m));
        }

        decimal sumDebit = rows.Sum(r => r.TotalDebit);
        decimal sumCredit = rows.Sum(r => r.TotalCredit);
        decimal sumDbBal = rows.Sum(r => r.DebitBalance);
        decimal sumCrBal = rows.Sum(r => r.CreditBalance);

        return new TrialBalanceDto(
            from,
            to,
            sumDebit,
            sumCredit,
            sumDbBal,
            sumCrBal,
            sumDebit == sumCredit && sumDbBal == sumCrBal,
            rows);
    }

    public async Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime startDate, DateTime endDate)
    {
        var trial = await GetTrialBalanceAsync(startDate, endDate);

        var revenueLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Ingresos)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalCredit - r.TotalDebit))
            .Where(l => l.Amount != 0m)
            .ToList();

        var costLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Costos)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalDebit - r.TotalCredit))
            .Where(l => l.Amount != 0m)
            .ToList();

        var expenseLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Gastos)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalDebit - r.TotalCredit))
            .Where(l => l.Amount != 0m)
            .ToList();

        decimal revenue = revenueLines.Sum(l => l.Amount);
        decimal cogs = costLines.Where(l => l.Code == AccountCodes.CostOfGoodsSold).Sum(l => l.Amount);
        decimal abnormalLoss = costLines.Where(l => l.Code == AccountCodes.AbnormalProductionLoss).Sum(l => l.Amount);
        decimal grossProfit = revenue - costLines.Sum(l => l.Amount);
        decimal operatingExpenses = expenseLines.Sum(l => l.Amount);
        decimal netIncome = grossProfit - operatingExpenses;

        return new IncomeStatementDto(
            startDate.Date,
            endDate.Date,
            revenue,
            cogs,
            abnormalLoss,
            grossProfit,
            operatingExpenses,
            netIncome,
            revenueLines,
            costLines,
            expenseLines);
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate)
    {
        DateTime start = new(2000, 1, 1);
        var trial = await GetTrialBalanceAsync(start, asOfDate);
        var income = await GetIncomeStatementAsync(start, asOfDate);

        var assetLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Activo)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalDebit - r.TotalCredit))
            .Where(l => l.Amount != 0m)
            .ToList();

        var liabilityLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Pasivo)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalCredit - r.TotalDebit))
            .Where(l => l.Amount != 0m)
            .ToList();

        var equityLines = trial.Rows
            .Where(r => r.Group == AccountGroup.Capital)
            .Select(r => new FinancialLineDto(r.Code, r.Name, r.TotalCredit - r.TotalDebit))
            .Where(l => l.Amount != 0m)
            .ToList();

        decimal assets = assetLines.Sum(l => l.Amount);
        decimal liabilities = liabilityLines.Sum(l => l.Amount);
        decimal equity = equityLines.Sum(l => l.Amount) + income.NetIncome;

        return new BalanceSheetDto(
            asOfDate.Date,
            assets,
            liabilities,
            equity,
            income.NetIncome,
            assets == (liabilities + equity),
            assetLines,
            liabilityLines,
            equityLines);
    }
}
