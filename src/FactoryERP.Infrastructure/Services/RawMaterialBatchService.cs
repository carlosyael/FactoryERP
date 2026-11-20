using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class RawMaterialBatchService : IRawMaterialBatchService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IManufacturingAccountingService? _accounting;
    private readonly ICurrentUserService? _currentUser;

    public RawMaterialBatchService(
        IDbContextFactory<AppDbContext> contextFactory,
        IManufacturingAccountingService? accounting = null,
        ICurrentUserService? currentUser = null)
    {
        _contextFactory = contextFactory;
        _accounting = accounting;
        _currentUser = currentUser;
    }

    public async Task<List<RawMaterialBatch>> GetBatchesAsync(int? rawMaterialId = null, bool onlyWithStock = false)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.RawMaterialBatches.AsNoTracking()
            .Include(b => b.RawMaterial)
            .Include(b => b.SourceProductionOrder)
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
            .Include(b => b.RawMaterial).Include(b => b.SourceProductionOrder)
            .Include(b => b.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .FirstOrDefaultAsync(b => b.Id == rawMaterialBatchId);
        if (batch == null) return null;

        var rawAffected = new List<(ProductionOrder Order, decimal Qty, string? ViaBulk, string Unit)>();
        var queue = new Queue<(int BatchId, string? ViaBulk)>([(rawMaterialBatchId, null)]);
        var visitedBatches = new HashSet<int> { rawMaterialBatchId };

        while (queue.Count > 0)
        {
            var (curBatchId, viaBulk) = queue.Dequeue();
            var allocs = await context.ProductionMaterialBatchAllocations.AsNoTracking()
                .Include(a => a.RawMaterial)
                .Include(a => a.ProductionOrder).ThenInclude(o => o!.Product)
                .Include(a => a.ProductionOrder).ThenInclude(o => o!.OutputRawMaterial)
                .Where(a => a.RawMaterialBatchId == curBatchId && a.QuantityAllocated > 0m)
                .OrderByDescending(a => a.ProductionOrderId)
                .ToListAsync();

            foreach (var a in allocs.Where(a => a.ProductionOrder != null))
            {
                string unit = a.RawMaterial != null ? UnitConverter.GetShortLabel(a.RawMaterial.UnitOfMeasure) : "ud";
                rawAffected.Add((a.ProductionOrder!, a.QuantityAllocated, viaBulk, unit));
                if (!a.ProductionOrder!.IsSemiFinishedOutput) continue;
                var childBatches = await context.RawMaterialBatches.AsNoTracking()
                    .Where(b => b.SourceProductionOrderId == a.ProductionOrder.Id)
                    .Select(b => new { b.Id, b.BatchCode }).ToListAsync();
                foreach (var cb in childBatches)
                    if (visitedBatches.Add(cb.Id)) queue.Enqueue((cb.Id, cb.BatchCode));
            }
        }

        var orderIds = rawAffected.Select(x => x.Order.Id).Distinct().ToList();
        var finishedByOrder = (await context.FinishedBatches.AsNoTracking()
            .Where(fb => fb.ProductionOrderId.HasValue && orderIds.Contains(fb.ProductionOrderId.Value))
            .ToListAsync())
            .GroupBy(fb => fb.ProductionOrderId!.Value).ToDictionary(g => g.Key, g => g.First());

        var affected = rawAffected.Select(x =>
        {
            finishedByOrder.TryGetValue(x.Order.Id, out var fb);
            return new RecallAffectedOrderDto(
                x.Order.Id, x.Order.OrderNumber, x.Order.BatchCode, x.Order.OutputDisplayName,
                x.Order.Status, x.Qty, fb?.Id, fb?.QuantityRemaining, x.Order.IsSemiFinishedOutput, x.ViaBulk, x.Unit);
        }).ToList();

        return new RawMaterialBatchRecallDto(batch, affected);
    }

    public async Task<RawMaterialBatch> AdjustBatchQuantityAsync(AdjustRawMaterialBatchRequest request)
    {
        if (request.NewQuantity < 0m)
            throw new ArgumentException("La nueva cantidad del lote no puede ser negativa.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("El motivo del ajuste es obligatorio.", nameof(request));

        decimal delta;
        string batchCode;
        RawMaterial materialSnapshot;
        RawMaterialBatch updatedBatch;

        await InventoryConcurrencyGate.RawMaterialGate.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            using var tx = await context.Database.BeginTransactionAsync();

            var batch = await context.RawMaterialBatches
                .Include(b => b.RawMaterial)
                .FirstOrDefaultAsync(b => b.Id == request.BatchId)
                ?? throw new InvalidOperationException("Lote de materia prima no encontrado.");
            var material = batch.RawMaterial
                ?? throw new InvalidOperationException("Materia prima asociada no encontrada.");

            decimal roundedNew = Math.Round(request.NewQuantity, 4, MidpointRounding.AwayFromZero);
            delta = Math.Round(roundedNew - batch.QuantityRemaining, 4, MidpointRounding.AwayFromZero);
            if (delta == 0m)
                throw new InvalidOperationException("La nueva cantidad debe ser diferente a la existencia actual del lote.");

            decimal newStock = Math.Round(material.CurrentStock + delta, 4, MidpointRounding.AwayFromZero);
            if (newStock < 0m)
                throw new InvalidOperationException("El ajuste dejaría el inventario total de la materia prima por debajo de cero.");

            batch.QuantityRemaining = roundedNew;
            material.CurrentStock = newStock;
            batchCode = batch.BatchCode;

            InventoryKardexRecorder.RecordRawMaterial(
                context, material, InventoryMovementType.RawMaterialAdjustment, delta, material.AverageCost,
                batch.BatchCode, request.Reason.Trim(), _currentUser?.UserName, batchCode: batch.BatchCode);

            await context.SaveChangesAsync();
            await tx.CommitAsync();
            materialSnapshot = material;
            updatedBatch = batch;
        }
        finally
        {
            InventoryConcurrencyGate.RawMaterialGate.Release();
        }

        if (_accounting != null && materialSnapshot.AverageCost > 0m)
        {
            decimal absValue = Math.Round(Math.Abs(delta) * materialSnapshot.AverageCost, 2, MidpointRounding.AwayFromZero);
            if (absValue > 0m)
            {
                await _accounting.PostRawMaterialAdjustmentAsync(
                    materialSnapshot, delta, $"Lote {batchCode}: {request.Reason.Trim()}", _currentUser?.UserId);
            }
        }

        return updatedBatch;
    }
}
