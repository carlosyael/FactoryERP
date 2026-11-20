using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

/// <summary>
/// Administra cuentas de usuario, asignación dinámica de roles y ciclo de contraseña.
/// </summary>
public interface IUserManagementService
{
    Task<List<UserSummaryDto>> GetAllUsersAsync();
    Task<UserSummaryDto> CreateUserAsync(CreateUserRequest request);
    Task<UserSummaryDto> UpdateUserAsync(UpdateUserRequest request);
    Task<bool> ToggleActiveAsync(string userId);
    Task ResetPasswordAsync(string userId, string newPassword);
}
