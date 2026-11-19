using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class Customer : BaseEntity
{
    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El RNC o Cédula es obligatorio.")]
    [StringLength(20)]
    public string RncCedula { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; } = CustomerType.Retail;

    [StringLength(25)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(25)]
    public string WhatsApp { get; set; } = string.Empty;

    [StringLength(120)]
    public string Email { get; set; } = string.Empty;

    [StringLength(250)]
    public string Address { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal CreditLimit { get; set; }

    [Range(0, 999999999)]
    public decimal CurrentBalance { get; set; }

    [Range(0, 365)]
    public int CreditDays { get; set; } = 30;

    [Range(0, 100)]
    public decimal DefaultDiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;
}
