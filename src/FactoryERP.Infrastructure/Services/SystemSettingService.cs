using System.Globalization;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Infrastructure.Services;

public sealed class SystemSettingService : ISystemSettingService
{
    private const string CacheKey = "FactoryERP.SystemSettings";
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IMemoryCache _cache;

    public SystemSettingService(IDbContextFactory<AppDbContext> contextFactory, IMemoryCache cache)
    {
        _contextFactory = contextFactory;
        _cache = cache;
    }

    public async Task<List<SystemSetting>> GetAllAsync()
    {
        var map = await GetMapAsync();
        return map.Values.OrderBy(s => s.Category).ThenBy(s => s.Key).ToList();
    }

    public async Task<string> GetValueAsync(string key, string defaultValue = "")
    {
        var map = await GetMapAsync();
        return map.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value)
            ? setting.Value
            : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m)
    {
        var map = await GetMapAsync();
        if (map.TryGetValue(key, out var setting) &&
            decimal.TryParse(setting.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var map = await GetMapAsync();
        if (map.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    public async Task UpdateSettingAsync(string key, string value)
        => await UpdateSettingsAsync(new Dictionary<string, string> { [key] = value });

    public async Task UpdateSettingsAsync(IReadOnlyDictionary<string, string> updates)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var keys = updates.Keys.ToList();
        var existing = await context.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key);

        foreach (var (key, value) in updates)
        {
            if (existing.TryGetValue(key, out var setting))
            {
                setting.Value = value;
            }
            else
            {
                context.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    IsSystem = false
                });
            }
        }

        await context.SaveChangesAsync();
        _cache.Remove(CacheKey);
    }

    private async Task<Dictionary<string, SystemSetting>> GetMapAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, SystemSetting>? cached) && cached != null)
        {
            return cached;
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        var list = await context.SystemSettings.AsNoTracking().ToListAsync();
        var map = list.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _cache.Set(CacheKey, map, TimeSpan.FromMinutes(10));
        return map;
    }
}
