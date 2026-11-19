using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Services;

public class BusinessProfileServiceTests
{
    [Fact]
    public async Task GetAndUpdateProfile_PersistsWhiteLabelIdentityAndFiresEvent()
    {
        using var factory = DbContextSqliteFactory.Create();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var settingService = new SystemSettingService(factory, cache);
        var profileService = new BusinessProfileService(settingService);

        bool eventFired = false;
        profileService.OnProfileChanged += () => eventFired = true;

        var initial = await profileService.GetProfileAsync();
        initial.BusinessName.Should().Be("Test Hair Lab");
        initial.CurrencySymbol.Should().Be("RD$");
        initial.DefaultTaxRate.Should().Be(0.18m);
        initial.ElectronicInvoicingEnabled.Should().BeFalse();

        var updated = initial with
        {
            BusinessName = "Criss Hair Studio SRL",
            Tagline = "Línea Botánica Profesional",
            WhatsAppNumber = "18095559999",
            PrimaryColor = "#7c2d12",
            PublicCatalogEnabled = true
        };

        await profileService.UpdateProfileAsync(updated);
        var reloaded = await profileService.GetProfileAsync();

        eventFired.Should().BeTrue();
        reloaded.BusinessName.Should().Be("Criss Hair Studio SRL");
        reloaded.Tagline.Should().Be("Línea Botánica Profesional");
        reloaded.WhatsAppNumber.Should().Be("18095559999");
        reloaded.PrimaryColor.Should().Be("#7c2d12");
    }
}
