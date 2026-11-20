using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Registro cronológico (Kardex) de entradas, salidas y ajustes de materias primas o lotes terminados.
/// </summary>
public class InventoryMovement : BaseEntity
{
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public InventoryItemType ItemType { get; set; }
    public InventoryMovementType MovementType { get; set; }

    public int? RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    public int? FinishedBatchId { get; set; }
    public FinishedBatch? FinishedBatch { get; set; }

    [StringLength(40)]
    public string BatchCode { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string Reference { get; set; } = string.Empty;

    public decimal QuantityDelta { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }

    [StringLength(400)]
    public string Notes { get; set; } = string.Empty;

    [StringLength(120)]
    public string PerformedBy { get; set; } = string.Empty;
}
