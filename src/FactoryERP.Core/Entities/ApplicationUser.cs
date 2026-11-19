using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FactoryERP.Core.Entities;

public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool RequirePasswordChange { get; set; }
}
