using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Models;

public sealed record TrialBalanceRowDto(
    int AccountId,
    string Code,
    string Name,
    AccountGroup Group,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record TrialBalanceDto(
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal TotalDebitBalance,
    decimal TotalCreditBalance,
    bool IsBalanced,
    IReadOnlyList<TrialBalanceRowDto> Rows);

public sealed record FinancialLineDto(
    string Code,
    string Name,
    decimal Amount);

public sealed record IncomeStatementDto(
    DateTime StartDate,
    DateTime EndDate,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal AbnormalLossCost,
    decimal GrossProfit,
    decimal OperatingExpenses,
    decimal NetIncome,
    IReadOnlyList<FinancialLineDto> RevenueLines,
    IReadOnlyList<FinancialLineDto> CostLines,
    IReadOnlyList<FinancialLineDto> ExpenseLines);

public sealed record BalanceSheetDto(
    DateTime AsOfDate,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal CurrentPeriodEarnings,
    bool IsBalanced,
    IReadOnlyList<FinancialLineDto> AssetLines,
    IReadOnlyList<FinancialLineDto> LiabilityLines,
    IReadOnlyList<FinancialLineDto> EquityLines);

public sealed record CreateJournalEntryLineRequest(
    int AccountId,
    decimal Debit,
    decimal Credit,
    string Description = "");

public sealed record CreateJournalEntryRequest(
    DateTime Date,
    string Concept,
    string Reference,
    bool PostImmediately,
    IReadOnlyList<CreateJournalEntryLineRequest> Lines);
