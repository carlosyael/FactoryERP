namespace FactoryERP.Core.Constants;

/// <summary>
/// Códigos canónicos del catálogo de cuentas contables industriales.
/// </summary>
public static class AccountCodes
{
    public const string CurrentAssetsControl = "1100";
    public const string Cash = "1101";
    public const string Bank = "1102";
    public const string AccountsReceivable = "1103";
    public const string RawMaterialInventory = "1104";
    public const string WorkInProgressInventory = "1105";
    public const string FinishedGoodsInventory = "1106";
    public const string TaxCreditAdvance = "1107";

    public const string CurrentLiabilitiesControl = "2100";
    public const string AccountsPayable = "2101";
    public const string TaxPayable = "2102";

    public const string EquityControl = "3100";
    public const string ShareCapital = "3101";
    public const string RetainedEarnings = "3102";

    public const string RevenueControl = "4100";
    public const string ProductSalesRevenue = "4101";

    public const string CostControl = "5100";
    public const string CostOfGoodsSold = "5101";
    public const string AbnormalProductionLoss = "5201";

    public const string ExpenseControl = "6100";
    public const string ManufacturingOverheadApplied = "6101";
    public const string OperatingExpenses = "6102";
    public const string AdministrativeExpenses = "6103";
    public const string DistributionExpenses = "6104";
}
