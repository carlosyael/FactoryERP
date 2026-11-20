namespace FactoryERP.Core.Models;

public sealed record RoleSummaryDto(
    string Id,
    string Name,
    bool IsSystemRole,
    int AssignedUsersCount,
    List<string> Permissions);

public sealed record UserSummaryDto(
    string Id,
    string FullName,
    string Email,
    bool IsActive,
    List<string> Roles);

public sealed record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserRequest(
    string UserId,
    string FullName,
    IReadOnlyList<string> Roles);
