using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IInventoryKardexService
{
    Task<List<InventoryMovement>> GetMovementsAsync(InventoryMovementFilter? filter = null);
}
