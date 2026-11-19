using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class RawMaterialService : IRawMaterialService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IManufacturingAccountingService? _accounting;

    public RawMaterialService(
        IDbContextFactory<AppDbContext> contextFactory,
        IManufacturingAccountingService? accounting = null)
    {
        _contextFactory = contextFactory;
        _accounting = accounting;
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
        if (string.IsNullOrWhiteSpace(material.Name))
            throw new ArgumentException("El nombre del material es obligatorio.");
        if (material.CurrentStock < 0m || material.AverageCost < 0m || material.MinimumStockLevel < 0m)
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
        if (string.IsNullOrWhiteSpace(material.Name))
            throw new ArgumentException("El nombre del material es obligatorio.");

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
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo del ajuste es obligatorio.", nameof(reason));

        RawMaterial snapshot;
        decimal delta;

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var material = await context.RawMaterials.FindAsync(rawMaterialId)
                ?? throw new InvalidOperationException("Materia prima no encontrada.");

            decimal roundedNew = Math.Round(newStock, 4, MidpointRounding.AwayFromZero);
            delta = roundedNew - material.CurrentStock;
            material.CurrentStock = roundedNew;
            await context.SaveChangesAsync();
            snapshot = material;
        }

        if (delta != 0m && snapshot.AverageCost > 0m && _accounting != null)
        {
            decimal absValue = Math.Round(Math.Abs(delta) * snapshot.AverageCost, 2, MidpointRounding.AwayFromZero);
            if (absValue > 0m)
            {
                await _accounting.PostRawMaterialAdjustmentAsync(snapshot, delta, reason.Trim());
            }
        }
    }
}
