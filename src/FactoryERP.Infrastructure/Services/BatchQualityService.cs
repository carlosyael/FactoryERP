using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class BatchQualityService : IBatchQualityService
{
    private static readonly SemaphoreSlim BatchGate = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ICurrentUserService? _currentUser;
    private readonly IManufacturingAccountingService _accounting;

    public BatchQualityService(
        IDbContextFactory<AppDbContext> contextFactory,
        IManufacturingAccountingService accounting,
        ICurrentUserService? currentUser = null)
    {
        _contextFactory = contextFactory;
        _accounting = accounting;
        _currentUser = currentUser;
    }

    public async Task<FinishedBatch> UpdateQualityStatusAsync(UpdateBatchQualityRequest request)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var batch = await context.FinishedBatches.FirstOrDefaultAsync(b => b.Id == request.BatchId)
            ?? throw new InvalidOperationException("Lote terminado no encontrado.");

        batch.QualityStatus = request.Status;
        batch.QualityNotes = ProductionPlannerHelper.TruncateNotes(request.Notes);
        batch.QualityCheckedAt = DateTime.UtcNow;
        batch.QualityCheckedBy = ProductionPlannerHelper.TruncateNotes(_currentUser?.UserName ?? "Sistema", 120);

        await context.SaveChangesAsync();
        return await context.FinishedBatches.AsNoTracking()
            .Include(b => b.Product)
            .FirstAsync(b => b.Id == batch.Id);
    }

    public async Task<FinishedBatch> AdjustBatchQuantityAsync(AdjustBatchQuantityRequest request)
    {
        if (request.NewQuantity < 0)
            throw new ArgumentException("La existencia del lote no puede ser negativa.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("El motivo del ajuste o baja del lote es obligatorio.", nameof(request));

        await BatchGate.WaitAsync();
        try
        {
            FinishedBatch snapshot;
            int deltaQty;

            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                using var tx = await context.Database.BeginTransactionAsync();
                var batch = await context.FinishedBatches.FirstOrDefaultAsync(b => b.Id == request.BatchId)
                    ?? throw new InvalidOperationException("Lote terminado no encontrado.");

                deltaQty = request.NewQuantity - batch.QuantityRemaining;
                batch.QuantityRemaining = request.NewQuantity;
                await context.SaveChangesAsync();
                await tx.CommitAsync();
                snapshot = batch;
            }

            if (deltaQty != 0 && snapshot.UnitCost > 0m)
            {
                decimal absValue = Math.Round(Math.Abs(deltaQty) * snapshot.UnitCost, 2, MidpointRounding.AwayFromZero);
                if (absValue > 0m)
                {
                    await _accounting.PostFinishedBatchAdjustmentAsync(
                        snapshot, deltaQty, ProductionPlannerHelper.TruncateNotes(request.Reason), _currentUser?.UserId);
                }
            }

            await using var readCtx = await _contextFactory.CreateDbContextAsync();
            return await readCtx.FinishedBatches.AsNoTracking()
                .Include(b => b.Product)
                .FirstAsync(b => b.Id == snapshot.Id);
        }
        finally
        {
            BatchGate.Release();
        }
    }
}
