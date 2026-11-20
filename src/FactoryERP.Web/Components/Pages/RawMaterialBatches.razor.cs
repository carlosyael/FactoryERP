using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class RawMaterialBatches
{
    [Inject] private IRawMaterialBatchService BatchService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "rawMaterialId")] public int? RawMaterialId { get; set; }

    private List<RawMaterialBatch> _batches = new();
    private string _currencySymbol = "RD$";
    private int? _rawMaterialIdFilter;
    private string _search = string.Empty;
    private bool _onlyWithStock;
    private RawMaterialBatchRecallDto? _recallData;

    private IEnumerable<RawMaterialBatch> FilteredBatches => _batches.Where(b =>
        (!_onlyWithStock || b.QuantityRemaining > 0m) &&
        (string.IsNullOrWhiteSpace(_search) ||
         b.BatchCode.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ||
         (b.RawMaterial?.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
         (b.RawMaterial?.Sku.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
         (b.PurchaseOrder?.OrderNumber.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
         (b.PurchaseOrder?.Supplier?.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)));

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
    }

    protected override async Task OnParametersSetAsync()
    {
        _rawMaterialIdFilter = RawMaterialId;
        await LoadAsync();
    }

    private async Task LoadAsync() => _batches = await BatchService.GetBatchesAsync(_rawMaterialIdFilter);

    private async Task ClearMaterialFilterAsync()
    {
        _rawMaterialIdFilter = null;
        if (RawMaterialId.HasValue)
        {
            Navigation.NavigateTo("/raw-material-batches");
            return;
        }
        await LoadAsync();
    }

    private async Task OpenRecallModalAsync(int batchId) =>
        _recallData = await BatchService.GetBatchTraceabilityAsync(batchId);
}
