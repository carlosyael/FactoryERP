using System.ComponentModel.DataAnnotations;

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
}
