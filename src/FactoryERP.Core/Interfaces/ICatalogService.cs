using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface ICatalogService
{
    Task<List<InternalCatalogItemDto>> GetInternalCatalogAsync(string? search = null, string? line = null);
    Task<List<PublicCatalogItemDto>> GetPublicCatalogAsync(string? search = null, string? line = null, bool onlyInStock = false);
    Task<List<string>> GetProductLinesAsync();
}
