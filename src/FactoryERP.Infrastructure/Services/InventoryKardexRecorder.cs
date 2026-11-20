using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Data;

namespace FactoryERP.Infrastructure.Services;

internal static class InventoryKardexRecorder
{
    public static void RecordRawMaterial(
        AppDbContext context,
        RawMaterial material,
        InventoryMovementType movementType,
        decimal quantityDelta,
        decimal unitCost,
        string reference,
        string notes = "",
        string? performedBy = null,
        DateTime? movementDate = null)
    {
        decimal roundedDelta = Math.Round(quantityDelta, 4, MidpointRounding.AwayFromZero);
        decimal roundedUnitCost = Math.Round(Math.Max(0m, unitCost), 4, MidpointRounding.AwayFromZero);
        decimal totalValue = Math.Round(Math.Abs(roundedDelta) * roundedUnitCost, 2, MidpointRounding.AwayFromZero);

        context.InventoryMovements.Add(new InventoryMovement
        {
            MovementDate = movementDate ?? DateTime.UtcNow,
            ItemType = InventoryItemType.RawMaterial,
            MovementType = movementType,
            RawMaterialId = material.Id,
            Reference = ProductionPlannerHelper.TruncateNotes(reference, 60),
            QuantityDelta = roundedDelta,
            BalanceAfter = Math.Round(material.CurrentStock, 4, MidpointRounding.AwayFromZero),
            UnitCost = roundedUnitCost,
            TotalValue = totalValue,
            Notes = ProductionPlannerHelper.TruncateNotes(notes),
            PerformedBy = ProductionPlannerHelper.TruncateNotes(performedBy ?? string.Empty, 120)
        });
    }

    public static void RecordFinishedBatch(
        AppDbContext context,
        FinishedBatch batch,
        InventoryMovementType movementType,
        int quantityDelta,
        int balanceAfter,
        string reference,
        string notes = "",
        string? performedBy = null,
        DateTime? movementDate = null)
    {
        decimal roundedUnitCost = Math.Round(Math.Max(0m, batch.UnitCost), 4, MidpointRounding.AwayFromZero);
        decimal totalValue = Math.Round(Math.Abs(quantityDelta) * roundedUnitCost, 2, MidpointRounding.AwayFromZero);

        context.InventoryMovements.Add(new InventoryMovement
        {
            MovementDate = movementDate ?? DateTime.UtcNow,
            ItemType = InventoryItemType.FinishedGood,
            MovementType = movementType,
            ProductId = batch.ProductId,
            FinishedBatch = batch.Id == 0 ? batch : null,
            FinishedBatchId = batch.Id > 0 ? batch.Id : null,
            BatchCode = ProductionPlannerHelper.TruncateNotes(batch.BatchCode, 40),
            Reference = ProductionPlannerHelper.TruncateNotes(reference, 60),
            QuantityDelta = quantityDelta,
            BalanceAfter = balanceAfter,
            UnitCost = roundedUnitCost,
            TotalValue = totalValue,
            Notes = ProductionPlannerHelper.TruncateNotes(notes),
            PerformedBy = ProductionPlannerHelper.TruncateNotes(performedBy ?? string.Empty, 120)
        });
    }
}
