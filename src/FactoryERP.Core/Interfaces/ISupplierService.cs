using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Interfaces;

public interface ISupplierService
{
    Task<List<Supplier>> GetAllAsync(bool includeInactive = false);
    Task<Supplier?> GetByIdAsync(int id);
    Task<Supplier> CreateAsync(Supplier supplier);
    Task UpdateAsync(Supplier supplier);
    Task<SupplierPayment> RegisterPaymentAsync(int supplierId, decimal amount, PaymentMethod method, string reference, string notes = "");
    Task<List<SupplierPayment>> GetPaymentsAsync(int supplierId);
}
