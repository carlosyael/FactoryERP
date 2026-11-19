using System.Security.Claims;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class AppMenuServiceTests
{
    [Fact]
    public async Task GetAuthorizedMenusAsync_FiltersMenusByRoleAndPermissionClaims()
    {
        using var factory = DbContextSqliteFactory.Create();
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.AppMenus.AddRange(
                new AppMenu { Title = "Producción", Url = "production", RequiredPermission = Permissions.ProductionManage, DisplayOrder = 1 },
                new AppMenu { Title = "Ventas", Url = "sales", RequiredPermission = Permissions.SalesManage, DisplayOrder = 2 },
                new AppMenu { Title = "Configuración", Url = "settings", RequiredPermission = Permissions.SettingsManage, DisplayOrder = 3 });
            await ctx.SaveChangesAsync();
        }

        var service = new AppMenuService(factory);

        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, UserRoles.Admin)], "test"));
        (await service.GetAuthorizedMenusAsync(admin)).Should().HaveCount(3);

        var prodUser = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, UserRoles.Produccion), new Claim("Permission", Permissions.ProductionManage)], "test"));
        var prodMenus = await service.GetAuthorizedMenusAsync(prodUser);
        prodMenus.Should().ContainSingle(m => m.Url == "production");
    }
}
