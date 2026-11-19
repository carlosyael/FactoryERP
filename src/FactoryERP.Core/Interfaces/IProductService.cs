using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IProductService
{
    Task<List<Product>> GetAllAsync(bool includeInactive = false);
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task<List<FinishedBatch>> GetBatchesAsync(int? productId = null);
}
