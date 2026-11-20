using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryERP.Tests.Services;

public class UserManagementServiceTests
{
    private static ServiceProvider BuildProvider(DbContextSqliteFactory factory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(factory);
        services.AddScoped(_ => factory.CreateDbContext());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddScoped<IRoleManagementService, RoleManagementService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateUpdateResetPasswordAndCustomRoleAssignment_WorksEndToEnd()
    {
        using var factory = DbContextSqliteFactory.Create(seedSettings: false);
        await using var sp = BuildProvider(factory);
        await using var scope = sp.CreateAsyncScope();

        var roleService = scope.ServiceProvider.GetRequiredService<IRoleManagementService>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await roleService.CreateRoleAsync("Calidad Capilar", [Permissions.RawMaterialsView, Permissions.FormulasManage]);
        await roleService.CreateRoleAsync(UserRoles.Ventas, [Permissions.SalesManage]);

        var created = await userService.CreateUserAsync(new CreateUserRequest(
            "Ana Gómez", "ana@erp.local", "ClaveFuerte2026!", ["Calidad Capilar"]));
        created.Roles.Should().ContainSingle().Which.Should().Be("Calidad Capilar");

        var updated = await userService.UpdateUserAsync(new UpdateUserRequest(
            created.Id, "Ana M. Gómez", ["Calidad Capilar", UserRoles.Ventas]));
        updated.FullName.Should().Be("Ana M. Gómez");
        updated.Roles.Should().BeEquivalentTo(["Calidad Capilar", UserRoles.Ventas]);

        await userService.ResetPasswordAsync(created.Id, "NuevaClave2026!");
        var dbUser = (await userManager.FindByIdAsync(created.Id))!;
        (await userManager.CheckPasswordAsync(dbUser, "NuevaClave2026!")).Should().BeTrue();
        (await userManager.CheckPasswordAsync(dbUser, "ClaveFuerte2026!")).Should().BeFalse();

        Func<Task> weakReset = () => userService.ResetPasswordAsync(created.Id, "123");
        await weakReset.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LastActiveAdminProtection_BlocksDeactivationAndRoleRemovalUntilSecondAdminExists()
    {
        using var factory = DbContextSqliteFactory.Create(seedSettings: false);
        await using var sp = BuildProvider(factory);
        await using var scope = sp.CreateAsyncScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
        await roleManager.CreateAsync(new IdentityRole(UserRoles.Ventas));

        var admin1 = await userService.CreateUserAsync(new CreateUserRequest(
            "Admin Principal", "admin1@erp.local", "AdminSegura2026!", [UserRoles.Admin]));

        Func<Task> toggleLast = () => userService.ToggleActiveAsync(admin1.Id);
        await toggleLast.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> stripLast = () => userService.UpdateUserAsync(new UpdateUserRequest(admin1.Id, "Admin Principal", [UserRoles.Ventas]));
        await stripLast.Should().ThrowAsync<InvalidOperationException>();

        var admin2 = await userService.CreateUserAsync(new CreateUserRequest(
            "Admin Backup", "admin2@erp.local", "AdminSegura2026!", [UserRoles.Admin]));

        bool activeAfter = await userService.ToggleActiveAsync(admin1.Id);
        activeAfter.Should().BeFalse();

        var demoted = await userService.UpdateUserAsync(new UpdateUserRequest(admin1.Id, "Admin Principal", [UserRoles.Ventas]));
        demoted.Roles.Should().ContainSingle().Which.Should().Be(UserRoles.Ventas);
        admin2.IsActive.Should().BeTrue();
    }
}
