using System.Security.Claims;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class AppMenuService : IAppMenuService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public AppMenuService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<AppMenu>> GetAuthorizedMenusAsync(ClaimsPrincipal user)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var menus = await context.AppMenus
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();

        if (user.IsInRole(UserRoles.Admin))
        {
            return menus;
        }

        var permissions = user.FindAll("Permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return menus
            .Where(m => string.IsNullOrWhiteSpace(m.RequiredPermission) || permissions.Contains(m.RequiredPermission))
            .ToList();
    }
}
