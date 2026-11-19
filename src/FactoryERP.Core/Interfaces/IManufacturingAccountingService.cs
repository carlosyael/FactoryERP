using FactoryERP.Core.Entities;

namespace FactoryERP.Core.Interfaces;

public interface IManufacturingAccountingService
{
    Task<JournalEntry> PostPurchaseReceiptAsync(PurchaseOrder purchaseOrder, string? userId = null);
    Task<JournalEntry> PostSupplierPaymentAsync(SupplierPayment payment, string? userId = null);
    Task<JournalEntry> PostProductionStartAsync(ProductionOrder order, decimal materialCost, string? userId = null);
    Task<JournalEntry> PostProductionCompletionAsync(ProductionOrder order, string? userId = null);
    Task<JournalEntry> PostProductionCancellationAsync(ProductionOrder order, string? userId = null);
    Task<JournalEntry> PostSaleAsync(Sale sale, string? userId = null);
    Task<JournalEntry> PostCustomerPaymentAsync(CustomerPayment payment, string? userId = null);
}
