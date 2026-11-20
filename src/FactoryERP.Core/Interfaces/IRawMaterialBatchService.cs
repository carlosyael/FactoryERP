using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IRawMaterialBatchService
{
    Task<List<RawMaterialBatch>> GetBatchesAsync(int? rawMaterialId = null, bool onlyWithStock = false);
    Task<RawMaterialBatchRecallDto?> GetBatchTraceabilityAsync(int rawMaterialBatchId);
    Task<RawMaterialBatch> AdjustBatchQuantityAsync(AdjustRawMaterialBatchRequest request);
}
