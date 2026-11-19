namespace FactoryERP.Core.Constants;

/// <summary>
/// Permisos granulares autorizados mediante claims.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "Dashboard.View";
    public const string SettingsManage = "Settings.Manage";
    public const string UsersManage = "Users.Manage";
    public const string RawMaterialsView = "RawMaterials.View";
    public const string RawMaterialsManage = "RawMaterials.Manage";
    public const string PurchasesManage = "Purchases.Manage";
    public const string FormulasManage = "Formulas.Manage";
    public const string ProductionManage = "Production.Manage";
    public const string CatalogManage = "Catalog.Manage";
    public const string CustomersManage = "Customers.Manage";
    public const string SalesManage = "Sales.Manage";
    public const string AccountingView = "Accounting.View";
    public const string AccountingManage = "Accounting.Manage";
    public const string ReportsView = "Reports.View";

    public static IReadOnlyList<string> GetAll() =>
    [
        DashboardView, SettingsManage, UsersManage,
        RawMaterialsView, RawMaterialsManage, PurchasesManage,
        FormulasManage, ProductionManage, CatalogManage,
        CustomersManage, SalesManage,
        AccountingView, AccountingManage, ReportsView
    ];
}
