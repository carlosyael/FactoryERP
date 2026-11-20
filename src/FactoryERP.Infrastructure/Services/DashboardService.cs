using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DashboardService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DashboardKpiDto> GetKpisAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var products = await context.Products
            .AsNoTracking()
            .Include(p => p.FinishedBatches)
            .Where(p => p.IsActive)
            .ToListAsync();

        var materials = await context.RawMaterials
            .AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync();

        int activeOrders = await context.ProductionOrders
            .CountAsync(o => o.Status == ProductionOrderStatus.Draft || o.Status == ProductionOrderStatus.InProgress);

        DateTime monthStart = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        decimal monthlySales = (await context.Sales
            .Where(s => s.SaleDate >= monthStart)
            .Select(s => s.Total)
            .ToListAsync()).Sum();

        decimal totalAr = (await context.Customers
            .Where(c => c.IsActive)
            .Select(c => c.CurrentBalance)
            .ToListAsync()).Sum();

        decimal totalAp = (await context.Suppliers
            .Where(s => s.IsActive)
            .Select(s => s.CurrentBalance)
            .ToListAsync()).Sum();

        var completedLosses = await context.ProductionOrders
            .Where(o => o.Status == ProductionOrderStatus.Completed && o.ActualLossPercent.HasValue)
            .Select(o => o.ActualLossPercent!.Value)
            .ToListAsync();

        decimal avgLoss = completedLosses.Count > 0
            ? Math.Round(completedLosses.Average(), 2, MidpointRounding.AwayFromZero)
            : 0m;

        DateTime utcToday = DateTime.UtcNow.Date;
        int lowProducts = products.Count(p => p.FinishedBatches.Where(b => b.IsSellable(utcToday)).Sum(b => b.QuantityRemaining) <= p.MinimumStockLevel);
        decimal finishedValue = products.Sum(p => p.FinishedBatches.Sum(b => b.QuantityRemaining * b.UnitCost));
        decimal rawValue = materials.Sum(m => m.CurrentStock * m.AverageCost);

        return new DashboardKpiDto(
            products.Count,
            materials.Count(m => m.IsLowStock),
            lowProducts,
            activeOrders,
            monthlySales,
            totalAr,
            totalAp,
            avgLoss,
            Math.Round(finishedValue, 2, MidpointRounding.AwayFromZero),
            Math.Round(rawValue, 2, MidpointRounding.AwayFromZero));
    }
}
