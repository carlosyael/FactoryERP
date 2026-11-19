using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Materia prima, ingrediente activo o material de empaque usado en fórmulas capilares.
/// </summary>
public class RawMaterial : BaseEntity
{
    [Required(ErrorMessage = "El SKU es obligatorio.")]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del material es obligatorio.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(350)]
    public string Description { get; set; } = string.Empty;

    public RawMaterialCategory Category { get; set; } = RawMaterialCategory.Base;
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Gram;

    [Range(0, 999999999)]
    public decimal CurrentStock { get; set; }

    [Range(0, 999999999)]
    public decimal MinimumStockLevel { get; set; }

    [Range(0, 999999999)]
    public decimal AverageCost { get; set; }

    [Range(0, 999999999)]
    public decimal LastPurchaseCost { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsLowStock => CurrentStock <= MinimumStockLevel;
}
