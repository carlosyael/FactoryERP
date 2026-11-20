using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class RawMaterialBatchService : IRawMaterialBatchService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public RawMaterialBatchService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<RawMaterialBatch>> GetBatchesAsync(int? rawMaterialId = null, bool onlyWithStock = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.RawMaterialBatches.AsNoTracking()
            .Include(b => b.RawMaterial)
            .Include(b => b.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .AsQueryable();

        if (rawMaterialId.HasValue) query = query.Where(b => b.RawMaterialId == rawMaterialId.Value);
        if (onlyWithStock) query = query.Where(b => b.QuantityRemaining > 0m);

        return await query
            .OrderBy(b => b.ExpirationDate ?? DateTime.MaxValue)
            .ThenBy(b => b.ReceivedDate)
            .ThenBy(b => b.Id)
            .ToListAsync();
    }

    public async Task<RawMaterialBatchRecallDto?> GetBatchTraceabilityAsync(int rawMaterialBatchId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var batch = await context.RawMaterialBatches.AsNoTracking()
            .Include(b => b.RawMaterial)
            .Include(b => b.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .FirstOrDefaultAsync(b => b.Id == rawMaterialBatchId);
        if (batch == null) return null;

        var allocations = await context.ProductionMaterialBatchAllocations.AsNoTracking()
            .Include(a => a.ProductionOrder).ThenInclude(o => o!.Product)
            .Where(a => a.RawMaterialBatchId == rawMaterialBatchId && a.QuantityAllocated > 0m)
            .OrderByDescending(a => a.ProductionOrderId)
            .ToListAsync();

        var orderIds = allocations.Select(a => a.ProductionOrderId).Distinct().ToList();
        var finishedByOrder = (await context.FinishedBatches.AsNoTracking()
            .Where(fb => fb.ProductionOrderId.HasValue && orderIds.Contains(fb.ProductionOrderId.Value))
            .ToListAsync())
            .GroupBy(fb => fb.ProductionOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var affected = allocations
            .Where(a => a.ProductionOrder != null)
            .Select(a =>
            {
                var o = a.ProductionOrder!;
                finishedByOrder.TryGetValue(o.Id, out var fb);
                return new RecallAffectedOrderDto(
                    o.Id, o.OrderNumber, o.BatchCode, o.Product?.Name ?? string.Empty,
                    o.Status, a.QuantityAllocated, fb?.Id, fb?.QuantityRemaining);
            })
            .ToList();

        return new RawMaterialBatchRecallDto(batch, affected);
    }
}
