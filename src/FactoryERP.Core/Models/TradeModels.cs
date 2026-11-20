using FactoryERP.Core.Enums;

namespace FactoryERP.Core.Models;

public sealed record CreatePurchaseOrderItemRequest(
    int RawMaterialId,
    decimal Quantity,
    decimal UnitCost,
    bool IsTaxable = true,
    string? BatchCode = null,
    DateTime? ExpirationDate = null);

public sealed record ReceivePurchaseRequest(
    int SupplierId,
    PaymentMethod PaymentMethod,
    DateTime? PurchaseDate,
    string Notes,
    IReadOnlyList<CreatePurchaseOrderItemRequest> Items);

public sealed record CreateSaleItemRequest(
    int ProductId,
    int Quantity,
    decimal? UnitPriceOverride = null,
    decimal DiscountPercent = 0m);

public sealed record CreateSaleRequest(
    int CustomerId,
    PaymentMethod PaymentMethod,
    DateTime? SaleDate,
    string Notes,
    IReadOnlyList<CreateSaleItemRequest> Items);

public sealed record TaxDocumentResultDto(
    ElectronicInvoiceStatus Status,
    string? DocumentNumber,
    string Message);
