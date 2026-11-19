using System.ComponentModel.DataAnnotations;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Entities;

public class FormulaItem : BaseEntity
{
    public int ProductFormulaId { get; set; }
    public ProductFormula? ProductFormula { get; set; }

    public int RawMaterialId { get; set; }
    public RawMaterial? RawMaterial { get; set; }

    [Range(0.0001, 999999999)]
    public decimal QuantityRequired { get; set; }

    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Gram;
}
