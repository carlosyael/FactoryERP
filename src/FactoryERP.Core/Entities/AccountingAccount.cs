using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class AccountingAccount : BaseEntity
{
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public AccountGroup Group { get; set; }
    public bool IsControlAccount { get; set; }
    public bool IsActive { get; set; } = true;

    public int? ParentAccountId { get; set; }
    public AccountingAccount? ParentAccount { get; set; }

    public ICollection<AccountingAccount> SubAccounts { get; set; } = new List<AccountingAccount>();
    public ICollection<JournalEntryLine> JournalLines { get; set; } = new List<JournalEntryLine>();
}
