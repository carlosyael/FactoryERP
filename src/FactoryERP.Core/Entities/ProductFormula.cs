using System.ComponentModel.DataAnnotations;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Receta versionada de fabricación para un producto capilar.
/// </summary>
public class ProductFormula : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required(ErrorMessage = "El nombre de la fórmula es obligatorio.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Version { get; set; } = "1.0";

    [Range(1, 1000000)]
    public int BaseYieldQuantity { get; set; } = 1;

    [Range(0, 99.99)]
    public decimal StandardLossMarginPercent { get; set; } = 5m;

    [Range(0, 999999999)]
    public decimal OverheadCostPerBatch { get; set; }

    [StringLength(1000)]
    public string Instructions { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<FormulaItem> Items { get; set; } = new();
}
