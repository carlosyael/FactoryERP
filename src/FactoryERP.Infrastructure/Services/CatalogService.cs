using FactoryERP.Core.Constants;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class CatalogService : ICatalogService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ISystemSettingService _settings;

    public CatalogService(IDbContextFactory<AppDbContext> contextFactory, ISystemSettingService settings)
    {
        _contextFactory = contextFactory;
        _settings = settings;
    }

    public async Task<List<InternalCatalogItemDto>> GetInternalCatalogAsync(string? search = null, string? line = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Products
            .AsNoTracking()
            .Include(p => p.FinishedBatches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term) || p.Line.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            query = query.Where(p => p.Line == line);
        }

        var products = await query.OrderBy(p => p.Line).ThenBy(p => p.Name).ToListAsync();
        return products.Select(p =>
        {
            var activeBatches = p.FinishedBatches.Where(b => b.QuantityRemaining > 0).ToList();
            int stock = activeBatches.Sum(b => b.QuantityRemaining);
            DateTime? nearestExp = activeBatches
                .Where(b => b.ExpirationDate.HasValue)
                .OrderBy(b => b.ExpirationDate)
                .Select(b => b.ExpirationDate)
                .FirstOrDefault();

            return new InternalCatalogItemDto(
                p.Id, p.Sku, p.Name, p.Line, p.Presentation, p.ImageUrl,
                p.Cost, p.RetailPrice, p.SalonPrice, p.WholesalePrice,
                p.GetGrossMarginPercent(), stock, p.MinimumStockLevel,
                p.ShowInPublicCatalog, p.IsActive, nearestExp);
        }).ToList();
    }

    public async Task<List<PublicCatalogItemDto>> GetPublicCatalogAsync(string? search = null, string? line = null, bool onlyInStock = false)
    {
        if (!await _settings.GetBoolAsync(SettingKeys.CatalogPublicEnabled, true))
        {
            return [];
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Products
            .AsNoTracking()
            .Include(p => p.FinishedBatches)
            .Where(p => p.IsActive && p.ShowInPublicCatalog);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term) || p.KeyIngredients.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            query = query.Where(p => p.Line == line);
        }

        var products = await query.OrderBy(p => p.Line).ThenBy(p => p.Name).ToListAsync();
        var items = products.Select(p =>
        {
            int stock = p.FinishedBatches.Where(b => b.QuantityRemaining > 0).Sum(b => b.QuantityRemaining);
            return new PublicCatalogItemDto(
                p.Id, p.Sku, p.Name, p.Line, p.Presentation,
                p.Description, p.Benefits, p.UsageInstructions, p.KeyIngredients, p.ImageUrl,
                p.RetailPrice, p.SalonPrice, p.WholesalePrice,
                stock, stock > 0);
        });

        if (onlyInStock) items = items.Where(i => i.InStock);
        return items.ToList();
    }

    public async Task<List<string>> GetProductLinesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.Line))
            .Select(p => p.Line)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();
    }
}
