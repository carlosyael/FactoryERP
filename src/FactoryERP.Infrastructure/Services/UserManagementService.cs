using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

public sealed class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserManagementService(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<List<UserSummaryDto>> GetAllUsersAsync()
    {
        var users = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
        var list = new List<UserSummaryDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            list.Add(new UserSummaryDto(u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive, roles.OrderBy(r => r).ToList()));
        }
        return list;
    }

    public async Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("El correo electrónico es obligatorio.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("La contraseña inicial es obligatoria.", nameof(request));

        var canonicalRoles = await ResolveValidRolesAsync(request.Roles);
        string email = request.Email.Trim();

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            IsActive = true
        };

        EnsureSucceeded(await _userManager.CreateAsync(user, request.Password));
        EnsureSucceeded(await _userManager.AddToRolesAsync(user, canonicalRoles));

        return new UserSummaryDto(user.Id, user.FullName, user.Email, user.IsActive, canonicalRoles);
    }

    public async Task<UserSummaryDto> UpdateUserAsync(UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(request));

        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var canonicalRoles = await ResolveValidRolesAsync(request.Roles);
        var currentRoles = await _userManager.GetRolesAsync(user);

        bool wasAdmin = currentRoles.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase);
        bool willBeAdmin = canonicalRoles.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase);

        if (wasAdmin && !willBeAdmin && user.IsActive && await CountActiveAdminsAsync() <= 1)
            throw new InvalidOperationException("Debe permanecer al menos un usuario Administrador activo.");

        user.FullName = request.FullName.Trim();
        EnsureSucceeded(await _userManager.UpdateAsync(user));

        var toRemove = currentRoles.Except(canonicalRoles, StringComparer.Ordinal).ToList();
        var toAdd = canonicalRoles.Except(currentRoles, StringComparer.Ordinal).ToList();

        if (toRemove.Count > 0) EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, toRemove));
        if (toAdd.Count > 0) EnsureSucceeded(await _userManager.AddToRolesAsync(user, toAdd));
        await _userManager.UpdateSecurityStampAsync(user);

        return new UserSummaryDto(user.Id, user.FullName, user.Email ?? string.Empty, user.IsActive, canonicalRoles);
    }

    public async Task<bool> ToggleActiveAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var roles = await _userManager.GetRolesAsync(user);
        bool isAdmin = roles.Contains(UserRoles.Admin, StringComparer.OrdinalIgnoreCase);

        if (user.IsActive && isAdmin && await CountActiveAdminsAsync() <= 1)
            throw new InvalidOperationException("Debe permanecer al menos un usuario Administrador activo.");

        user.IsActive = !user.IsActive;
        EnsureSucceeded(await _userManager.UpdateAsync(user));
        if (!user.IsActive) await _userManager.UpdateSecurityStampAsync(user);
        return user.IsActive;
    }

    public async Task ResetPasswordAsync(string userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("La nueva contraseña es obligatorio.", nameof(newPassword));

        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await _userManager.ResetPasswordAsync(user, token, newPassword));
        await _userManager.UpdateSecurityStampAsync(user);
    }

    private async Task<List<string>> ResolveValidRolesAsync(IReadOnlyList<string> roles)
    {
        if (roles is null || roles.Count == 0)
            throw new InvalidOperationException("Debe asignar al menos un rol al usuario.");

        var resolved = new List<string>();
        foreach (string raw in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var found = await _roleManager.FindByNameAsync(raw.Trim())
                ?? throw new InvalidOperationException($"El rol '{raw}' no existe.");
            if (!string.IsNullOrWhiteSpace(found.Name) && !resolved.Contains(found.Name, StringComparer.Ordinal))
                resolved.Add(found.Name);
        }

        if (resolved.Count == 0)
            throw new InvalidOperationException("Debe asignar al menos un rol válido al usuario.");

        return resolved;
    }

    private async Task<int> CountActiveAdminsAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(UserRoles.Admin);
        return admins.Count(a => a.IsActive);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
