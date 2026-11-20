using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FactoryERP.Web.Components.Pages;

public partial class InventoryKardex
{
    [Inject] private IInventoryKardexService KardexService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "rawMaterialId")] public int? RawMaterialId { get; set; }
    [SupplyParameterFromQuery(Name = "productId")] public int? ProductId { get; set; }
    [SupplyParameterFromQuery(Name = "search")] public string? SearchQuery { get; set; }

    private List<InventoryMovement> _movements = new();
    private string _currencySymbol = "RD$";
    private string _itemTypeFilter = string.Empty;
    private string _movementTypeFilter = string.Empty;
    private int? _rawMaterialIdFilter;
    private int? _productIdFilter;
    private DateTime _startDate = DateTime.UtcNow.Date.AddDays(-30);
    private DateTime _endDate = DateTime.UtcNow.Date;
    private string _search = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
    }

    protected override async Task OnParametersSetAsync()
    {
        _rawMaterialIdFilter = RawMaterialId;
        _productIdFilter = ProductId;
        if (!string.IsNullOrWhiteSpace(SearchQuery)) _search = SearchQuery;
        if (_rawMaterialIdFilter.HasValue) _itemTypeFilter = nameof(InventoryItemType.RawMaterial);
        else if (_productIdFilter.HasValue) _itemTypeFilter = nameof(InventoryItemType.FinishedGood);
        await LoadAsync();
    }

    private async Task OnItemTypeChangedAsync()
    {
        if (_itemTypeFilter == nameof(InventoryItemType.RawMaterial)) _productIdFilter = null;
        else if (_itemTypeFilter == nameof(InventoryItemType.FinishedGood)) _rawMaterialIdFilter = null;
        else
        {
            _rawMaterialIdFilter = null;
            _productIdFilter = null;
        }
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        InventoryItemType? itemType = Enum.TryParse<InventoryItemType>(_itemTypeFilter, out var it) ? it : null;
        InventoryMovementType? movType = Enum.TryParse<InventoryMovementType>(_movementTypeFilter, out var mt) ? mt : null;
        _movements = await KardexService.GetMovementsAsync(new InventoryMovementFilter(
            itemType, movType, _rawMaterialIdFilter, _productIdFilter, _startDate, _endDate, _search));
    }

    private async Task ClearRawMaterialFilterAsync()
    {
        _rawMaterialIdFilter = null;
        await LoadAsync();
    }

    private async Task ClearProductFilterAsync()
    {
        _productIdFilter = null;
        await LoadAsync();
    }

    private async Task ClearFiltersAsync()
    {
        _itemTypeFilter = string.Empty;
        _movementTypeFilter = string.Empty;
        _rawMaterialIdFilter = null;
        _productIdFilter = null;
        _startDate = DateTime.UtcNow.Date.AddDays(-30);
        _endDate = DateTime.UtcNow.Date;
        _search = string.Empty;
        if (RawMaterialId.HasValue || ProductId.HasValue || !string.IsNullOrWhiteSpace(SearchQuery))
        {
            Navigation.NavigateTo("/kardex");
            return;
        }
        await LoadAsync();
    }

    private async Task PrintAsync() => await JSRuntime.InvokeVoidAsync("printPage");

    private static string GetItemTitle(InventoryMovement m) => m.ItemType == InventoryItemType.RawMaterial
        ? $"{m.RawMaterial?.Sku} - {m.RawMaterial?.Name}".Trim(' ', '-')
        : $"{m.Product?.Sku} - {m.Product?.Name}".Trim(' ', '-');

    private static string GetUnitLabel(InventoryMovement m) => m.ItemType == InventoryItemType.RawMaterial
        ? (m.RawMaterial != null ? UnitConverter.GetShortLabel(m.RawMaterial.UnitOfMeasure) : "ud")
        : "uds";

    private static string GetMovementBadge(InventoryMovement m) => m.QuantityDelta >= 0m
        ? "bg-success-subtle text-success"
        : "bg-danger-subtle text-danger";

    private static string GetMovementLabel(InventoryMovementType t) => t switch
    {
        InventoryMovementType.PurchaseReceipt => "Entrada Compra",
        InventoryMovementType.RawMaterialAdjustment => "Ajuste MP",
        InventoryMovementType.ProductionConsumption => "Consumo OP",
        InventoryMovementType.ProductionMaterialReconciliation => "Reconciliación OP",
        InventoryMovementType.ProductionCancellationReturn => "Devolución Cancelación",
        InventoryMovementType.ProductionBatchOutput => "Entrada Producción",
        InventoryMovementType.SaleDispatch => "Salida Venta",
        InventoryMovementType.FinishedBatchAdjustment => "Ajuste / Baja Lote",
        _ => t.ToString()
    };
}
