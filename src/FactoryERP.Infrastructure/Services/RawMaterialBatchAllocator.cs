using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class RawMaterialBatchAllocator
{
    public static async Task<Dictionary<int, decimal>> GetExpiredStockByMaterialAsync(
        AppDbContext context, IEnumerable<int> rawMaterialIds, DateTime utcToday)
    {
        var ids = rawMaterialIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, decimal>();

        var rows = await context.RawMaterialBatches.AsNoTracking()
            .Where(b => ids.Contains(b.RawMaterialId) && b.QuantityRemaining > 0m && b.ExpirationDate.HasValue)
            .Select(b => new { b.RawMaterialId, b.QuantityRemaining, b.ExpirationDate })
            .ToListAsync();

        return rows
            .Where(b => b.ExpirationDate!.Value.Date < utcToday.Date)
            .GroupBy(b => b.RawMaterialId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.QuantityRemaining), 4, MidpointRounding.AwayFromZero));
    }

    public static async Task<string?> AllocateFefoAsync(
        AppDbContext context, ProductionOrder order, RawMaterial material, decimal quantityToConsume, DateTime utcToday)
    {
        var batches = await context.RawMaterialBatches
            .Where(b => b.RawMaterialId == material.Id && b.QuantityRemaining > 0m)
            .OrderBy(b => b.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(b => b.ReceivedDate)
            .ThenBy(b => b.Id)
            .ToListAsync();

        decimal expiredStock = batches.Where(b => b.IsExpired(utcToday)).Sum(b => b.QuantityRemaining);
        decimal usableTotal = Math.Round(material.CurrentStock - expiredStock, 4, MidpointRounding.AwayFromZero);
        if (usableTotal < quantityToConsume)
            throw new InvalidOperationException(
                $"Stock vigente insuficiente de '{material.Name}'. Requerido: {quantityToConsume:N2}, Vigente disponible: {Math.Max(0m, usableTotal):N2}.");

        decimal remaining = quantityToConsume;
        string? primaryBatchCode = null;
        foreach (var batch in batches.Where(b => b.IsUsable(utcToday)))
        {
            if (remaining <= 0m) break;
            decimal take = Math.Min(remaining, batch.QuantityRemaining);
            batch.QuantityRemaining = Math.Round(batch.QuantityRemaining - take, 4, MidpointRounding.AwayFromZero);
            remaining = Math.Round(remaining - take, 4, MidpointRounding.AwayFromZero);
            primaryBatchCode ??= batch.BatchCode;

            var existing = order.BatchAllocations.FirstOrDefault(a => a.RawMaterialBatchId == batch.Id);
            if (existing != null) existing.QuantityAllocated = Math.Round(existing.QuantityAllocated + take, 4, MidpointRounding.AwayFromZero);
            else order.BatchAllocations.Add(new ProductionMaterialBatchAllocation
            {
                ProductionOrder = order,
                RawMaterialId = material.Id,
                RawMaterialBatchId = batch.Id,
                QuantityAllocated = take
            });
        }

        return primaryBatchCode;
    }

    public static async Task<string?> RestoreAllocatedAsync(
        AppDbContext context, ProductionOrder order, RawMaterial material, decimal quantityToRestore)
    {
        var allocations = order.BatchAllocations
            .Where(a => a.RawMaterialId == material.Id && a.QuantityAllocated > 0m)
            .OrderByDescending(a => a.Id)
            .ToList();
        if (allocations.Count == 0) return null;

        var batchIds = allocations.Select(a => a.RawMaterialBatchId).Distinct().ToList();
        var batchMap = await context.RawMaterialBatches.Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);

        decimal remaining = quantityToRestore;
        string? primaryBatchCode = null;
        foreach (var alloc in allocations)
        {
            if (remaining <= 0m) break;
            if (!batchMap.TryGetValue(alloc.RawMaterialBatchId, out var batch)) continue;
            decimal restore = Math.Min(remaining, alloc.QuantityAllocated);
            alloc.QuantityAllocated = Math.Round(alloc.QuantityAllocated - restore, 4, MidpointRounding.AwayFromZero);
            batch.QuantityRemaining = Math.Round(batch.QuantityRemaining + restore, 4, MidpointRounding.AwayFromZero);
            remaining = Math.Round(remaining - restore, 4, MidpointRounding.AwayFromZero);
            primaryBatchCode ??= batch.BatchCode;
        }

        return primaryBatchCode;
    }
}
