using System.Globalization;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;

namespace FactoryERP.Infrastructure.Services;

public sealed class BusinessProfileService : IBusinessProfileService
{
    private readonly ISystemSettingService _settings;

    public BusinessProfileService(ISystemSettingService settings)
    {
        _settings = settings;
    }

    public event Action? OnProfileChanged;

    public async Task<BusinessProfileDto> GetProfileAsync()
    {
        return new BusinessProfileDto(
            BusinessName: await _settings.GetValueAsync(SettingKeys.BusinessName, "FactoryERP"),
            Tagline: await _settings.GetValueAsync(SettingKeys.BusinessTagline, "Génesis · Manufactura y Gestión Capilar"),
            Rnc: await _settings.GetValueAsync(SettingKeys.BusinessRnc, string.Empty),
            Phone: await _settings.GetValueAsync(SettingKeys.BusinessPhone, string.Empty),
            WhatsAppNumber: await _settings.GetValueAsync(SettingKeys.BusinessWhatsApp, string.Empty),
            Email: await _settings.GetValueAsync(SettingKeys.BusinessEmail, string.Empty),
            Address: await _settings.GetValueAsync(SettingKeys.BusinessAddress, string.Empty),
            LogoUrl: await _settings.GetValueAsync(SettingKeys.BusinessLogoUrl, string.Empty),
            PrimaryColor: await _settings.GetValueAsync(SettingKeys.BusinessPrimaryColor, "#0f766e"),
            CurrencySymbol: await _settings.GetValueAsync(SettingKeys.FiscalCurrencySymbol, "RD$"),
            DefaultTaxRate: await _settings.GetDecimalAsync(SettingKeys.FiscalDefaultTaxRate, 0.18m),
            ElectronicInvoicingEnabled: await _settings.GetBoolAsync(SettingKeys.FiscalElectronicInvoicingEnabled, false),
            MaxDiscountPercentage: await _settings.GetDecimalAsync(SettingKeys.SalesMaxDiscountPercentage, 30m),
            DefaultLossMarginPercent: await _settings.GetDecimalAsync(SettingKeys.ProductionDefaultLossMarginPercent, 5m),
            AbnormalLossTolerancePercent: await _settings.GetDecimalAsync(SettingKeys.ProductionAbnormalLossTolerancePercent, 2m),
            PublicCatalogEnabled: await _settings.GetBoolAsync(SettingKeys.CatalogPublicEnabled, true),
            CatalogWelcomeMessage: await _settings.GetValueAsync(SettingKeys.CatalogWelcomeMessage, string.Empty),
            RequireQualityApproval: await _settings.GetBoolAsync(SettingKeys.ProductionRequireQualityApproval, false));
    }

    public async Task UpdateProfileAsync(BusinessProfileDto profile)
    {
        if (string.IsNullOrWhiteSpace(profile.BusinessName))
            throw new ArgumentException("El nombre del comercio es obligatorio.", nameof(profile));
        if (profile.DefaultTaxRate < 0m || profile.DefaultTaxRate > 1m)
            throw new ArgumentException("La tasa de ITBIS debe expresarse en decimal entre 0 y 1 (ej. 0.18).", nameof(profile));
        if (profile.MaxDiscountPercentage < 0m || profile.MaxDiscountPercentage > 100m)
            throw new ArgumentException("El descuento máximo debe estar entre 0% y 100%.", nameof(profile));
        if (profile.DefaultLossMarginPercent < 0m || profile.DefaultLossMarginPercent >= 100m)
            throw new ArgumentException("La merma estándar por defecto debe estar entre 0% y 99.99%.", nameof(profile));
        if (profile.AbnormalLossTolerancePercent < 0m || profile.AbnormalLossTolerancePercent > 50m)
            throw new ArgumentException("La tolerancia de merma anormal debe estar entre 0% y 50%.", nameof(profile));

        var updates = new Dictionary<string, string>
        {
            [SettingKeys.BusinessName] = profile.BusinessName.Trim(),
            [SettingKeys.BusinessTagline] = profile.Tagline.Trim(),
            [SettingKeys.BusinessRnc] = profile.Rnc.Trim(),
            [SettingKeys.BusinessPhone] = profile.Phone.Trim(),
            [SettingKeys.BusinessWhatsApp] = profile.WhatsAppNumber.Trim(),
            [SettingKeys.BusinessEmail] = profile.Email.Trim(),
            [SettingKeys.BusinessAddress] = profile.Address.Trim(),
            [SettingKeys.BusinessLogoUrl] = profile.LogoUrl.Trim(),
            [SettingKeys.BusinessPrimaryColor] = string.IsNullOrWhiteSpace(profile.PrimaryColor) ? "#0f766e" : profile.PrimaryColor.Trim(),
            [SettingKeys.FiscalCurrencySymbol] = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol.Trim(),
            [SettingKeys.FiscalDefaultTaxRate] = profile.DefaultTaxRate.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.FiscalElectronicInvoicingEnabled] = profile.ElectronicInvoicingEnabled ? "true" : "false",
            [SettingKeys.SalesMaxDiscountPercentage] = profile.MaxDiscountPercentage.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ProductionDefaultLossMarginPercent] = profile.DefaultLossMarginPercent.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ProductionAbnormalLossTolerancePercent] = profile.AbnormalLossTolerancePercent.ToString(CultureInfo.InvariantCulture),
            [SettingKeys.ProductionRequireQualityApproval] = profile.RequireQualityApproval ? "true" : "false",
            [SettingKeys.CatalogPublicEnabled] = profile.PublicCatalogEnabled ? "true" : "false",
            [SettingKeys.CatalogWelcomeMessage] = profile.CatalogWelcomeMessage.Trim()
        };

        await _settings.UpdateSettingsAsync(updates);
        OnProfileChanged?.Invoke();
    }
}
