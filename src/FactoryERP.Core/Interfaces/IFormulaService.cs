using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IFormulaService
{
    Task<List<ProductFormula>> GetAllAsync(bool includeInactive = false);
    Task<List<ProductFormula>> GetByProductIdAsync(int productId);
    Task<ProductFormula?> GetByIdAsync(int id);
    Task<ProductFormula> CreateAsync(ProductFormula formula);
    Task UpdateAsync(ProductFormula formula);
    Task<decimal> CalculateBaseMaterialCostAsync(int formulaId);
}
