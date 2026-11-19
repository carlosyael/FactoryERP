using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

public class Supplier : BaseEntity
{
    [Required(ErrorMessage = "El nombre del suplidor es obligatorio.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string Rnc { get; set; } = string.Empty;

    [StringLength(25)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; } = true;
}
