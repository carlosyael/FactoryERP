using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Models;

public sealed record InventoryMovementFilter(
    InventoryItemType? ItemType = null,
    InventoryMovementType? MovementType = null,
    int? RawMaterialId = null,
    int? ProductId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Search = null,
    int MaxResults = 300);

public sealed record RecallAffectedOrderDto(
    int ProductionOrderId,
    string OrderNumber,
    string OutputBatchCode,
    string ProductName,
    ProductionOrderStatus OrderStatus,
    decimal QuantityConsumed,
    int? FinishedBatchId,
    int? FinishedBatchRemaining,
    bool IsSemiFinishedOutput = false,
    string? ViaBulkBatchCode = null,
    string? ConsumedUnit = null);

public sealed record RawMaterialBatchRecallDto(
    RawMaterialBatch Batch,
    IReadOnlyList<RecallAffectedOrderDto> AffectedOrders);

public sealed record AdjustRawMaterialBatchRequest(
    int BatchId,
    decimal NewQuantity,
    string Reason);

public sealed record MaterialPlanningSuggestionDto(
    int RawMaterialId,
    string Sku,
    string Name,
    UnitOfMeasure UnitOfMeasure,
    decimal PhysicalStock,
    decimal ExpiredStock,
    decimal UsableStock,
    decimal ReservedByDrafts,
    decimal NetAvailableStock,
    decimal MinimumStockLevel,
    decimal SuggestedPurchaseQuantity,
    decimal ReferenceUnitCost,
    decimal EstimatedPurchaseSubTotal,
    bool IsManufacturedInHouse = false);
