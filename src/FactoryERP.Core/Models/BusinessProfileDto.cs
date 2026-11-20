namespace FactoryERP.Core.Models;

/// <summary>
/// Perfil completo de identidad y reglas comerciales white-label del comercio.
/// </summary>
public sealed record BusinessProfileDto(
    string BusinessName,
    string Tagline,
    string Rnc,
    string Phone,
    string WhatsAppNumber,
    string Email,
    string Address,
    string LogoUrl,
    string PrimaryColor,
    string CurrencySymbol,
    decimal DefaultTaxRate,
    bool ElectronicInvoicingEnabled,
    decimal MaxDiscountPercentage,
    decimal DefaultLossMarginPercent,
    decimal AbnormalLossTolerancePercent,
    bool PublicCatalogEnabled,
    string CatalogWelcomeMessage,
    bool RequireQualityApproval = false);
