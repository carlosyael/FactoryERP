namespace FactoryERP.Core.Models;

public sealed record PublicCatalogItemDto(
    int ProductId,
    string Sku,
    string Name,
    string Line,
    string Presentation,
    string Description,
    string Benefits,
    string UsageInstructions,
    string KeyIngredients,
    string ImageUrl,
    decimal RetailPrice,
    decimal SalonPrice,
    decimal WholesalePrice,
    int AvailableStock,
    bool InStock);

public sealed record InternalCatalogItemDto(
    int ProductId,
    string Sku,
    string Name,
    string Line,
    string Presentation,
    string ImageUrl,
    decimal Cost,
    decimal RetailPrice,
    decimal SalonPrice,
    decimal WholesalePrice,
    decimal GrossMarginPercent,
    int AvailableStock,
    int MinimumStockLevel,
    bool ShowInPublicCatalog,
    bool IsActive,
    DateTime? NearestExpirationDate);

public sealed record DashboardKpiDto(
    int ActiveProductsCount,
    int LowStockRawMaterialsCount,
    int LowStockProductsCount,
    int ActiveProductionOrdersCount,
    decimal MonthlySalesTotal,
    decimal TotalAccountsReceivable,
    decimal TotalAccountsPayable,
    decimal AverageActualLossPercent,
    decimal FinishedInventoryValue,
    decimal RawMaterialInventoryValue);
