using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    [Range(0.0001, 999999999)]
    public decimal Quantity { get; set; }

    [Range(0, 999999999)]
    public decimal UnitCost { get; set; }

    public bool IsTaxable { get; set; } = true;
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
