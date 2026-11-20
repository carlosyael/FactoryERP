using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Lote específico de materia prima recibido con fecha de vencimiento para trazabilidad FEFO.
/// </summary>
public class RawMaterialBatch : BaseEntity
{
    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    public int? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [Required, StringLength(40)]
    public string BatchCode { get; set; } = string.Empty;

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }

    public decimal InitialQuantity { get; set; }
    public decimal QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }

    [StringLength(300)]
    public string Notes { get; set; } = string.Empty;

    public bool IsExpired(DateTime utcToday) => ExpirationDate.HasValue && ExpirationDate.Value.Date < utcToday.Date;

    public bool IsUsable(DateTime utcToday) => QuantityRemaining > 0m && !IsExpired(utcToday);
}
