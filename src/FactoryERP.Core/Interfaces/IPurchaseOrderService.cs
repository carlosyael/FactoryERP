using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrder>> GetAllAsync();
    Task<PurchaseOrder?> GetByIdAsync(int id);
    Task<PurchaseOrder> ReceivePurchaseAsync(ReceivePurchaseRequest request);
}
