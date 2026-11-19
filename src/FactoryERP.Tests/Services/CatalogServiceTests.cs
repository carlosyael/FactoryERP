using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class CatalogServiceTests
{
    [Fact]
    public async Task PublicCatalog_HidesCostsAndPrivateProductsAndRespectsGlobalToggle()
    {
        using var factory = DbContextSqliteFactory.Create();
        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var productService = new ProductService(factory);
        var catalogService = new CatalogService(factory, settings);

        var visible = await productService.CreateAsync(new Product
        {
            Sku = "PUB-01",
            Name = "Crema Rizos",
            Line = "Rizos",
            Cost = 180m,
            RetailPrice = 700m,
            ShowInPublicCatalog = true,
            IsActive = true
        });

        await productService.CreateAsync(new Product
        {
            Sku = "PRIV-01",
            Name = "Fórmula Privada",
            Line = "Reparación",
            Cost = 150m,
            RetailPrice = 600m,
            ShowInPublicCatalog = false,
            IsActive = true
        });

        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.FinishedBatches.Add(new FinishedBatch
            {
                ProductId = visible.Id,
                BatchCode = "LOT-CAT-1",
                InitialQuantity = 12,
                QuantityRemaining = 12,
                UnitCost = 180m
            });
            await ctx.SaveChangesAsync();
        }

        var internalItems = await catalogService.GetInternalCatalogAsync();
        internalItems.Should().HaveCount(2);
        internalItems.First(i => i.ProductId == visible.Id).Cost.Should().Be(180m);

        var publicItems = await catalogService.GetPublicCatalogAsync();
        publicItems.Should().ContainSingle(p => p.Sku == "PUB-01");
        publicItems[0].AvailableStock.Should().Be(12);

        await settings.UpdateSettingAsync(SettingKeys.CatalogPublicEnabled, "false");
        var disabledPublic = await catalogService.GetPublicCatalogAsync();
        disabledPublic.Should().BeEmpty();
    }
}
