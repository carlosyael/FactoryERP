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
