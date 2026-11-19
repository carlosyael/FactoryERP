using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class CustomerPayment : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [StringLength(80)]
    public string Reference { get; set; } = string.Empty;

    [StringLength(250)]
    public string Notes { get; set; } = string.Empty;
}
