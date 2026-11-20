namespace FactoryERP.Core.Enums;

/// <summary>
/// Tipo de operación que origina una entrada o salida en el Kardex de inventario.
/// </summary>
public enum InventoryMovementType
{
    PurchaseReceipt = 0,
    RawMaterialAdjustment = 1,
    ProductionConsumption = 2,
    ProductionMaterialReconciliation = 3,
    ProductionCancellationReturn = 4,
    ProductionBatchOutput = 5,
    SaleDispatch = 6,
    FinishedBatchAdjustment = 7
}
