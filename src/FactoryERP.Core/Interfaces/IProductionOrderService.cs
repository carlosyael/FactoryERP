using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IProductionOrderService
{
    Task<List<ProductionOrder>> GetAllAsync();
    Task<ProductionOrder?> GetByIdAsync(int id);
    Task<ProductionSimulationDto> SimulateAsync(int formulaId, int plannedQuantity, decimal? overrideLossMarginPercent = null);
    Task<ProductionOrder> CreateDraftAsync(CreateProductionOrderRequest request);
    Task<ProductionOrder> UpdateDraftAsync(UpdateProductionOrderRequest request);
    Task<ProductionOrder> StartProductionAsync(int orderId);
    Task<ProductionOrder> CompleteProductionAsync(CompleteProductionRequest request);
    Task<ProductionOrder> CancelProductionAsync(int orderId, string reason);
}
