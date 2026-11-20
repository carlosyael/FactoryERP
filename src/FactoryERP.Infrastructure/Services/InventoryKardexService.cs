using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class InventoryKardexService : IInventoryKardexService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public InventoryKardexService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<InventoryMovement>> GetMovementsAsync(InventoryMovementFilter? filter = null)
    {
        filter ??= new InventoryMovementFilter();
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.InventoryMovements.AsNoTracking()
            .Include(m => m.RawMaterial)
            .Include(m => m.Product)
            .Include(m => m.FinishedBatch)
            .AsQueryable();

        if (filter.ItemType.HasValue)
            query = query.Where(m => m.ItemType == filter.ItemType.Value);
        if (filter.MovementType.HasValue)
            query = query.Where(m => m.MovementType == filter.MovementType.Value);
        if (filter.RawMaterialId.HasValue)
            query = query.Where(m => m.RawMaterialId == filter.RawMaterialId.Value);
        if (filter.ProductId.HasValue)
            query = query.Where(m => m.ProductId == filter.ProductId.Value);
        if (filter.StartDate.HasValue)
        {
            DateTime start = filter.StartDate.Value.Date;
            query = query.Where(m => m.MovementDate >= start);
        }
        if (filter.EndDate.HasValue)
        {
            DateTime endExclusive = filter.EndDate.Value.Date.AddDays(1);
            query = query.Where(m => m.MovementDate < endExclusive);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.Reference.ToLower().Contains(term) ||
                m.BatchCode.ToLower().Contains(term) ||
                m.Notes.ToLower().Contains(term) ||
                (m.RawMaterial != null && (m.RawMaterial.Name.ToLower().Contains(term) || m.RawMaterial.Sku.ToLower().Contains(term))) ||
                (m.Product != null && (m.Product.Name.ToLower().Contains(term) || m.Product.Sku.ToLower().Contains(term))));
        }

        int max = Math.Clamp(filter.MaxResults, 1, 1000);
        return await query.OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id).Take(max).ToListAsync();
    }
}
