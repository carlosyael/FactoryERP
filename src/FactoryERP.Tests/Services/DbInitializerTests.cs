using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryERP.Tests.Services;

public class DbInitializerTests
{
    [Fact]
    public async Task InitializeAsync_SeedsFactoryErpDefaultsAndRepairsLegacyIconsIdempotently()
    {
        using var factory = DbContextSqliteFactory.Create(seedSettings: false);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(factory);
        services.AddScoped(_ => factory.CreateDbContext());
        services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        await using var sp = services.BuildServiceProvider();

        // Pre-cargar datos legados (placeholder anterior e íconos no soportados)
        await using (var setupCtx = await factory.CreateDbContextAsync())
        {
            setupCtx.SystemSettings.Add(new SystemSetting { Key = SettingKeys.BusinessName, Value = "Mi Fábrica Capilar" });
            setupCtx.AppMenus.AddRange(
                new AppMenu { Title = "Fórmulas y Recetas", Url = "formulas", Icon = "bi-flask", DisplayOrder = 20 },
                new AppMenu { Title = "Balanza de Comprobación", Url = "accounting/trial-balance", Icon = "bi-scale", DisplayOrder = 120 });
            await setupCtx.SaveChangesAsync();
        }

        await using (var scope = sp.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DbInitializer.InitializeAsync(scope.ServiceProvider, ctx, seedDemoData: true);
            await DbInitializer.InitializeAsync(scope.ServiceProvider, ctx, seedDemoData: true);
        }

        await using var verifyCtx = await factory.CreateDbContextAsync();
        var businessSetting = await verifyCtx.SystemSettings.FirstAsync(s => s.Key == SettingKeys.BusinessName);
        businessSetting.Value.Should().Be("FactoryERP");

        var menus = await verifyCtx.AppMenus.ToListAsync();
        menus.Should().NotContain(m => m.Icon == "bi-flask" || m.Icon == "bi-scale");
        menus.First(m => m.Url == "formulas").Icon.Should().Be("bi-eyedropper");
        menus.First(m => m.Url == "accounting/trial-balance").Icon.Should().Be("bi-calculator");
        (await verifyCtx.Products.CountAsync()).Should().Be(2);
    }
}
