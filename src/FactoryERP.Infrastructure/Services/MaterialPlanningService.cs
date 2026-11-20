using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class MaterialPlanningService : IMaterialPlanningService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public MaterialPlanningService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    internal static async Task<Dictionary<int, decimal>> GetDraftReservationsAsync(
        AppDbContext context, IEnumerable<int>? rawMaterialIds = null)
    {
        var query = context.ProductionOrderMaterials.AsNoTracking()
            .Where(m => m.ProductionOrder != null && m.ProductionOrder.Status == ProductionOrderStatus.Draft);

        var ids = rawMaterialIds?.Distinct().ToList();
        if (ids is { Count: > 0 })
            query = query.Where(m => ids.Contains(m.RawMaterialId));

        var rows = await query
            .Select(m => new { m.RawMaterialId, m.PlannedQuantity })
            .ToListAsync();

        return rows
            .GroupBy(r => r.RawMaterialId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.PlannedQuantity), 4, MidpointRounding.AwayFromZero));
    }

    public async Task<List<MaterialPlanningSuggestionDto>> GetPurchaseSuggestionsAsync(
        bool onlyWithShortage = true, bool excludeManufacturedInHouse = true)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var materials = await context.RawMaterials.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();

        if (materials.Count == 0) return [];

        var matIds = materials.Select(m => m.Id).ToList();
        DateTime utcToday = DateTime.UtcNow.Date;
        var expiredByMat = await RawMaterialBatchAllocator.GetExpiredStockByMaterialAsync(context, matIds, utcToday);
        var reservedByMat = await GetDraftReservationsAsync(context, matIds);
        var inHouseIds = (await context.ProductFormulas.AsNoTracking()
            .Where(f => f.IsActive && f.OutputRawMaterialId.HasValue)
            .Select(f => f.OutputRawMaterialId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        var suggestions = new List<MaterialPlanningSuggestionDto>();
        foreach (var mat in materials)
        {
            bool isInHouse = mat.Category == RawMaterialCategory.SemiFinished || inHouseIds.Contains(mat.Id);
            if (excludeManufacturedInHouse && isInHouse) continue;

            decimal expired = expiredByMat.GetValueOrDefault(mat.Id, 0m);
            decimal usable = Math.Max(0m, Math.Round(mat.CurrentStock - expired, 4, MidpointRounding.AwayFromZero));
            decimal reserved = reservedByMat.GetValueOrDefault(mat.Id, 0m);
            decimal netAvailable = Math.Round(usable - reserved, 4, MidpointRounding.AwayFromZero);
            decimal target = Math.Round(reserved + mat.MinimumStockLevel, 4, MidpointRounding.AwayFromZero);
            decimal suggested = Math.Max(0m, Math.Round(target - usable, 4, MidpointRounding.AwayFromZero));
            if (onlyWithShortage && suggested <= 0m) continue;

            decimal unitCost = mat.LastPurchaseCost > 0m ? mat.LastPurchaseCost : mat.AverageCost;
            decimal subTotal = Math.Round(suggested * unitCost, 2, MidpointRounding.AwayFromZero);

            suggestions.Add(new MaterialPlanningSuggestionDto(
                mat.Id, mat.Sku, mat.Name, mat.UnitOfMeasure,
                mat.CurrentStock, expired, usable, reserved, netAvailable,
                mat.MinimumStockLevel, suggested, unitCost, subTotal, isInHouse));
        }

        return suggestions
            .OrderByDescending(s => s.SuggestedPurchaseQuantity > 0m)
            .ThenByDescending(s => s.EstimatedPurchaseSubTotal)
            .ThenBy(s => s.Name)
            .ToList();
    }

    public async Task<List<MaterialPlanningSuggestionDto>> GetShortagesForFormulaAsync(
        int formulaId, int plannedQuantity, decimal? overrideLossMarginPercent = null)
    {
        if (plannedQuantity <= 0) return [];
        await using var context = await _contextFactory.CreateDbContextAsync();
        var formula = await context.ProductFormulas.AsNoTracking()
            .Include(f => f.Product)
            .Include(f => f.OutputRawMaterial)
            .Include(f => f.Items).ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(f => f.Id == formulaId)
            ?? throw new InvalidOperationException("Fórmula no encontrada.");

        var matIds = formula.Items.Select(i => i.RawMaterialId).Distinct().ToList();
        var expired = await RawMaterialBatchAllocator.GetExpiredStockByMaterialAsync(context, matIds, DateTime.UtcNow.Date);
        var reserved = await GetDraftReservationsAsync(context, matIds);
        var materialsById = formula.Items.Where(i => i.RawMaterial != null)
            .Select(i => i.RawMaterial!).DistinctBy(m => m.Id).ToDictionary(m => m.Id);
        var inHouseIds = (await context.ProductFormulas.AsNoTracking()
            .Where(f => f.IsActive && f.OutputRawMaterialId.HasValue)
            .Select(f => f.OutputRawMaterialId!.Value)
            .Distinct()
            .ToListAsync()).ToHashSet();

        decimal loss = overrideLossMarginPercent ?? formula.StandardLossMarginPercent;
        var sim = ProductionPlannerHelper.BuildSimulation(formula, plannedQuantity, loss, expired, reserved);
        var result = new List<MaterialPlanningSuggestionDto>();

        foreach (var req in sim.Materials.Where(m => !m.HasSufficientStock))
        {
            var mat = materialsById[req.RawMaterialId];
            decimal shortage = Math.Max(0m, Math.Round(req.RequiredQuantity - req.AvailableStock, 4, MidpointRounding.AwayFromZero));
            decimal exp = expired.GetValueOrDefault(mat.Id, 0m);
            decimal unitCost = mat.LastPurchaseCost > 0m ? mat.LastPurchaseCost : mat.AverageCost;
            result.Add(new MaterialPlanningSuggestionDto(
                mat.Id, mat.Sku, mat.Name, mat.UnitOfMeasure,
                mat.CurrentStock, exp, req.AvailableStock, req.ReservedByDrafts,
                Math.Round(req.AvailableStock - req.ReservedByDrafts, 4, MidpointRounding.AwayFromZero),
                mat.MinimumStockLevel, shortage, unitCost,
                Math.Round(shortage * unitCost, 2, MidpointRounding.AwayFromZero),
                mat.Category == RawMaterialCategory.SemiFinished || inHouseIds.Contains(mat.Id)));
        }

        return result;
    }
}
