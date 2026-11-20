namespace FactoryERP.Core.Constants;

/// <summary>
/// Claves de configuración white-label y reglas operativas almacenadas en base de datos.
/// </summary>
public static class SettingKeys
{
    public const string BusinessName = "Business.Name";
    public const string BusinessTagline = "Business.Tagline";
    public const string BusinessRnc = "Business.Rnc";
    public const string BusinessPhone = "Business.Phone";
    public const string BusinessWhatsApp = "Business.WhatsApp";
    public const string BusinessEmail = "Business.Email";
    public const string BusinessAddress = "Business.Address";
    public const string BusinessLogoUrl = "Business.LogoUrl";
    public const string BusinessPrimaryColor = "Business.PrimaryColor";

    public const string FiscalCurrencySymbol = "Fiscal.CurrencySymbol";
    public const string FiscalDefaultTaxRate = "Fiscal.DefaultTaxRate";
    public const string FiscalElectronicInvoicingEnabled = "Fiscal.ElectronicInvoicingEnabled";

    public const string SalesMaxDiscountPercentage = "Sales.MaxDiscountPercentage";
    public const string ProductionDefaultLossMarginPercent = "Production.DefaultLossMarginPercent";
    public const string ProductionAbnormalLossTolerancePercent = "Production.AbnormalLossTolerancePercent";
    public const string ProductionRequireQualityApproval = "Production.RequireQualityApproval";

    public const string CatalogPublicEnabled = "Catalog.PublicEnabled";
    public const string CatalogWelcomeMessage = "Catalog.WelcomeMessage";
}
