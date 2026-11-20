using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

/// <summary>
/// Administra roles personalizados y sus permisos granulares basados en claims.
/// </summary>
public interface IRoleManagementService
{
    Task<List<RoleSummaryDto>> GetAllRolesAsync();
    Task<RoleSummaryDto?> GetByIdAsync(string roleId);
    Task<RoleSummaryDto> CreateRoleAsync(string roleName, IEnumerable<string> permissions);
    Task<RoleSummaryDto> UpdateRoleAsync(string roleId, string roleName, IEnumerable<string> permissions);
    Task DeleteRoleAsync(string roleId);
}
