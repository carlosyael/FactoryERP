using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class RawMaterialService : IRawMaterialService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public RawMaterialService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<RawMaterial>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.RawMaterials.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(m => m.IsActive);
        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<RawMaterial?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.RawMaterials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<RawMaterial> CreateAsync(RawMaterial material)
    {
        if (material.CurrentStock < 0m || material.AverageCost < 0m)
            throw new ArgumentException("El stock y el costo no pueden ser negativos.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        string sku = material.Sku.Trim().ToUpperInvariant();
        if (await context.RawMaterials.AnyAsync(m => m.Sku == sku))
            throw new InvalidOperationException($"Ya existe un material con SKU '{sku}'.");

        material.Sku = sku;
        material.Name = material.Name.Trim();
        context.RawMaterials.Add(material);
        await context.SaveChangesAsync();
        return material;
    }

    public async Task UpdateAsync(RawMaterial material)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.RawMaterials.FindAsync(material.Id)
            ?? throw new InvalidOperationException("Materia prima no encontrada.");

        string sku = material.Sku.Trim().ToUpperInvariant();
        if (await context.RawMaterials.AnyAsync(m => m.Sku == sku && m.Id != material.Id))
            throw new InvalidOperationException($"El SKU '{sku}' ya está asignado a otro material.");

        existing.Sku = sku;
        existing.Name = material.Name.Trim();
        existing.Description = material.Description.Trim();
        existing.Category = material.Category;
        existing.UnitOfMeasure = material.UnitOfMeasure;
        existing.MinimumStockLevel = Math.Max(0m, material.MinimumStockLevel);
        existing.AverageCost = Math.Max(0m, material.AverageCost);
        existing.IsActive = material.IsActive;

        await context.SaveChangesAsync();
    }

    public async Task AdjustStockAsync(int rawMaterialId, decimal newStock, string reason)
    {
        if (newStock < 0m)
            throw new ArgumentException("El inventario de materia prima no puede ser negativo.", nameof(newStock));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var material = await context.RawMaterials.FindAsync(rawMaterialId)
            ?? throw new InvalidOperationException("Materia prima no encontrada.");

        material.CurrentStock = Math.Round(newStock, 4, MidpointRounding.AwayFromZero);
        await context.SaveChangesAsync();
    }
}
