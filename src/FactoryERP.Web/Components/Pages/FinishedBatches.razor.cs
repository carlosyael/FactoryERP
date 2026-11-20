using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class FinishedBatches
{
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IBatchQualityService BatchQualityService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<FinishedBatch> _batches = new();
    private string _currencySymbol = "RD$";
    private string _search = string.Empty;
    private string _qualityFilter = string.Empty;
    private bool _onlyWithStock;

    private bool _showQualityModal;
    private FinishedBatch? _qualityTarget;
    private BatchQualityStatus _qualityStatus;
    private string _qualityNotes = string.Empty;

    private bool _showAdjustModal;
    private FinishedBatch? _adjustTarget;
    private int _newBatchQty;
    private string _adjustReason = "Baja por control físico";

    private IEnumerable<FinishedBatch> FilteredBatches => _batches.Where(b =>
        (!_onlyWithStock || b.QuantityRemaining > 0) &&
        (string.IsNullOrWhiteSpace(_qualityFilter) || (Enum.TryParse<BatchQualityStatus>(_qualityFilter, out var qf) && b.QualityStatus == qf)) &&
        (string.IsNullOrWhiteSpace(_search) ||
         b.BatchCode.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ||
         (b.Product?.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)));

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
        _batches = await ProductService.GetBatchesAsync();
    }

    private void OpenQualityModal(FinishedBatch batch)
    {
        _qualityTarget = batch;
        _qualityStatus = batch.QualityStatus;
        _qualityNotes = batch.QualityNotes;
        _showQualityModal = true;
    }

    private async Task SaveQualityAsync()
    {
        if (_qualityTarget == null) return;
        try
        {
            await BatchQualityService.UpdateQualityStatusAsync(new(_qualityTarget.Id, _qualityStatus, _qualityNotes));
            _showQualityModal = false;
            _batches = await ProductService.GetBatchesAsync();
            await Swal.FireAsync("Calidad actualizada", "Estado del lote registrado correctamente.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private void OpenAdjustModal(FinishedBatch batch)
    {
        _adjustTarget = batch;
        _newBatchQty = batch.QuantityRemaining;
        _adjustReason = "Baja / merma de almacén";
        _showAdjustModal = true;
    }

    private async Task SaveAdjustAsync()
    {
        if (_adjustTarget == null) return;
        try
        {
            await BatchQualityService.AdjustBatchQuantityAsync(new(_adjustTarget.Id, _newBatchQty, _adjustReason));
            _showAdjustModal = false;
            _batches = await ProductService.GetBatchesAsync();
            await Swal.FireAsync("Lote ajustado", "Existencia y contabilidad actualizadas.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private static string GetQualityBadge(BatchQualityStatus status) => status switch
    {
        BatchQualityStatus.Approved => "bg-success",
        BatchQualityStatus.Quarantine => "bg-warning text-dark",
        _ => "bg-danger"
    };

    private static string GetQualityLabel(BatchQualityStatus status) => status switch
    {
        BatchQualityStatus.Approved => "Aprobado",
        BatchQualityStatus.Quarantine => "Cuarentena",
        _ => "Rechazado"
    };
}
