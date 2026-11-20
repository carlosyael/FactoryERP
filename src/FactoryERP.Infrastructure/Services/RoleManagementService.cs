using System.Security.Claims;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleManagementService(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<List<RoleSummaryDto>> GetAllRolesAsync()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        var list = new List<RoleSummaryDto>(roles.Count);
        foreach (var role in roles)
        {
            list.Add(await BuildSummaryAsync(role));
        }
        return list;
    }

    public async Task<RoleSummaryDto?> GetByIdAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        return role is null ? null : await BuildSummaryAsync(role);
    }

    public async Task<RoleSummaryDto> CreateRoleAsync(string roleName, IEnumerable<string> permissions)
    {
        string normalizedName = ValidateRoleName(roleName);
        var validPermissions = NormalizeAndValidatePermissions(permissions);

        if (await RoleNameExistsAsync(normalizedName, excludeRoleId: null))
            throw new InvalidOperationException($"Ya existe un rol llamado '{normalizedName}'.");

        var role = new IdentityRole(normalizedName);
        var result = await _roleManager.CreateAsync(role);
        EnsureSucceeded(result);

        foreach (string permission in validPermissions)
        {
            EnsureSucceeded(await _roleManager.AddClaimAsync(role, new Claim("Permission", permission)));
        }

        return await BuildSummaryAsync(role);
    }

    public async Task<RoleSummaryDto> UpdateRoleAsync(string roleId, string roleName, IEnumerable<string> permissions)
    {
        var role = await _roleManager.FindByIdAsync(roleId)
            ?? throw new InvalidOperationException("Rol no encontrado.");

        string targetName = ValidateRoleName(roleName);
        var validPermissions = NormalizeAndValidatePermissions(permissions);

        bool isAdminRole = string.Equals(role.Name, UserRoles.Admin, StringComparison.OrdinalIgnoreCase);
        if (isAdminRole)
        {
            if (!string.Equals(targetName, UserRoles.Admin, StringComparison.Ordinal))
                throw new InvalidOperationException("El rol del sistema 'Admin' no puede renombrarse.");

            if (!validPermissions.Contains(Permissions.UsersManage) || !validPermissions.Contains(Permissions.SettingsManage))
                throw new InvalidOperationException("El rol 'Admin' debe conservar los permisos críticos de Usuarios/Roles y Configuración.");
        }

        if (await RoleNameExistsAsync(targetName, excludeRoleId: role.Id))
            throw new InvalidOperationException($"Ya existe otro rol llamado '{targetName}'.");

        if (!string.Equals(role.Name, targetName, StringComparison.Ordinal))
        {
            role.Name = targetName;
            EnsureSucceeded(await _roleManager.UpdateAsync(role));
        }

        var existingClaims = (await _roleManager.GetClaimsAsync(role)).Where(c => c.Type == "Permission").ToList();
        foreach (var claim in existingClaims.Where(c => !validPermissions.Contains(c.Value)))
        {
            EnsureSucceeded(await _roleManager.RemoveClaimAsync(role, claim));
        }

        var currentValues = existingClaims.Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
        foreach (string perm in validPermissions.Where(p => !currentValues.Contains(p)))
        {
            EnsureSucceeded(await _roleManager.AddClaimAsync(role, new Claim("Permission", perm)));
        }

        await RefreshSecurityStampsForRoleAsync(role.Name!);
        return await BuildSummaryAsync(role);
    }

    public async Task DeleteRoleAsync(string roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId)
            ?? throw new InvalidOperationException("Rol no encontrado.");

        if (string.Equals(role.Name, UserRoles.Admin, StringComparison.OrdinalIgnoreCase) ||
            UserRoles.GetAll().Contains(role.Name!, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"El rol base del sistema '{role.Name}' no puede eliminarse.");
        }

        var assignedUsers = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (assignedUsers.Count > 0)
            throw new InvalidOperationException($"No se puede eliminar el rol '{role.Name}' porque tiene {assignedUsers.Count} usuario(s) asignado(s).");

        EnsureSucceeded(await _roleManager.DeleteAsync(role));
    }

    private async Task<RoleSummaryDto> BuildSummaryAsync(IdentityRole role)
    {
        string name = role.Name ?? string.Empty;
        var claims = await _roleManager.GetClaimsAsync(role);
        var perms = claims.Where(c => c.Type == "Permission").Select(c => c.Value).OrderBy(v => v).ToList();
        var users = string.IsNullOrEmpty(name) ? [] : await _userManager.GetUsersInRoleAsync(name);
        bool isSystem = UserRoles.GetAll().Contains(name, StringComparer.OrdinalIgnoreCase);
        return new RoleSummaryDto(role.Id, name, isSystem, users.Count, perms);
    }

    private async Task<bool> RoleNameExistsAsync(string roleName, string? excludeRoleId)
    {
        var all = await _roleManager.Roles.AsNoTracking().ToListAsync();
        return all.Any(r => r.Id != excludeRoleId && string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RefreshSecurityStampsForRoleAsync(string roleName)
    {
        var users = await _userManager.GetUsersInRoleAsync(roleName);
        foreach (var user in users)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }
    }

    private static string ValidateRoleName(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("El nombre del rol es obligatorio.", nameof(roleName));
        string trimmed = roleName.Trim();
        if (trimmed.Length > 60)
            throw new ArgumentException("El nombre del rol no puede superar 60 caracteres.", nameof(roleName));
        return trimmed;
    }

    private static HashSet<string> NormalizeAndValidatePermissions(IEnumerable<string> permissions)
    {
        var canonicalMap = Permissions.GetAll().ToDictionary(p => p, p => p, StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in permissions ?? [])
        {
            string code = raw?.Trim() ?? string.Empty;
            if (!canonicalMap.TryGetValue(code, out string? canonical))
                throw new ArgumentException($"El permiso '{raw}' no es válido en el sistema.");
            result.Add(canonical);
        }
        return result;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
