using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class ProductService : IProductService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ProductService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Product>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Products
            .AsNoTracking()
            .Include(p => p.FinishedBatches)
            .AsQueryable();

        if (!includeInactive) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Products
            .AsNoTracking()
            .Include(p => p.FinishedBatches)
            .Include(p => p.Formulas)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        string sku = product.Sku.Trim().ToUpperInvariant();
        if (await context.Products.AnyAsync(p => p.Sku == sku))
            throw new InvalidOperationException($"Ya existe un producto con SKU '{sku}'.");

        product.Sku = sku;
        product.Name = product.Name.Trim();
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Products.FindAsync(product.Id)
            ?? throw new InvalidOperationException("Producto no encontrado.");

        string sku = product.Sku.Trim().ToUpperInvariant();
        if (await context.Products.AnyAsync(p => p.Sku == sku && p.Id != product.Id))
            throw new InvalidOperationException($"El SKU '{sku}' ya está asignado a otro producto.");

        existing.Sku = sku;
        existing.Name = product.Name.Trim();
        existing.Line = product.Line.Trim();
        existing.Presentation = product.Presentation.Trim();
        existing.Description = product.Description.Trim();
        existing.Benefits = product.Benefits.Trim();
        existing.UsageInstructions = product.UsageInstructions.Trim();
        existing.KeyIngredients = product.KeyIngredients.Trim();
        existing.ImageUrl = product.ImageUrl.Trim();
        existing.RetailPrice = product.RetailPrice;
        existing.SalonPrice = product.SalonPrice;
        existing.WholesalePrice = product.WholesalePrice;
        existing.TaxRate = product.TaxRate;
        existing.IsTaxable = product.IsTaxable;
        existing.MinimumStockLevel = product.MinimumStockLevel;
        existing.ShowInPublicCatalog = product.ShowInPublicCatalog;
        existing.IsActive = product.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task<List<FinishedBatch>> GetBatchesAsync(int? productId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.FinishedBatches
            .AsNoTracking()
            .Include(b => b.Product)
            .AsQueryable();

        if (productId.HasValue) query = query.Where(b => b.ProductId == productId.Value);

        return await query.OrderByDescending(b => b.ProductionDate).ThenByDescending(b => b.Id).ToListAsync();
    }
}
