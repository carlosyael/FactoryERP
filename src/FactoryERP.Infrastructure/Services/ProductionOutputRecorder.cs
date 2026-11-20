using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FactoryERP.Infrastructure.Services;

internal static class ProductionOutputRecorder
{
    public static async Task RecordCompletionOutputAsync(
        AppDbContext context,
        ProductionOrder order,
        int actualYieldQuantity,
        decimal actualUnitCost,
        bool requireQualityApproval,
        string? performedBy)
    {
        if (order.OutputRawMaterialId.HasValue)
        {
            var outputMat = await context.RawMaterials.FindAsync(order.OutputRawMaterialId.Value)
                ?? throw new InvalidOperationException("Material semielaborado/granel de destino no encontrado.");

            outputMat.AverageCost = ProductionCostCalculator.CalculateWeightedAverageCost(
                Math.Max(0m, outputMat.CurrentStock), outputMat.AverageCost, actualYieldQuantity, actualUnitCost);
            outputMat.CurrentStock = Math.Round(outputMat.CurrentStock + actualYieldQuantity, 4, MidpointRounding.AwayFromZero);

            context.RawMaterialBatches.Add(new RawMaterialBatch
            {
                RawMaterialId = outputMat.Id,
                SourceProductionOrderId = order.Id,
                BatchCode = order.BatchCode,
                ReceivedDate = DateTime.UtcNow,
                ExpirationDate = order.ExpirationDate,
                InitialQuantity = actualYieldQuantity,
                QuantityRemaining = actualYieldQuantity,
                UnitCost = actualUnitCost,
                Notes = ProductionPlannerHelper.TruncateNotes($"Granel producido orden {order.OrderNumber}", 300)
            });

            InventoryKardexRecorder.RecordRawMaterial(
                context, outputMat, InventoryMovementType.ProductionBatchOutput, actualYieldQuantity, actualUnitCost,
                order.OrderNumber, $"Granel producido lote {order.BatchCode}", performedBy, batchCode: order.BatchCode);
            return;
        }

        int productId = order.ProductId
            ?? throw new InvalidOperationException("Producto terminado no especificado.");
        var product = await context.Products.FindAsync(productId)
            ?? throw new InvalidOperationException("Producto terminado no encontrado.");
        int existingStock = await context.FinishedBatches
            .Where(b => b.ProductId == productId && b.QuantityRemaining > 0)
            .SumAsync(b => b.QuantityRemaining);

        product.Cost = ProductionCostCalculator.CalculateWeightedAverageCost(
            existingStock, product.Cost, actualYieldQuantity, actualUnitCost);

        var newBatch = new FinishedBatch
        {
            ProductId = productId,
            ProductionOrderId = order.Id,
            BatchCode = order.BatchCode,
            ProductionDate = DateTime.UtcNow,
            ExpirationDate = order.ExpirationDate,
            InitialQuantity = actualYieldQuantity,
            QuantityRemaining = actualYieldQuantity,
            UnitCost = actualUnitCost,
            QualityStatus = requireQualityApproval ? BatchQualityStatus.Quarantine : BatchQualityStatus.Approved
        };
        context.FinishedBatches.Add(newBatch);
        InventoryKardexRecorder.RecordFinishedBatch(
            context, newBatch, InventoryMovementType.ProductionBatchOutput, actualYieldQuantity,
            actualYieldQuantity, order.OrderNumber, $"Producción lote {order.BatchCode}", performedBy);
    }
}
