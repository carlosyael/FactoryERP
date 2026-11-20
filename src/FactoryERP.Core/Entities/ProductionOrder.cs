using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Orden de preparación y fabricación con trazabilidad de insumos, merma y resultado real.
/// </summary>
public class ProductionOrder : BaseEntity
{
    [Required, StringLength(40)]
    public string OrderNumber { get; set; } = string.Empty;

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int ProductFormulaId { get; set; }
    public ProductFormula? ProductFormula { get; set; }

    [Required, StringLength(40)]
    public string BatchCode { get; set; } = string.Empty;

    public DateTime PlannedDate { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Draft;

    [Range(1, 1000000)]
    public int PlannedQuantity { get; set; }

    [Range(0, 99.99)]
    public decimal ExpectedLossMarginPercent { get; set; }

    public int ExpectedResultingQuantity { get; set; }

    public int? ActualYieldQuantity { get; set; }
    public int? ActualLossQuantity { get; set; }
    public decimal? ActualLossPercent { get; set; }

    public decimal EstimatedMaterialCost { get; set; }
    public decimal EstimatedOverheadCost { get; set; }
    public decimal ActualMaterialCost { get; set; }
    public decimal ActualOverheadCost { get; set; }
    public decimal AbnormalLossCost { get; set; }
    public decimal CapitalizedInventoryCost { get; set; }
    public decimal ActualUnitCost { get; set; }

    public DateTime? ExpirationDate { get; set; }

    [StringLength(400)]
    public string Notes { get; set; } = string.Empty;

    public List<ProductionOrderMaterial> Materials { get; set; } = new();
    public List<ProductionMaterialBatchAllocation> BatchAllocations { get; set; } = new();
}
