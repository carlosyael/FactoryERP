using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

/// <summary>
/// Producto capilar terminado disponible en catálogo interno y opcionalmente en catálogo público.
/// </summary>
public class Product : BaseEntity
{
    [Required(ErrorMessage = "El SKU es obligatorio.")]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [StringLength(80)]
    public string Line { get; set; } = "General";

    [StringLength(60)]
    public string Presentation { get; set; } = "16 oz";

    [StringLength(600)]
    public string Description { get; set; } = string.Empty;

    [StringLength(500)]
    public string Benefits { get; set; } = string.Empty;

    [StringLength(500)]
    public string UsageInstructions { get; set; } = string.Empty;

    [StringLength(400)]
    public string KeyIngredients { get; set; } = string.Empty;

    [StringLength(300)]
    public string ImageUrl { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal RetailPrice { get; set; }

    [Range(0, 999999999)]
    public decimal SalonPrice { get; set; }

    [Range(0, 999999999)]
    public decimal WholesalePrice { get; set; }

    [Range(0, 999999999)]
    public decimal Cost { get; set; }

    [Range(0, 1)]
    public decimal TaxRate { get; set; } = 0.18m;

    public bool IsTaxable { get; set; } = true;

    [Range(0, 1000000)]
    public int MinimumStockLevel { get; set; }

    public bool ShowInPublicCatalog { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public List<FinishedBatch> FinishedBatches { get; set; } = new();
    public List<ProductFormula> Formulas { get; set; } = new();

    public decimal GetPriceForCustomerType(CustomerType customerType) => customerType switch
    {
        CustomerType.Salon when SalonPrice > 0 => SalonPrice,
        CustomerType.Wholesale when WholesalePrice > 0 => WholesalePrice,
        _ => RetailPrice
    };

    public decimal GetGrossMarginPercent()
    {
        if (RetailPrice <= 0) return 0m;
        return Math.Round(((RetailPrice - Cost) / RetailPrice) * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
