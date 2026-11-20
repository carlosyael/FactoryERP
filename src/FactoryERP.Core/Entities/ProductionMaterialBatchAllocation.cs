namespace FactoryERP.Core.Entities;

/// <summary>
/// Relación de trazabilidad entre una orden de producción y el lote de materia prima consumido.
/// </summary>
public class ProductionMaterialBatchAllocation : BaseEntity
{
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public int RawMaterialBatchId { get; set; }
    public RawMaterialBatch? RawMaterialBatch { get; set; }

    public decimal QuantityAllocated { get; set; }
}
