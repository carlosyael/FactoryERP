namespace FactoryERP.Core.Entities;

public class ProductionOrderMaterial : BaseEntity
{
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public decimal PlannedQuantity { get; set; }
    public decimal ActualQuantityUsed { get; set; }
    public decimal UnitCostAtConsumption { get; set; }
    public decimal TotalCost { get; set; }
}
