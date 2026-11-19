using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class ChartOfAccountsService : IChartOfAccountsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ChartOfAccountsService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<AccountingAccount>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountingAccounts
            .AsNoTracking()
            .Include(a => a.ParentAccount)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }

    public async Task<List<AccountingAccount>> GetPostableAccountsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountingAccounts
            .AsNoTracking()
            .Where(a => a.IsActive && !a.IsControlAccount)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }

    public async Task<AccountingAccount?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AccountingAccounts
            .Include(a => a.ParentAccount)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AccountingAccount> CreateAsync(AccountingAccount account)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        string normalizedCode = account.Code.Trim();
        if (await context.AccountingAccounts.AnyAsync(a => a.Code == normalizedCode))
        {
            throw new InvalidOperationException($"Ya existe una cuenta contable con el código '{normalizedCode}'.");
        }

        account.Code = normalizedCode;
        account.Name = account.Name.Trim();
        context.AccountingAccounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task UpdateAsync(AccountingAccount account)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.AccountingAccounts.FindAsync(account.Id)
            ?? throw new InvalidOperationException("Cuenta contable no encontrada.");

        string normalizedCode = account.Code.Trim();
        if (await context.AccountingAccounts.AnyAsync(a => a.Code == normalizedCode && a.Id != account.Id))
        {
            throw new InvalidOperationException($"El código '{normalizedCode}' ya pertenece a otra cuenta.");
        }

        existing.Code = normalizedCode;
        existing.Name = account.Name.Trim();
        existing.Group = account.Group;
        existing.IsControlAccount = account.IsControlAccount;
        existing.IsActive = account.IsActive;
        existing.ParentAccountId = account.ParentAccountId;

        await context.SaveChangesAsync();
    }

    public async Task<int> SeedStandardChartAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.AccountingAccounts.ToDictionaryAsync(a => a.Code);
        int added = 0;

        var controls = new (string Code, string Name, AccountGroup Group)[]
        {
            (AccountCodes.CurrentAssetsControl, "Activos Corrientes", AccountGroup.Activo),
            (AccountCodes.CurrentLiabilitiesControl, "Pasivos Corrientes", AccountGroup.Pasivo),
            (AccountCodes.EquityControl, "Capital y Patrimonio", AccountGroup.Capital),
            (AccountCodes.RevenueControl, "Ingresos Operacionales", AccountGroup.Ingresos),
            (AccountCodes.CostControl, "Costos de Producción y Ventas", AccountGroup.Costos),
            (AccountCodes.ExpenseControl, "Gastos Operativos", AccountGroup.Gastos)
        };

        foreach (var (code, name, group) in controls)
        {
            if (!existing.ContainsKey(code))
            {
                var acc = new AccountingAccount { Code = code, Name = name, Group = group, IsControlAccount = true };
                context.AccountingAccounts.Add(acc);
                await context.SaveChangesAsync();
                existing[code] = acc;
                added++;
            }
        }

        var details = new (string Code, string Name, AccountGroup Group, string ParentCode)[]
        {
            (AccountCodes.Cash, "Caja General", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.Bank, "Bancos", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.AccountsReceivable, "Cuentas por Cobrar Clientes", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.RawMaterialInventory, "Inventario de Materias Primas e Insumos", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.WorkInProgressInventory, "Inventario de Producción en Proceso (WIP)", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.FinishedGoodsInventory, "Inventario de Productos Terminados", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),
            (AccountCodes.TaxCreditAdvance, "ITBIS Adelantado en Compras", AccountGroup.Activo, AccountCodes.CurrentAssetsControl),

            (AccountCodes.AccountsPayable, "Cuentas por Pagar Suplidores", AccountGroup.Pasivo, AccountCodes.CurrentLiabilitiesControl),
            (AccountCodes.TaxPayable, "ITBIS por Pagar DGII", AccountGroup.Pasivo, AccountCodes.CurrentLiabilitiesControl),

            (AccountCodes.ShareCapital, "Capital Social", AccountGroup.Capital, AccountCodes.EquityControl),
            (AccountCodes.RetainedEarnings, "Resultados Acumulados", AccountGroup.Capital, AccountCodes.EquityControl),

            (AccountCodes.ProductSalesRevenue, "Ingresos por Ventas de Productos", AccountGroup.Ingresos, AccountCodes.RevenueControl),

            (AccountCodes.CostOfGoodsSold, "Costo de Productos Vendidos (COGS)", AccountGroup.Costos, AccountCodes.CostControl),
            (AccountCodes.AbnormalProductionLoss, "Merma Anormal de Producción", AccountGroup.Costos, AccountCodes.CostControl),

            (AccountCodes.ManufacturingOverheadApplied, "Costos Indirectos de Fabricación Aplicados", AccountGroup.Gastos, AccountCodes.ExpenseControl),
            (AccountCodes.OperatingExpenses, "Gastos Operativos Generales", AccountGroup.Gastos, AccountCodes.ExpenseControl),
            (AccountCodes.AdministrativeExpenses, "Gastos Administrativos", AccountGroup.Gastos, AccountCodes.ExpenseControl),
            (AccountCodes.DistributionExpenses, "Gastos de Distribución y Envíos", AccountGroup.Gastos, AccountCodes.ExpenseControl)
        };

        foreach (var (code, name, group, parentCode) in details)
        {
            if (!existing.ContainsKey(code))
            {
                context.AccountingAccounts.Add(new AccountingAccount
                {
                    Code = code,
                    Name = name,
                    Group = group,
                    IsControlAccount = false,
                    ParentAccountId = existing[parentCode].Id
                });
                added++;
            }
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
        }

        return added;
    }
}
