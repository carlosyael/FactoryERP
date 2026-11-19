using System.Data.Common;
using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FactoryERP.Tests.Factories;

/// <summary>
/// Fábrica de contextos SQLite en memoria compartida con claves foráneas, busy_timeout y transacciones ACID reales.
/// </summary>
public sealed class DbContextSqliteFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _anchorConnection;
    private readonly DbContextOptions<AppDbContext> _options;

    private DbContextSqliteFactory(SqliteConnection anchorConnection, DbContextOptions<AppDbContext> options)
    {
        _anchorConnection = anchorConnection;
        _options = options;
    }

    public static DbContextSqliteFactory Create(bool seedSettings = true)
    {
        string dbName = $"FactoryErpTest_{Guid.NewGuid():N}";
        string connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared;Foreign Keys=True";

        var anchorConnection = new SqliteConnection(connectionString);
        anchorConnection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;

        var factory = new DbContextSqliteFactory(anchorConnection, options);
        using var context = factory.CreateDbContext();
        context.Database.EnsureCreated();

        if (seedSettings)
        {
            SeedBaselineSettings(context);
        }

        return factory;
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        _anchorConnection.Close();
        _anchorConnection.Dispose();
    }

    private sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            ApplyPragmas(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ApplyPragmas(connection);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void ApplyPragmas(DbConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedBaselineSettings(AppDbContext context)
    {
        var defaults = new List<SystemSetting>
        {
            new() { Key = SettingKeys.BusinessName, Value = "Test Hair Lab", DefaultValue = "Test Hair Lab", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessTagline, Value = "Capilar Pro", DefaultValue = "Capilar Pro", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessRnc, Value = "130000001", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessPhone, Value = "809-555-0000", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessWhatsApp, Value = "18095550000", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessEmail, Value = "info@example.com", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.BusinessAddress, Value = "Santo Domingo", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.TextArea },
            new() { Key = SettingKeys.BusinessLogoUrl, Value = "", DefaultValue = "", Category = "Comercio", DataType = SettingDataType.ImageUrl },
            new() { Key = SettingKeys.BusinessPrimaryColor, Value = "#0f766e", DefaultValue = "#0f766e", Category = "Comercio", DataType = SettingDataType.Color },
            new() { Key = SettingKeys.FiscalCurrencySymbol, Value = "RD$", DefaultValue = "RD$", Category = "Fiscal", DataType = SettingDataType.Text },
            new() { Key = SettingKeys.FiscalDefaultTaxRate, Value = "0.18", DefaultValue = "0.18", Category = "Fiscal", DataType = SettingDataType.Decimal },
            new() { Key = SettingKeys.FiscalElectronicInvoicingEnabled, Value = "false", DefaultValue = "false", Category = "Fiscal", DataType = SettingDataType.Boolean },
            new() { Key = SettingKeys.SalesMaxDiscountPercentage, Value = "30", DefaultValue = "30", Category = "Comercial", DataType = SettingDataType.Decimal },
            new() { Key = SettingKeys.ProductionDefaultLossMarginPercent, Value = "5", DefaultValue = "5", Category = "Producción", DataType = SettingDataType.Decimal },
            new() { Key = SettingKeys.ProductionAbnormalLossTolerancePercent, Value = "2", DefaultValue = "2", Category = "Producción", DataType = SettingDataType.Decimal },
            new() { Key = SettingKeys.CatalogPublicEnabled, Value = "true", DefaultValue = "true", Category = "Catálogo", DataType = SettingDataType.Boolean },
            new() { Key = SettingKeys.CatalogWelcomeMessage, Value = "Bienvenidos", DefaultValue = "Bienvenidos", Category = "Catálogo", DataType = SettingDataType.TextArea }
        };

        context.SystemSettings.AddRange(defaults);
        context.SaveChanges();
    }
}
