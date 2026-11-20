using System.Security.Claims;
using FactoryERP.Core.Interfaces;

namespace FactoryERP.Web.Services;

public interface ICircuitUserAccessor
{
    void SetUser(ClaimsPrincipal? principal);
}

public sealed class CurrentUserService : ICurrentUserService, ICircuitUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _circuitUser;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void SetUser(ClaimsPrincipal? principal) => _circuitUser = principal;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _circuitUser?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.Identity?.Name
        ?? _circuitUser?.Identity?.Name;
}
