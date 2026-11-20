using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
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
        await FormulaValidationHelper.ValidateFormulaAsync(context, formula);

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

        bool hasOrders = await context.ProductionOrders.AnyAsync(
            o => o.ProductFormulaId == formula.Id && o.Status != ProductionOrderStatus.Cancelled);
        if (hasOrders)
        {
            if (FormulaValidationHelper.HasStructuralChanges(existing, formula))
                throw new InvalidOperationException(
                    "Esta fórmula ya tiene órdenes de producción vinculadas. Solo puedes editar instrucciones o estado activo; usa 'Nueva versión' para cambiar receta, rendimiento o costos.");

            existing.Instructions = (formula.Instructions ?? string.Empty).Trim();
            existing.IsActive = formula.IsActive;
            await context.SaveChangesAsync();
            return;
        }

        await FormulaValidationHelper.ValidateFormulaAsync(context, formula);

        existing.ProductId = formula.ProductId;
        existing.Name = formula.Name.Trim();
        existing.Version = formula.Version.Trim();
        existing.BaseYieldQuantity = formula.BaseYieldQuantity;
        existing.StandardLossMarginPercent = formula.StandardLossMarginPercent;
        existing.OverheadCostPerBatch = formula.OverheadCostPerBatch;
        existing.Instructions = (formula.Instructions ?? string.Empty).Trim();
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

    public async Task<ProductFormula> CloneAsNewVersionAsync(int formulaId, string? newVersion = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var source = await context.ProductFormulas.AsNoTracking()
            .Include(f => f.Items)
            .FirstOrDefaultAsync(f => f.Id == formulaId)
            ?? throw new InvalidOperationException("Fórmula no encontrada.");

        var versionSet = (await context.ProductFormulas
            .Where(f => f.ProductId == source.ProductId)
            .Select(f => f.Version)
            .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        string targetVersion = string.IsNullOrWhiteSpace(newVersion)
            ? FormulaValidationHelper.ComputeNextVersion(source.Version, versionSet)
            : newVersion.Trim();

        if (versionSet.Contains(targetVersion))
            throw new InvalidOperationException($"Ya existe la versión '{targetVersion}' para este producto.");

        var clone = new ProductFormula
        {
            ProductId = source.ProductId,
            Name = source.Name,
            Version = targetVersion,
            BaseYieldQuantity = source.BaseYieldQuantity,
            StandardLossMarginPercent = source.StandardLossMarginPercent,
            OverheadCostPerBatch = source.OverheadCostPerBatch,
            Instructions = source.Instructions,
            IsActive = true,
            Items = source.Items.Select(i => new FormulaItem
            {
                RawMaterialId = i.RawMaterialId,
                QuantityRequired = i.QuantityRequired,
                UnitOfMeasure = i.UnitOfMeasure
            }).ToList()
        };

        context.ProductFormulas.Add(clone);
        await context.SaveChangesAsync();
        return (await GetByIdAsync(clone.Id))!;
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
}
