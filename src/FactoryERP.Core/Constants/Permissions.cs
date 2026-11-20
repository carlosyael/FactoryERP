namespace FactoryERP.Core.Constants;

public sealed record PermissionDefinition(string Code, string Module, string DisplayName, string Description);

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

    private static readonly IReadOnlyList<PermissionDefinition> Definitions =
    [
        new(DashboardView, "Panel", "Ver Panel Principal", "Acceso al resumen operativo y KPIs ejecutivos."),
        new(RawMaterialsView, "Producción", "Ver Materias Primas", "Consultar inventario de insumos, costos y alertas de stock."),
        new(RawMaterialsManage, "Producción", "Gestionar Materias Primas", "Crear, editar y ajustar existencias de insumos y empaques."),
        new(FormulasManage, "Producción", "Gestionar Fórmulas", "Crear, versionar y editar recetas de fabricación."),
        new(ProductionManage, "Producción", "Gestionar Producción y Lotes", "Simular, iniciar, completar o cancelar órdenes y ver lotes FEFO."),
        new(PurchasesManage, "Compras", "Gestionar Compras y Suplidores", "Administrar suplidores, recepciones con CPP e historial de pagos CxP."),
        new(CatalogManage, "Catálogo y Comercial", "Gestionar Catálogo Interno", "Administrar productos terminados, márgenes, fotos y publicación."),
        new(CustomersManage, "Catálogo y Comercial", "Gestionar Clientes y CxC", "Crear clientes, definir crédito y registrar cobros."),
        new(SalesManage, "Catálogo y Comercial", "Gestionar Pedidos y Ventas", "Emitir ventas FEFO con ITBIS 18% y reimprimir comprobantes."),
        new(AccountingView, "Contabilidad", "Ver Plan y Libro Diario", "Consultar cuentas contables y movimientos del diario."),
        new(AccountingManage, "Contabilidad", "Operar Contabilidad", "Crear cuentas, registrar gastos y mayorizar o anular asientos."),
        new(ReportsView, "Contabilidad", "Ver Reportes Financieros", "Consultar Balanza de Comprobación, Estado de Resultados y Balance."),
        new(SettingsManage, "Sistema", "Configurar Comercio", "Editar marca white-label, impuestos y catálogo público."),
        new(UsersManage, "Sistema", "Gestionar Usuarios y Roles", "Administrar cuentas de acceso, roles personalizados y permisos.")
    ];

    public static IReadOnlyList<PermissionDefinition> GetDefinitions() => Definitions;

    public static IReadOnlyList<string> GetAll() =>
        Definitions.Select(d => d.Code).ToList();
}
