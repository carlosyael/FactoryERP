using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class JournalEntry : BaseEntity
{
    [Required, StringLength(40)]
    public string EntryNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    [Required, StringLength(255)]
    public string Concept { get; set; } = string.Empty;

    [StringLength(100)]
    public string Reference { get; set; } = string.Empty;

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public DateTime? PostedAt { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    public List<JournalEntryLine> Lines { get; set; } = new();
}
