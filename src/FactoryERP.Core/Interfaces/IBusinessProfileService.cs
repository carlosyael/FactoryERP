using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IBusinessProfileService
{
    event Action? OnProfileChanged;
    Task<BusinessProfileDto> GetProfileAsync();
    Task UpdateProfileAsync(BusinessProfileDto profile);
}
