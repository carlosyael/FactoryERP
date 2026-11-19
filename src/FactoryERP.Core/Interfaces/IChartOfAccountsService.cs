using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IChartOfAccountsService
{
    Task<List<AccountingAccount>> GetAllAsync();
    Task<List<AccountingAccount>> GetPostableAccountsAsync();
    Task<AccountingAccount?> GetByIdAsync(int id);
    Task<AccountingAccount> CreateAsync(AccountingAccount account);
    Task UpdateAsync(AccountingAccount account);
    Task<int> SeedStandardChartAsync();
}
