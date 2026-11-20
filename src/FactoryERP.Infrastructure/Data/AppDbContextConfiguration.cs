using FactoryERP.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Data;

internal static class AppDbContextConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
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
        builder.Entity<RawMaterialBatch>(e =>
        {
            e.Property(x => x.InitialQuantity).HasPrecision(18, 4);
            e.Property(x => x.QuantityRemaining).HasPrecision(18, 4);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
        });
        builder.Entity<ProductionMaterialBatchAllocation>(e =>
            e.Property(x => x.QuantityAllocated).HasPrecision(18, 4));
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
        builder.Entity<ProductFormula>(e => e.Property(x => x.StandardLossMarginPercent).HasPrecision(18, 4));
        builder.Entity<FormulaItem>(e => e.Property(x => x.QuantityRequired).HasPrecision(18, 4));
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
        builder.Entity<FinishedBatch>(e => e.Property(x => x.UnitCost).HasPrecision(18, 4));
        builder.Entity<SaleItem>(e =>
        {
            e.Property(x => x.TaxRate).HasPrecision(18, 4);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
        });
        builder.Entity<InventoryMovement>(e =>
        {
            e.Property(x => x.QuantityDelta).HasPrecision(18, 4);
            e.Property(x => x.BalanceAfter).HasPrecision(18, 4);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
        });
    }

    private static void ConfigureIndexes(ModelBuilder builder)
    {
        builder.Entity<RawMaterial>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<RawMaterialBatch>().HasIndex(x => x.BatchCode);
        builder.Entity<RawMaterialBatch>().HasIndex(x => new { x.RawMaterialId, x.ExpirationDate });
        builder.Entity<ProductionMaterialBatchAllocation>().HasIndex(x => new { x.ProductionOrderId, x.RawMaterialBatchId });
        builder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        builder.Entity<PurchaseOrder>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<ProductionOrder>().HasIndex(x => x.OrderNumber).IsUnique();
        builder.Entity<FinishedBatch>().HasIndex(x => x.BatchCode).IsUnique();
        builder.Entity<FinishedBatch>().HasIndex(x => x.QualityStatus);
        builder.Entity<FinishedBatch>().HasIndex(x => new { x.ProductId, x.ExpirationDate });
        builder.Entity<InventoryMovement>().HasIndex(x => x.MovementDate);
        builder.Entity<InventoryMovement>().HasIndex(x => new { x.ItemType, x.MovementDate });
        builder.Entity<InventoryMovement>().HasIndex(x => x.RawMaterialId);
        builder.Entity<InventoryMovement>().HasIndex(x => x.ProductId);
        builder.Entity<Sale>().HasIndex(x => x.SaleNumber).IsUnique();
        builder.Entity<AccountingAccount>().HasIndex(x => x.Code).IsUnique();
        builder.Entity<JournalEntry>().HasIndex(x => x.EntryNumber).IsUnique();
    }

    private static void ConfigureRelationships(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PurchaseOrderItem>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RawMaterialBatch>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RawMaterialBatch>().HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SupplierPayment>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FormulaItem>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrder>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrder>().HasOne(x => x.ProductFormula).WithMany().HasForeignKey(x => x.ProductFormulaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionOrderMaterial>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionMaterialBatchAllocation>().HasOne(x => x.ProductionOrder).WithMany(o => o.BatchAllocations).HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ProductionMaterialBatchAllocation>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ProductionMaterialBatchAllocation>().HasOne(x => x.RawMaterialBatch).WithMany().HasForeignKey(x => x.RawMaterialBatchId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FinishedBatch>().HasOne(x => x.Product).WithMany(p => p.FinishedBatches).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<FinishedBatch>().HasOne(x => x.ProductionOrder).WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InventoryMovement>().HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InventoryMovement>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InventoryMovement>().HasOne(x => x.FinishedBatch).WithMany().HasForeignKey(x => x.FinishedBatchId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CustomerPayment>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Sale>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleItem>().HasOne(x => x.FinishedBatch).WithMany().HasForeignKey(x => x.FinishedBatchId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<AccountingAccount>().HasOne(x => x.ParentAccount).WithMany(x => x.SubAccounts).HasForeignKey(x => x.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<JournalEntry>().HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<JournalEntryLine>().HasOne(x => x.Account).WithMany(x => x.JournalLines).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
