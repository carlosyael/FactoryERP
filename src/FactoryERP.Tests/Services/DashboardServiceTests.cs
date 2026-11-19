using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetKpisAsync_AggregatesInventoryProductionAndBalances()
    {
        using var factory = DbContextSqliteFactory.Create();
        var materialService = new RawMaterialService(factory);
        var productService = new ProductService(factory);
        var dashboard = new DashboardService(factory);

        await materialService.CreateAsync(new RawMaterial
        {
            Sku = "MP-LOW-1",
            Name = "Aceite Low",
            UnitOfMeasure = UnitOfMeasure.Milliliter,
            CurrentStock = 100m,
            MinimumStockLevel = 200m,
            AverageCost = 5m
        });

        var p = await productService.CreateAsync(new Product
        {
            Sku = "CAP-D-1",
            Name = "Shampoo",
            MinimumStockLevel = 10,
            RetailPrice = 500m,
            Cost = 150m
        });

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.FinishedBatches.Add(new FinishedBatch
            {
                ProductId = p.Id,
                BatchCode = "LOT-D1",
                InitialQuantity = 4,
                QuantityRemaining = 4,
                UnitCost = 150m
            });
            await ctx.SaveChangesAsync();
        }

        var kpis = await dashboard.GetKpisAsync();
        kpis.ActiveProductsCount.Should().Be(1);
        kpis.LowStockRawMaterialsCount.Should().Be(1);
        kpis.LowStockProductsCount.Should().Be(1); // 4 <= 10
        kpis.RawMaterialInventoryValue.Should().Be(500m); // 100 * 5
        kpis.FinishedInventoryValue.Should().Be(600m); // 4 * 150
    }
}
