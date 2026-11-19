using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

public class AppMenu : BaseEntity
{
    [Required, StringLength(80)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Url { get; set; } = string.Empty;

    [StringLength(60)]
    public string Icon { get; set; } = "bi-circle";

    [StringLength(60)]
    public string? MenuGroup { get; set; }

    [StringLength(80)]
    public string RequiredPermission { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
