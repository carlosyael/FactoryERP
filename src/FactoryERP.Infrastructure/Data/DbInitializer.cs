using System.Security.Claims;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryERP.Infrastructure.Data;

/// <summary>
/// Inicializa esquema, roles, permisos, configuración white-label, plan de cuentas y menú dinámico.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, AppDbContext context, bool seedDemoData = true)
    {
        if (context.Database.IsSqlServer())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        await SeedRolesAndAdminAsync(services);
        await SeedSettingsAsync(context);
        await SeedMenusAsync(context);

        var chartService = services.GetRequiredService<IChartOfAccountsService>();
        await chartService.SeedStandardChartAsync();

        if (seedDemoData)
        {
            await DbDemoSeeder.SeedAsync(context);
        }
    }

    private static async Task SeedRolesAndAdminAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var rolePermissions = new Dictionary<string, IReadOnlyList<string>>
        {
            [UserRoles.Admin] = Permissions.GetAll(),
            [UserRoles.Produccion] = [Permissions.DashboardView, Permissions.RawMaterialsView, Permissions.RawMaterialsManage, Permissions.FormulasManage, Permissions.ProductionManage, Permissions.CatalogManage],
            [UserRoles.Ventas] = [Permissions.DashboardView, Permissions.CatalogManage, Permissions.CustomersManage, Permissions.SalesManage],
            [UserRoles.Contabilidad] = [Permissions.DashboardView, Permissions.AccountingView, Permissions.AccountingManage, Permissions.ReportsView, Permissions.PurchasesManage]
        };

        foreach (var (roleName, perms) in rolePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole(roleName);
                await roleManager.CreateAsync(role);
            }

            var existingClaims = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet();

            foreach (var perm in perms.Where(p => !existingClaims.Contains(p)))
            {
                await roleManager.AddClaimAsync(role, new Claim("Permission", perm));
            }
        }

        const string adminEmail = "admin@factoryerp.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            string seedPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? "Admin123!";
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrador del Sistema",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(admin, seedPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, UserRoles.Admin);
            }
        }
    }

    private static async Task SeedSettingsAsync(AppDbContext context)
    {
        var defaults = new List<SystemSetting>
        {
            new() { Key = SettingKeys.BusinessName, Value = "FactoryERP", DefaultValue = "FactoryERP", Category = "Comercio", DataType = SettingDataType.Text, Description = "Nombre comercial visible en el ERP, catálogo público y comprobantes." },
            new() { Key = SettingKeys.BusinessTagline, Value = "Génesis · Manufactura y Gestión Capilar", DefaultValue = "Génesis · Manufactura y Gestión Capilar", Category = "Comercio", DataType = SettingDataType.Text, Description = "Slogan o subtítulo del comercio." },
            new() { Key = SettingKeys.BusinessRnc, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text, Description = "RNC o registro fiscal del comercio." },
            new() { Key = SettingKeys.BusinessPhone, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text, Description = "Teléfono principal del comercio." },
            new() { Key = SettingKeys.BusinessWhatsApp, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text, Description = "Número de WhatsApp (con código país, ej. 18095551234) para pedidos del catálogo público." },
            new() { Key = SettingKeys.BusinessEmail, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text, Description = "Correo electrónico de contacto comercial." },
            new() { Key = SettingKeys.BusinessAddress, Value = "República Dominicana", DefaultValue = "República Dominicana", Category = "Comercio", DataType = SettingDataType.TextArea, Description = "Dirección física del taller o comercio." },
            new() { Key = SettingKeys.BusinessLogoUrl, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.ImageUrl, Description = "Logo del comercio (subida local o URL pública)." },
            new() { Key = SettingKeys.BusinessPrimaryColor, Value = "#0f766e", DefaultValue = "#0f766e", Category = "Comercio", DataType = SettingDataType.Color, Description = "Color principal de la marca en el sistema y catálogo público." },
            new() { Key = SettingKeys.FiscalCurrencySymbol, Value = "RD$", DefaultValue = "RD$", Category = "Fiscal", DataType = SettingDataType.Text, Description = "Símbolo de moneda oficial (Pesos Dominicanos)." },
            new() { Key = SettingKeys.FiscalDefaultTaxRate, Value = "0.18", DefaultValue = "0.18", Category = "Fiscal", DataType = SettingDataType.Decimal, MinValue = 0m, MaxValue = 1m, Description = "Tasa estándar de ITBIS (0.18 = 18%)." },
            new() { Key = SettingKeys.FiscalElectronicInvoicingEnabled, Value = "false", DefaultValue = "false", Category = "Fiscal", DataType = SettingDataType.Boolean, Description = "Integración de facturación electrónica DGII (actualmente pendiente de implementación)." },
            new() { Key = SettingKeys.SalesMaxDiscountPercentage, Value = "30", DefaultValue = "30", Category = "Comercial", DataType = SettingDataType.Decimal, MinValue = 0m, MaxValue = 100m, Description = "Descuento máximo permitido en ventas (%)." },
            new() { Key = SettingKeys.ProductionDefaultLossMarginPercent, Value = "5", DefaultValue = "5", Category = "Producción", DataType = SettingDataType.Decimal, MinValue = 0m, MaxValue = 99m, Description = "Margen de pérdida estándar aplicado por defecto a nuevas fórmulas/órdenes (%)." },
            new() { Key = SettingKeys.ProductionAbnormalLossTolerancePercent, Value = "2", DefaultValue = "2", Category = "Producción", DataType = SettingDataType.Decimal, MinValue = 0m, MaxValue = 50m, Description = "Tolerancia adicional sobre la merma esperada antes de contabilizar pérdida anormal (%)." },
            new() { Key = SettingKeys.CatalogPublicEnabled, Value = "true", DefaultValue = "true", Category = "Catálogo", DataType = SettingDataType.Boolean, Description = "Habilita o deshabilita la vista pública compartible (/catalogo)." },
            new() { Key = SettingKeys.CatalogWelcomeMessage, Value = "Explora nuestra línea profesional de cuidado capilar.", DefaultValue = "Explora nuestra línea profesional de cuidado capilar.", Category = "Catálogo", DataType = SettingDataType.TextArea, Description = "Mensaje de bienvenida mostrado en el catálogo público." }
        };

        var existingSettings = await context.SystemSettings.ToDictionaryAsync(s => s.Key);
        if (existingSettings.TryGetValue(SettingKeys.BusinessName, out var nameSetting) &&
            nameSetting.Value == "Mi Fábrica Capilar")
        {
            nameSetting.Value = "FactoryERP";
            nameSetting.DefaultValue = "FactoryERP";
            await context.SaveChangesAsync();
        }

        var missing = defaults.Where(d => !existingSettings.ContainsKey(d.Key)).ToList();
        if (missing.Count > 0)
        {
            await context.SystemSettings.AddRangeAsync(missing);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedMenusAsync(AppDbContext context)
    {
        var existingMenus = await context.AppMenus.ToListAsync();
        if (existingMenus.Count > 0)
        {
            bool repaired = false;
            foreach (var menu in existingMenus)
            {
                if (menu.Icon == "bi-flask") { menu.Icon = "bi-eyedropper"; repaired = true; }
                else if (menu.Icon == "bi-scale") { menu.Icon = "bi-calculator"; repaired = true; }
            }
            if (repaired) await context.SaveChangesAsync();
            return;
        }

        var menus = new List<AppMenu>
        {
            new() { Title = "Materias Primas", Url = "raw-materials", Icon = "bi-droplet-half", MenuGroup = "Producción", RequiredPermission = Permissions.RawMaterialsView, DisplayOrder = 10 },
            new() { Title = "Fórmulas y Recetas", Url = "formulas", Icon = "bi-eyedropper", MenuGroup = "Producción", RequiredPermission = Permissions.FormulasManage, DisplayOrder = 20 },
            new() { Title = "Órdenes de Producción", Url = "production", Icon = "bi-gear-wide-connected", MenuGroup = "Producción", RequiredPermission = Permissions.ProductionManage, DisplayOrder = 30 },
            new() { Title = "Lotes Terminados", Url = "batches", Icon = "bi-boxes", MenuGroup = "Producción", RequiredPermission = Permissions.ProductionManage, DisplayOrder = 40 },

            new() { Title = "Suplidores", Url = "suppliers", Icon = "bi-truck", MenuGroup = "Compras e Insumos", RequiredPermission = Permissions.PurchasesManage, DisplayOrder = 50 },
            new() { Title = "Compras de Insumos", Url = "purchases", Icon = "bi-cart-plus", MenuGroup = "Compras e Insumos", RequiredPermission = Permissions.PurchasesManage, DisplayOrder = 60 },

            new() { Title = "Catálogo Interno", Url = "catalog", Icon = "bi-grid-3x3-gap", MenuGroup = "Catálogo y Comercial", RequiredPermission = Permissions.CatalogManage, DisplayOrder = 70 },
            new() { Title = "Catálogo Público", Url = "catalogo", Icon = "bi-shop", MenuGroup = "Catálogo y Comercial", RequiredPermission = Permissions.DashboardView, DisplayOrder = 75 },
            new() { Title = "Clientes y CxC", Url = "customers", Icon = "bi-people", MenuGroup = "Catálogo y Comercial", RequiredPermission = Permissions.CustomersManage, DisplayOrder = 80 },
            new() { Title = "Pedidos y Ventas", Url = "sales", Icon = "bi-receipt-cutoff", MenuGroup = "Catálogo y Comercial", RequiredPermission = Permissions.SalesManage, DisplayOrder = 90 },

            new() { Title = "Plan de Cuentas", Url = "accounting/accounts", Icon = "bi-diagram-3", MenuGroup = "Contabilidad", RequiredPermission = Permissions.AccountingView, DisplayOrder = 100 },
            new() { Title = "Libro Diario", Url = "accounting/journal", Icon = "bi-journal-text", MenuGroup = "Contabilidad", RequiredPermission = Permissions.AccountingView, DisplayOrder = 110 },
            new() { Title = "Balanza de Comprobación", Url = "accounting/trial-balance", Icon = "bi-calculator", MenuGroup = "Contabilidad", RequiredPermission = Permissions.ReportsView, DisplayOrder = 120 },
            new() { Title = "Estado de Resultados", Url = "accounting/income-statement", Icon = "bi-graph-up-arrow", MenuGroup = "Contabilidad", RequiredPermission = Permissions.ReportsView, DisplayOrder = 130 },
            new() { Title = "Balance General", Url = "accounting/balance-sheet", Icon = "bi-bank", MenuGroup = "Contabilidad", RequiredPermission = Permissions.ReportsView, DisplayOrder = 140 },

            new() { Title = "Configuración", Url = "settings", Icon = "bi-sliders", MenuGroup = "Sistema", RequiredPermission = Permissions.SettingsManage, DisplayOrder = 150 },
            new() { Title = "Usuarios", Url = "users", Icon = "bi-person-gear", MenuGroup = "Sistema", RequiredPermission = Permissions.UsersManage, DisplayOrder = 160 },
            new() { Title = "Roles y Permisos", Url = "roles", Icon = "bi-shield-lock", MenuGroup = "Sistema", RequiredPermission = Permissions.UsersManage, DisplayOrder = 170 }
        };

        await context.AppMenus.AddRangeAsync(menus);
        await context.SaveChangesAsync();
    }
}
