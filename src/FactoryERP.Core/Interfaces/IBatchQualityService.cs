using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

public interface IBatchQualityService
{
    Task<FinishedBatch> UpdateQualityStatusAsync(UpdateBatchQualityRequest request);
    Task<FinishedBatch> AdjustBatchQuantityAsync(AdjustBatchQuantityRequest request);
}
