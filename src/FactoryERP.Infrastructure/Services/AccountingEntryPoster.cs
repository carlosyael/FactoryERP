using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class AccountingEntryPoster
{
    public static async Task<JournalEntry> CreatePostedEntryAsync(
        IDbContextFactory<AppDbContext> contextFactory,
        DateTime date,
        string concept,
        string reference,
        string? userId,
        List<(string Code, decimal Debit, decimal Credit, string Desc)> rawLines)
    {
        var nonZero = rawLines.Where(l => l.Debit > 0m || l.Credit > 0m).ToList();
        if (nonZero.Count < 2)
            throw new InvalidOperationException("No se puede generar un asiento contable con menos de dos líneas válidas.");

        await using var context = await contextFactory.CreateDbContextAsync();
        var codes = nonZero.Select(l => l.Code).Distinct().ToList();
        var accountMap = await context.AccountingAccounts.Where(a => codes.Contains(a.Code)).ToDictionaryAsync(a => a.Code);

        var entryLines = new List<JournalEntryLine>();
        foreach (var (code, debit, credit, desc) in nonZero)
        {
            if (!accountMap.TryGetValue(code, out var account))
                throw new InvalidOperationException($"Cuenta contable requerida '{code}' no existe en el plan de cuentas.");

            entryLines.Add(new JournalEntryLine
            {
                AccountId = account.Id,
                Debit = Math.Round(debit, 2, MidpointRounding.AwayFromZero),
                Credit = Math.Round(credit, 2, MidpointRounding.AwayFromZero),
                Description = desc
            });
        }

        decimal totalDebit = entryLines.Sum(l => l.Debit);
        decimal totalCredit = entryLines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
            throw new InvalidOperationException($"Asiento automático descuadrado ({concept}): D={totalDebit:N2} vs C={totalCredit:N2}");

        string? validUserId = (!string.IsNullOrWhiteSpace(userId) && await context.Users.AnyAsync(u => u.Id == userId)) ? userId : null;
        var entry = new JournalEntry
        {
            EntryNumber = $"AST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Date = date,
            Concept = concept,
            Reference = reference,
            Status = JournalEntryStatus.Posted,
            PostedAt = DateTime.UtcNow,
            CreatedByUserId = validUserId,
            Lines = entryLines
        };

        context.JournalEntries.Add(entry);
        await context.SaveChangesAsync();

        return await context.JournalEntries.AsNoTracking()
            .Include(j => j.Lines).ThenInclude(l => l.Account)
            .FirstAsync(j => j.Id == entry.Id);
    }
}
