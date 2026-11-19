using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class JournalEntryService : IJournalEntryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public JournalEntryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<JournalEntry>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .ThenInclude(l => l.Account)
            .AsQueryable();

        if (startDate.HasValue) query = query.Where(j => j.Date >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(j => j.Date <= endDate.Value.Date.AddDays(1).AddTicks(-1));

        return await query.OrderByDescending(j => j.Date).ThenByDescending(j => j.Id).ToListAsync();
    }

    public async Task<JournalEntry?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<JournalEntry> CreateAsync(CreateJournalEntryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Concept))
            throw new InvalidOperationException("El concepto del asiento es obligatorio.");

        if (request.Lines == null || request.Lines.Count < 2)
            throw new InvalidOperationException("Un asiento contable requiere al menos 2 líneas.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await context.AccountingAccounts
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        var lines = new List<JournalEntryLine>();

        foreach (var line in request.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account))
                throw new InvalidOperationException($"Cuenta contable ID {line.AccountId} no existe.");
            if (!account.IsActive)
                throw new InvalidOperationException($"La cuenta '{account.Code} - {account.Name}' está inactiva.");
            if (account.IsControlAccount)
                throw new InvalidOperationException($"No se pueden registrar movimientos directos en la cuenta control '{account.Code}'.");
            if (line.Debit < 0m || line.Credit < 0m || (line.Debit == 0m && line.Credit == 0m) || (line.Debit > 0m && line.Credit > 0m))
                throw new InvalidOperationException("Cada línea debe tener un monto positivo exclusivamente en Débito o en Crédito.");

            decimal debit = Math.Round(line.Debit, 2, MidpointRounding.AwayFromZero);
            decimal credit = Math.Round(line.Credit, 2, MidpointRounding.AwayFromZero);
            totalDebit += debit;
            totalCredit += credit;

            lines.Add(new JournalEntryLine
            {
                AccountId = account.Id,
                Debit = debit,
                Credit = credit,
                Description = line.Description.Trim()
            });
        }

        if (totalDebit <= 0m || totalDebit != totalCredit)
            throw new InvalidOperationException($"Asiento descuadrado: Débito ({totalDebit:N2}) != Crédito ({totalCredit:N2}).");

        var entry = new JournalEntry
        {
            EntryNumber = $"AST-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Date = request.Date,
            Concept = request.Concept.Trim(),
            Reference = request.Reference.Trim(),
            Status = request.PostImmediately ? JournalEntryStatus.Posted : JournalEntryStatus.Draft,
            PostedAt = request.PostImmediately ? DateTime.UtcNow : null,
            Lines = lines
        };

        context.JournalEntries.Add(entry);
        await context.SaveChangesAsync();

        return (await GetByIdAsync(entry.Id))!;
    }

    public async Task PostAsync(int journalEntryId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.Id == journalEntryId)
            ?? throw new InvalidOperationException("Asiento contable no encontrado.");

        if (entry.Status != JournalEntryStatus.Draft)
            throw new InvalidOperationException("Solo los asientos en borrador pueden mayorizarse.");

        decimal debit = entry.Lines.Sum(l => l.Debit);
        decimal credit = entry.Lines.Sum(l => l.Credit);
        if (debit <= 0m || debit != credit)
            throw new InvalidOperationException("El asiento no está cuadrado y no puede mayorizarse.");

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task VoidAsync(int journalEntryId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.JournalEntries.FindAsync(journalEntryId)
            ?? throw new InvalidOperationException("Asiento contable no encontrado.");

        if (entry.Status == JournalEntryStatus.Voided) return;
        entry.Status = JournalEntryStatus.Voided;
        await context.SaveChangesAsync();
    }

    public async Task<JournalEntry> RecordExpenseAsync(
        string expenseAccountCode,
        string paymentAccountCode,
        decimal amount,
        string concept,
        string reference,
        DateTime? date = null)
    {
        if (amount <= 0m) throw new ArgumentException("El monto del gasto debe ser mayor a cero.", nameof(amount));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var expenseAcc = await context.AccountingAccounts.FirstOrDefaultAsync(a => a.Code == expenseAccountCode)
            ?? throw new InvalidOperationException($"Cuenta de gasto '{expenseAccountCode}' no encontrada.");
        var paymentAcc = await context.AccountingAccounts.FirstOrDefaultAsync(a => a.Code == paymentAccountCode)
            ?? throw new InvalidOperationException($"Cuenta de pago '{paymentAccountCode}' no encontrada.");

        decimal rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        return await CreateAsync(new CreateJournalEntryRequest(
            date ?? DateTime.UtcNow,
            concept,
            reference,
            true,
            [
                new(expenseAcc.Id, rounded, 0m, concept),
                new(paymentAcc.Id, 0m, rounded, $"Pago {concept}")
            ]));
    }
}
