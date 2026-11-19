using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IJournalEntryService
{
    Task<List<JournalEntry>> GetAllAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<JournalEntry?> GetByIdAsync(int id);
    Task<JournalEntry> CreateAsync(CreateJournalEntryRequest request);
    Task PostAsync(int journalEntryId);
    Task VoidAsync(int journalEntryId);
    Task<JournalEntry> RecordExpenseAsync(string expenseAccountCode, string paymentAccountCode, decimal amount, string concept, string reference, DateTime? date = null);
}
