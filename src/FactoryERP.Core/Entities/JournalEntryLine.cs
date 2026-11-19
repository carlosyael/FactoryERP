using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

public class JournalEntryLine : BaseEntity
{
    public int JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    public int AccountId { get; set; }
    public AccountingAccount? Account { get; set; }

    [Range(0, 999999999.99)]
    public decimal Debit { get; set; }

    [Range(0, 999999999.99)]
    public decimal Credit { get; set; }

    [StringLength(255)]
    public string Description { get; set; } = string.Empty;
}
