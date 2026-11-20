using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class ProductionOrderService : IProductionOrderService
{
    private static readonly SemaphoreSlim ProductionGate = new(1, 1);
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ISystemSettingService _settings;
    private readonly IManufacturingAccountingService _accounting;

    public ProductionOrderService(
        IDbContextFactory<AppDbContext> contextFactory,
        ISystemSettingService settings,
        IManufacturingAccountingService accounting)
    {
        _contextFactory = contextFactory;
        _settings = settings;
        _accounting = accounting;
    }

    public async Task<List<ProductionOrder>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProductionOrders.AsNoTracking()
            .Include(o => o.Product).Include(o => o.ProductFormula)
            .Include(o => o.Materials).ThenInclude(m => m.RawMaterial)
            .OrderByDescending(o => o.Id).ToListAsync();
    }

    public async Task<ProductionOrder?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.ProductionOrders.AsNoTracking()
            .Include(o => o.Product).Include(o => o.ProductFormula)
            .Include(o => o.Materials).ThenInclude(m => m.RawMaterial)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<ProductionSimulationDto> SimulateAsync(
        int formulaId, int plannedQuantity, decimal? overrideLossMarginPercent = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var formula = await context.ProductFormulas.AsNoTracking()
            .Include(f => f.Product)
            .Include(f => f.Items).ThenInclude(i => i.RawMaterial)
            .FirstOrDefaultAsync(f => f.Id == formulaId)
            ?? throw new InvalidOperationException("Fórmula no encontrada.");

        decimal loss = overrideLossMarginPercent ?? formula.StandardLossMarginPercent;
        return ProductionPlannerHelper.BuildSimulation(formula, plannedQuantity, loss);
    }

    public async Task<ProductionOrder> CreateDraftAsync(CreateProductionOrderRequest request)
    {
        var sim = await SimulateAsync(request.ProductFormulaId, request.PlannedQuantity, request.OverrideLossMarginPercent);
        string token = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        string batch = string.IsNullOrWhiteSpace(request.BatchCode)
            ? $"LOT-{DateTime.UtcNow:yyyyMMdd}-{token}"
            : request.BatchCode.Trim().ToUpperInvariant();

        var order = ProductionPlannerHelper.BuildDraftOrder(sim, request, batch, token);

        await using var context = await _contextFactory.CreateDbContextAsync();
        bool batchExists = await context.FinishedBatches.AnyAsync(b => b.BatchCode == batch)
            || await context.ProductionOrders.AnyAsync(o => o.BatchCode == batch && o.Status != ProductionOrderStatus.Cancelled);
        if (batchExists)
            throw new InvalidOperationException($"Ya existe una orden o lote registrado con el código '{batch}'.");

        context.ProductionOrders.Add(order);
        await context.SaveChangesAsync();
        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<ProductionOrder> UpdateDraftAsync(UpdateProductionOrderRequest request)
    {
        var sim = await SimulateAsync(request.ProductFormulaId, request.PlannedQuantity, request.OverrideLossMarginPercent);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var order = await context.ProductionOrders.Include(o => o.Materials)
            .FirstOrDefaultAsync(o => o.Id == request.ProductionOrderId)
            ?? throw new InvalidOperationException("Orden de producción no encontrada.");

        if (order.Status != ProductionOrderStatus.Draft)
            throw new InvalidOperationException("Solo las órdenes en borrador pueden editarse.");

        string batch = string.IsNullOrWhiteSpace(request.BatchCode)
            ? order.BatchCode
            : request.BatchCode.Trim().ToUpperInvariant();

        bool batchExists = await context.FinishedBatches.AnyAsync(b => b.BatchCode == batch)
            || await context.ProductionOrders.AnyAsync(
                o => o.Id != order.Id && o.BatchCode == batch && o.Status != ProductionOrderStatus.Cancelled);
        if (batchExists)
            throw new InvalidOperationException($"Ya existe una orden o lote registrado con el código '{batch}'.");

        context.RemoveRange(order.Materials);
        ProductionPlannerHelper.ApplyDraftUpdate(order, sim, request, batch);
        await context.SaveChangesAsync();
        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<ProductionOrder> StartProductionAsync(int orderId)
    {
        await ProductionGate.WaitAsync();
        try
        {
            ProductionOrder order;
            decimal matCost;
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                using var tx = await context.Database.BeginTransactionAsync();
                order = await context.ProductionOrders
                    .Include(o => o.ProductFormula)
                    .Include(o => o.Materials)
                    .FirstOrDefaultAsync(o => o.Id == orderId)
                    ?? throw new InvalidOperationException("Orden de producción no encontrada.");

                matCost = await ProductionExecutionHelper.StartOrderAsync(context, order);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }

            if (matCost > 0m) await _accounting.PostProductionStartAsync(order, matCost);
            return (await GetByIdAsync(order.Id))!;
        }
        finally
        {
            ProductionGate.Release();
        }
    }

    public async Task<ProductionOrder> CompleteProductionAsync(CompleteProductionRequest request)
    {
        decimal tolerance = await _settings.GetDecimalAsync(SettingKeys.ProductionAbnormalLossTolerancePercent, 2m);
        bool requireQc = await _settings.GetBoolAsync(SettingKeys.ProductionRequireQualityApproval, false);
        await ProductionGate.WaitAsync();
        try
        {
            ProductionOrder order;
            decimal wipCleared;
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                using var tx = await context.Database.BeginTransactionAsync();
                order = await context.ProductionOrders.Include(o => o.Materials)
                    .FirstOrDefaultAsync(o => o.Id == request.ProductionOrderId)
                    ?? throw new InvalidOperationException("Orden de producción no encontrada.");

                wipCleared = await ProductionExecutionHelper.CompleteOrderAsync(context, order, request, tolerance, requireQc);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }

            if (wipCleared > 0m || order.ActualMaterialCost + order.ActualOverheadCost > 0m)
                await _accounting.PostProductionCompletionAsync(order, wipMaterialCleared: wipCleared);
            return (await GetByIdAsync(order.Id))!;
        }
        finally
        {
            ProductionGate.Release();
        }
    }

    public async Task<ProductionOrder> CancelProductionAsync(int orderId, string reason)
    {
        await ProductionGate.WaitAsync();
        try
        {
            ProductionOrder order;
            bool hadWip;
            await using (var context = await _contextFactory.CreateDbContextAsync())
            {
                using var tx = await context.Database.BeginTransactionAsync();
                order = await context.ProductionOrders
                    .Include(o => o.ProductFormula)
                    .Include(o => o.Materials)
                    .FirstOrDefaultAsync(o => o.Id == orderId)
                    ?? throw new InvalidOperationException("Orden de producción no encontrada.");

                hadWip = await ProductionExecutionHelper.CancelOrderAsync(context, order, reason);
                await context.SaveChangesAsync();
                await tx.CommitAsync();
            }

            if (hadWip && order.ActualMaterialCost > 0m)
                await _accounting.PostProductionCancellationAsync(order);
            return (await GetByIdAsync(order.Id))!;
        }
        finally
        {
            ProductionGate.Release();
        }
    }
}
