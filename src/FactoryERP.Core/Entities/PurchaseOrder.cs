using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class PurchaseOrder : BaseEntity
{
    [Required, StringLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }

    [StringLength(300)]
    public string Notes { get; set; } = string.Empty;

    public List<PurchaseOrderItem> Items { get; set; } = new();
}
