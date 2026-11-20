using FactoryERP.Core.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<AppMenu> AppMenus => Set<AppMenu>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductFormula> ProductFormulas => Set<ProductFormula>();
    public DbSet<FormulaItem> FormulaItems => Set<FormulaItem>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<FinishedBatch> FinishedBatches => Set<FinishedBatch>();

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPayment> CustomerPayments => Set<CustomerPayment>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    public DbSet<AccountingAccount> AccountingAccounts => Set<AccountingAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureDecimalPrecisions(builder);
        ConfigureIndexes(builder);
        ConfigureRelationships(builder);
    }

    private static void ConfigureDecimalPrecisions(ModelBuilder builder)
    {
        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        builder.Entity<RawMaterial>(e =>
        {
            e.Property(x => x.CurrentStock).HasPrecision(18, 4);
            e.Property(x => x.MinimumStockLevel).HasPrecision(18, 4);
            e.Property(x => x.AverageCost).HasPrecision(18, 4);
            e.Property(x => x.LastPurchaseCost).HasPrecision(18, 4);
        });

        builder.Entity<PurchaseOrderItem>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
        });

        builder.Entity<Product>(e =>
        {
            e.Property(x => x.Cost).HasPrecision(18, 4);
            e.Property(x => x.TaxRate).HasPrecision(18, 4);
        });

        builder.Entity<ProductFormula>(e =>
            e.Property(x => x.StandardLossMarginPercent).HasPrecision(18, 4));

        builder.Entity<FormulaItem>(e =>
            e.Property(x => x.QuantityRequired).HasPrecision(18, 4));

        builder.Entity<ProductionOrder>(e =>
        {
            e.Property(x => x.ExpectedLossMarginPercent).HasPrecision(18, 4);
            e.Property(x => x.ActualLossPercent).HasPrecision(18, 4);
            e.Property(x => x.ActualUnitCost).HasPrecision(18, 4);
        });

        builder.Entity<ProductionOrderMaterial>(e =>
        {
            e.Property(x => x.PlannedQuantity).HasPrecision(18, 4);
            e.Property(x => x.ActualQuantityUsed).HasPrecision(18, 4);
            e.Property(x => x.UnitCostAtConsumption).HasPrecision(18, 4);
        });

        builder.Entity<FinishedBatch>(e =>
            e.Property(x => x.UnitCost).HasPrecision(18, 4));

        builder.Entity<SaleItem>(e =>
        {
            e.Property(x => x.TaxRate).HasPrecision(18, 4);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
        });
    }

    private static void ConfigureIndexes(ModelBuilder builder)
    {
        builder.Entity<RawMaterial>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<PurchaseOrder>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<ProductionOrder>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<FinishedBatch>().HasIndex(x => x.BatchCode).IsUnique();
        builder.Entity<FinishedBatch>().HasIndex(x => x.QualityStatus);
        builder.Entity<FinishedBatch>().HasIndex(x => new { x.ProductId, x.ExpirationDate });
        builder.Entity<Sale>().HasIndex(x => x.SaleNumber).IsUnique();
        builder.Entity<AccountingAccount>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<JournalEntry>().HasIndex(x => x.EntryNumber).IsUnique();
    }

    private static void ConfigureRelationships(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PurchaseOrderItem>()
            .HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SupplierPayment>()
            .HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FormulaItem>()
            .HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrder>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrder>()
            .HasOne(x => x.ProductFormula).WithMany().HasForeignKey(x => x.ProductFormulaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrderMaterial>()
            .HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FinishedBatch>()
            .HasOne(x => x.Product).WithMany(p => p.FinishedBatches).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<FinishedBatch>()
            .HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CustomerPayment>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Sale>()
            .HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleItem>()
            .HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleItem>()
            .HasOne(x => x.FinishedBatch).WithMany().HasForeignKey(x => x.FinishedBatchId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AccountingAccount>()
            .HasOne(x => x.ParentAccount).WithMany(x => x.SubAccounts).HasForeignKey(x => x.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<JournalEntry>()
            .HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<JournalEntryLine>()
            .HasOne(x => x.Account).WithMany(x => x.JournalLines).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
