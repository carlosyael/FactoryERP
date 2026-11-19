using System.Security.Claims;
using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IAppMenuService
{
    Task<List<AppMenu>> GetAuthorizedMenusAsync(ClaimsPrincipal user);
}
