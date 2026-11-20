using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Models;

public sealed record ProductionMaterialRequirementDto(
    int RawMaterialId,
    string Sku,
    string Name,
    UnitOfMeasure MaterialUnit,
    decimal RequiredQuantity,
    decimal AvailableStock,
    decimal UnitCost,
    decimal EstimatedTotalCost,
    bool HasSufficientStock);

public sealed record ProductionSimulationDto(
    int ProductId,
    string ProductName,
    int FormulaId,
    string FormulaName,
    string FormulaVersion,
    int PlannedQuantity,
    decimal ExpectedLossMarginPercent,
    int ExpectedLossQuantity,
    int ExpectedResultingQuantity,
    decimal EstimatedMaterialCost,
    decimal EstimatedOverheadCost,
    decimal EstimatedTotalCost,
    decimal EstimatedUnitCost,
    bool CanProduce,
    IReadOnlyList<ProductionMaterialRequirementDto> Materials);

public sealed record CreateProductionOrderRequest(
    int ProductFormulaId,
    int PlannedQuantity,
    decimal? OverrideLossMarginPercent,
    DateTime? PlannedDate,
    DateTime? ExpirationDate,
    string? BatchCode,
    string Notes = "");

public sealed record UpdateProductionOrderRequest(
    int ProductionOrderId,
    int ProductFormulaId,
    int PlannedQuantity,
    decimal? OverrideLossMarginPercent,
    DateTime? PlannedDate,
    DateTime? ExpirationDate,
    string? BatchCode,
    string Notes = "");

public sealed record CompleteProductionRequest(
    int ProductionOrderId,
    int ActualYieldQuantity,
    decimal? ActualOverheadCost,
    DateTime? ExpirationDate,
    string? Notes = null,
    IReadOnlyDictionary<int, decimal>? ActualMaterialQuantities = null);

public sealed record UpdateBatchQualityRequest(
    int BatchId,
    BatchQualityStatus Status,
    string Notes = "");

public sealed record AdjustBatchQuantityRequest(
    int BatchId,
    int NewQuantity,
    string Reason);
