using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Lote de producto terminado resultante de una orden de fabricación.
/// </summary>
public class FinishedBatch : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }

    [Required, StringLength(40)]
    public string BatchCode { get; set; } = string.Empty;

    public DateTime ProductionDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDate { get; set; }

    public int InitialQuantity { get; set; }
    public int QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }

    public BatchQualityStatus QualityStatus { get; set; } = BatchQualityStatus.Approved;

    [StringLength(400)]
    public string QualityNotes { get; set; } = string.Empty;

    public DateTime? QualityCheckedAt { get; set; }

    [StringLength(120)]
    public string QualityCheckedBy { get; set; } = string.Empty;

    public bool IsExpired(DateTime utcToday) => ExpirationDate.HasValue && ExpirationDate.Value.Date < utcToday.Date;

    public bool IsSellable(DateTime utcToday) =>
        QuantityRemaining > 0 && QualityStatus == BatchQualityStatus.Approved && !IsExpired(utcToday);
}
