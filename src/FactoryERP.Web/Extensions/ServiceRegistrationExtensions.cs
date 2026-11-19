using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Infrastructure.Interceptors;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Web.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddFactoryErpCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ILoadingService, LoadingService>();
        services.AddScoped<IFileUploadService, FileUploadService>();
        services.AddScoped<AuditInterceptor>();

        ConfigureDatabase(services, configuration);

        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IBusinessProfileService, BusinessProfileService>();
        services.AddScoped<IAppMenuService, AppMenuService>();
        services.AddScoped<ITaxDocumentProvider, PendingElectronicInvoiceProvider>();

        services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IManufacturingAccountingService, ManufacturingAccountingService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();

        services.AddScoped<IRawMaterialService, RawMaterialService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IFormulaService, FormulaService>();
        services.AddScoped<IProductionOrderService, ProductionOrderService>();

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    private static void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        string provider = Environment.GetEnvironmentVariable("DatabaseProvider")
            ?? configuration["DatabaseProvider"]
            ?? "SqlServer";

        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=factoryerp_dev.db";

        void ConfigureOptions(IServiceProvider sp, DbContextOptionsBuilder options)
        {
            var interceptor = sp.GetRequiredService<AuditInterceptor>();
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null));
            }

            options.AddInterceptors(interceptor);
        }

        services.AddDbContextFactory<AppDbContext>(ConfigureOptions, ServiceLifetime.Scoped);
        services.AddDbContext<AppDbContext>(ConfigureOptions);
    }
}
