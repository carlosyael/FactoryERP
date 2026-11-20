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
    public DbSet<RawMaterialBatch> RawMaterialBatches => Set<RawMaterialBatch>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductFormula> ProductFormulas => Set<ProductFormula>();
    public DbSet<FormulaItem> FormulaItems => Set<FormulaItem>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<ProductionMaterialBatchAllocation> ProductionMaterialBatchAllocations => Set<ProductionMaterialBatchAllocation>();
    public DbSet<FinishedBatch> FinishedBatches => Set<FinishedBatch>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

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
        AppDbContextConfiguration.Configure(builder);
    }
}
