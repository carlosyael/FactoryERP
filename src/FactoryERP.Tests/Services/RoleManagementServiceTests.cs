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

public class RoleManagementServiceTests
{
    private static ServiceProvider BuildProvider(DbContextSqliteFactory factory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDbContextFactory<AppDbContext>>(factory);
        services.AddScoped(_ => factory.CreateDbContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateUpdateAndDeleteCustomRole_ManagesClaimsAndValidations()
    {
        using var factory = DbContextSqliteFactory.Create(seedSettings: false);
        await using var sp = BuildProvider(factory);
        await using var scope = sp.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRoleManagementService>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var created = await service.CreateRoleAsync("Supervisor Planta", [Permissions.DashboardView, Permissions.ProductionManage]);
        created.Name.Should().Be("Supervisor Planta");
        created.IsSystemRole.Should().BeFalse();
        created.Permissions.Should().BeEquivalentTo([Permissions.DashboardView, Permissions.ProductionManage]);

        var identityRole = await roleManager.FindByIdAsync(created.Id);
        var claims = await roleManager.GetClaimsAsync(identityRole!);
        claims.Where(c => c.Type == "Permission").Select(c => c.Value)
            .Should().BeEquivalentTo([Permissions.DashboardView, Permissions.ProductionManage]);

        var updated = await service.UpdateRoleAsync(created.Id, "Jefe de Planta", [Permissions.DashboardView, Permissions.FormulasManage, Permissions.RawMaterialsView]);
        updated.Name.Should().Be("Jefe de Planta");
        updated.Permissions.Should().BeEquivalentTo([Permissions.DashboardView, Permissions.FormulasManage, Permissions.RawMaterialsView]);

        Func<Task> dupAct = () => service.CreateRoleAsync("jefe de planta", [Permissions.DashboardView]);
        await dupAct.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> emptyAct = () => service.CreateRoleAsync("  ", [Permissions.DashboardView]);
        await emptyAct.Should().ThrowAsync<ArgumentException>();

        Func<Task> badPermAct = () => service.CreateRoleAsync("Otro", ["Permiso.Inexistente"]);
        await badPermAct.Should().ThrowAsync<ArgumentException>();

        await service.DeleteRoleAsync(created.Id);
        (await service.GetByIdAsync(created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AdminProtectionAndAssignedRoleDeletion_AreEnforced()
    {
        using var factory = DbContextSqliteFactory.Create(seedSettings: false);
        await using var sp = BuildProvider(factory);
        await using var scope = sp.CreateAsyncScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var service = scope.ServiceProvider.GetRequiredService<IRoleManagementService>();

        await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
        var adminRole = (await roleManager.FindByNameAsync(UserRoles.Admin))!;
        foreach (string p in Permissions.GetAll())
            await roleManager.AddClaimAsync(adminRole, new System.Security.Claims.Claim("Permission", p));

        Func<Task> renameAdmin = () => service.UpdateRoleAsync(adminRole.Id, "SuperAdmin", Permissions.GetAll());
        await renameAdmin.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> stripAdminUsers = () => service.UpdateRoleAsync(adminRole.Id, UserRoles.Admin, [Permissions.DashboardView]);
        await stripAdminUsers.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> stripAdminSettings = () => service.UpdateRoleAsync(adminRole.Id, UserRoles.Admin, [Permissions.UsersManage]);
        await stripAdminSettings.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> deleteAdmin = () => service.DeleteRoleAsync(adminRole.Id);
        await deleteAdmin.Should().ThrowAsync<InvalidOperationException>();

        var custom = await service.CreateRoleAsync("Calidad", [Permissions.RawMaterialsView]);
        var user = new ApplicationUser { UserName = "qa@erp.local", Email = "qa@erp.local", FullName = "Inspector", IsActive = true };
        await userManager.CreateAsync(user, "Calidad123!");
        await userManager.AddToRoleAsync(user, custom.Name);

        var roles = await service.GetAllRolesAsync();
        roles.First(r => r.Id == custom.Id).AssignedUsersCount.Should().Be(1);

        Func<Task> deleteAssigned = () => service.DeleteRoleAsync(custom.Id);
        await deleteAssigned.Should().ThrowAsync<InvalidOperationException>();
    }
}
