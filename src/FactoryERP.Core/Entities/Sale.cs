using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class Sale : BaseEntity
{
    [Required, StringLength(40)]
    public string SaleNumber { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal TotalCogs { get; set; }

    [StringLength(300)]
    public string Notes { get; set; } = string.Empty;

    [StringLength(40)]
    public string? TaxDocumentNumber { get; set; }

    public ElectronicInvoiceStatus ElectronicInvoiceStatus { get; set; } = ElectronicInvoiceStatus.PendingImplementation;

    public List<SaleItem> Items { get; set; } = new();
}
