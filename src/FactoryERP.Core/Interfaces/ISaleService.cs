using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface ISaleService
{
    Task<List<Sale>> GetAllAsync();
    Task<Sale?> GetByIdAsync(int id);
    Task<Sale> CreateSaleAsync(CreateSaleRequest request);
}
