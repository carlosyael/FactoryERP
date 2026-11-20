using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class BatchQualityService : IBatchQualityService
{
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
        if (!Enum.IsDefined(typeof(BatchQualityStatus), request.Status))
            throw new ArgumentOutOfRangeException(nameof(request), "Estado de calidad inválido.");
        if (request.Status == BatchQualityStatus.Rejected && string.IsNullOrWhiteSpace(request.Notes))
            throw new ArgumentException("Debe indicar el motivo del rechazo de calidad.", nameof(request));

        await InventoryConcurrencyGate.FinishedBatchGate.WaitAsync();
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var batch = await context.FinishedBatches.FirstOrDefaultAsync(b => b.Id == request.BatchId)
                ?? throw new InvalidOperationException("Lote terminado no encontrado.");

            if (request.Status == BatchQualityStatus.Approved && batch.IsExpired(DateTime.UtcNow.Date))
                throw new InvalidOperationException("No se puede aprobar para venta un lote que ya está vencido.");

            batch.QualityStatus = request.Status;
            batch.QualityNotes = ProductionPlannerHelper.TruncateNotes(request.Notes);
            batch.QualityCheckedAt = DateTime.UtcNow;
            batch.QualityCheckedBy = ProductionPlannerHelper.TruncateNotes(_currentUser?.UserName ?? "Sistema", 120);

            await context.SaveChangesAsync();
            return await context.FinishedBatches.AsNoTracking()
                .Include(b => b.Product)
                .FirstAsync(b => b.Id == batch.Id);
        }
        finally
        {
            InventoryConcurrencyGate.FinishedBatchGate.Release();
        }
    }

    public async Task<FinishedBatch> AdjustBatchQuantityAsync(AdjustBatchQuantityRequest request)
    {
        if (request.NewQuantity < 0)
            throw new ArgumentException("La existencia del lote no puede ser negativa.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("El motivo del ajuste o baja del lote es obligatorio.", nameof(request));

        await InventoryConcurrencyGate.FinishedBatchGate.WaitAsync();
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
                if (deltaQty == 0)
                    throw new InvalidOperationException("La nueva existencia es igual a la existencia actual del lote.");

                batch.QuantityRemaining = request.NewQuantity;
                await context.SaveChangesAsync();
                await tx.CommitAsync();
                snapshot = batch;
            }

            if (snapshot.UnitCost > 0m)
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
            InventoryConcurrencyGate.FinishedBatchGate.Release();
        }
    }
}
