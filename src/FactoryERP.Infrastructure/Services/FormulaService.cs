using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class FormulaService : IFormulaService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public FormulaService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<ProductFormula>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ProductFormulas
            .AsNoTracking()
            .Include(f => f.Product)
            .Include(f => f.Items).ThenInclude(i => i.RawMaterial)
            .AsQueryable();

        if (!includeInactive) query = query.Where(f => f.IsActive);
        return await query.OrderBy(f => f.Product!.Name).ThenByDescending(f => f.Version).ToListAsync();
    }

    public async Task<List<ProductFormula>> GetByProductIdAsync(int productId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProductFormulas
            .AsNoTracking()
            .Include(f => f.Items).ThenInclude(i => i.RawMaterial)
            .Where(f => f.ProductId == productId)
            .OrderByDescending(f => f.Version)
            .ToListAsync();
    }

    public async Task<ProductFormula?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProductFormulas
            .AsNoTracking()
            .Include(f => f.Product)
            .Include(f => f.Items).ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<ProductFormula> CreateAsync(ProductFormula formula)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await ValidateFormulaAsync(context, formula);

        formula.Name = formula.Name.Trim();
        formula.Version = formula.Version.Trim();
        context.ProductFormulas.Add(formula);
        await context.SaveChangesAsync();
        return (await GetByIdAsync(formula.Id))!;
    }

    public async Task UpdateAsync(ProductFormula formula)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.ProductFormulas
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.Id == formula.Id)
            ?? throw new InvalidOperationException("Fórmula no encontrada.");

        await ValidateFormulaAsync(context, formula);

        existing.ProductId = formula.ProductId;
        existing.Name = formula.Name.Trim();
        existing.Version = formula.Version.Trim();
        existing.BaseYieldQuantity = formula.BaseYieldQuantity;
        existing.StandardLossMarginPercent = formula.StandardLossMarginPercent;
        existing.OverheadCostPerBatch = formula.OverheadCostPerBatch;
        existing.Instructions = formula.Instructions.Trim();
        existing.IsActive = formula.IsActive;

        context.FormulaItems.RemoveRange(existing.Items);
        existing.Items = formula.Items.Select(i => new FormulaItem
        {
            RawMaterialId = i.RawMaterialId,
            QuantityRequired = i.QuantityRequired,
            UnitOfMeasure = i.UnitOfMeasure
        }).ToList();

        await context.SaveChangesAsync();
    }

    public async Task<decimal> CalculateBaseMaterialCostAsync(int formulaId)
    {
        var formula = await GetByIdAsync(formulaId)
            ?? throw new InvalidOperationException("Fórmula no encontrada.");

        decimal total = 0m;
        foreach (var item in formula.Items)
        {
            var mat = item.RawMaterial ?? throw new InvalidOperationException("Materia prima no cargada.");
            decimal converted = UnitConverter.Convert(item.QuantityRequired, item.UnitOfMeasure, mat.UnitOfMeasure);
            total += converted * mat.AverageCost;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static async Task ValidateFormulaAsync(AppDbContext context, ProductFormula formula)
    {
        if (string.IsNullOrWhiteSpace(formula.Name))
            throw new ArgumentException("El nombre de la fórmula es obligatorio.");
        if (formula.BaseYieldQuantity <= 0)
            throw new ArgumentException("El rendimiento base debe ser mayor a cero.");
        if (formula.StandardLossMarginPercent < 0m || formula.StandardLossMarginPercent >= 100m)
            throw new ArgumentException("El margen de pérdida debe estar entre 0% y 99.99%.");
        if (formula.OverheadCostPerBatch < 0m)
            throw new ArgumentException("El costo indirecto por lote no puede ser negativo.");
        if (formula.Items == null || formula.Items.Count == 0)
            throw new InvalidOperationException("La fórmula debe contener al menos una materia prima.");
        if (!await context.Products.AnyAsync(p => p.Id == formula.ProductId))
            throw new InvalidOperationException("Producto terminado no encontrado.");
        if (formula.Items.GroupBy(i => i.RawMaterialId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("La fórmula no debe repetir la misma materia prima.");

        var ids = formula.Items.Select(i => i.RawMaterialId).Distinct().ToList();
        var materials = await context.RawMaterials.Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        foreach (var item in formula.Items)
        {
            if (item.QuantityRequired <= 0m)
                throw new ArgumentException("La cantidad requerida de cada material debe ser mayor a cero.");
            if (!materials.TryGetValue(item.RawMaterialId, out var mat))
                throw new InvalidOperationException($"Materia prima ID {item.RawMaterialId} no encontrada.");
            if (!UnitConverter.AreCompatible(item.UnitOfMeasure, mat.UnitOfMeasure))
                throw new InvalidOperationException(
                    $"La unidad {item.UnitOfMeasure} es incompatible con '{mat.Name}' ({mat.UnitOfMeasure}).");
        }
    }
}
