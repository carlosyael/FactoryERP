using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IRawMaterialService
{
    Task<List<RawMaterial>> GetAllAsync(bool includeInactive = false);
    Task<RawMaterial?> GetByIdAsync(int id);
    Task<RawMaterial> CreateAsync(RawMaterial material);
    Task UpdateAsync(RawMaterial material);
    Task AdjustStockAsync(int rawMaterialId, decimal newStock, string reason);
}
