using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class SupplierPayment : BaseEntity
{
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    [StringLength(80)]
    public string Reference { get; set; } = string.Empty;

    [StringLength(250)]
    public string Notes { get; set; } = string.Empty;
}
