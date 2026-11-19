using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface ISystemSettingService
{
    Task<List<SystemSetting>> GetAllAsync();
    Task<string> GetValueAsync(string key, string defaultValue = "");
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m);
    Task<bool> GetBoolAsync(string key, bool defaultValue = false);
    Task UpdateSettingAsync(string key, string value);
    Task UpdateSettingsAsync(IReadOnlyDictionary<string, string> updates);
}
