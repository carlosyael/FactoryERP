using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }

    [StringLength(120)]
    public string UserId { get; set; } = "system";

    [Required, StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string EntityId { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Action { get; set; } = string.Empty;

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
