using FactoryERP.Core.Constants;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class SystemSettingServiceTests
{
    [Fact]
    public async Task GetAndUpdateSettings_ReadsTypedValuesCreatesCustomKeysAndRefreshesCache()
    {
        using var factory = DbContextSqliteFactory.Create();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new SystemSettingService(factory, cache);

        (await service.GetValueAsync("Missing.Key", "Fallback")).Should().Be("Fallback");
        (await service.GetDecimalAsync(SettingKeys.FiscalDefaultTaxRate)).Should().Be(0.18m);
        (await service.GetBoolAsync(SettingKeys.CatalogPublicEnabled)).Should().BeTrue();

        await service.UpdateSettingsAsync(new Dictionary<string, string>
        {
            [SettingKeys.BusinessName] = "NuevoNombre",
            ["Custom.Note"] = "ValorCustom"
        });

        (await service.GetValueAsync(SettingKeys.BusinessName)).Should().Be("NuevoNombre");
        (await service.GetValueAsync("Custom.Note")).Should().Be("ValorCustom");
        (await service.GetAllAsync()).Should().Contain(s => s.Key == "Custom.Note");
    }
}
