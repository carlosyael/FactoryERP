using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IFinancialReportService
{
    Task<TrialBalanceDto> GetTrialBalanceAsync(DateTime startDate, DateTime endDate);
    Task<IncomeStatementDto> GetIncomeStatementAsync(DateTime startDate, DateTime endDate);
    Task<BalanceSheetDto> GetBalanceSheetAsync(DateTime asOfDate);
}
