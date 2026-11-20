using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Web.Services;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class ProductionOrders
{
    [Inject] private IProductionOrderService ProductionOrderService { get; set; } = default!;
    [Inject] private IFormulaService FormulaService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private ILoadingService LoadingService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<ProductionOrder> _orders = new();
    private List<ProductFormula> _formulas = new();
    private string _currencySymbol = "RD$";
    private decimal _defaultLossMargin = 5m;
    private string _search = string.Empty;
    private string _statusFilter = string.Empty;
    private ProductionOrder? _detailOrder;
    private ProductionSimulationDto? _simulation;
    private bool _showCreate, _showComplete;
    private int _editingOrderId, _selectedFormulaId, _plannedQuantity = 10, _actualYield;
    private string _editingOrderNumber = string.Empty, _batchCode = string.Empty, _draftNotes = string.Empty, _completionNotes = string.Empty;
    private decimal? _lossMargin;
    private decimal _actualOverhead;
    private DateTime _plannedDate = DateTime.Today, _draftExpirationDate = DateTime.Today.AddMonths(18), _expirationDate = DateTime.Today.AddMonths(18);
    private ProductionOrder? _completeTarget;
    private List<MaterialConsumptionInput> _completeLines = new();

    private IEnumerable<ProductionOrder> FilteredOrders => _orders.Where(o =>
        (string.IsNullOrWhiteSpace(_statusFilter) || (Enum.TryParse<ProductionOrderStatus>(_statusFilter, out var st) && o.Status == st)) &&
        (string.IsNullOrWhiteSpace(_search) || o.OrderNumber.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ||
         o.BatchCode.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) || (o.Product?.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)));

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
        _defaultLossMargin = profile.DefaultLossMarginPercent;
        _orders = await ProductionOrderService.GetAllAsync();
        _formulas = await FormulaService.GetAllAsync();
    }

    private async Task OpenCreateModal()
    {
        var first = _formulas.FirstOrDefault();
        await InitDraftModalAsync(0, "", first?.Id ?? 0, first?.BaseYieldQuantity ?? 10,
            first?.StandardLossMarginPercent ?? _defaultLossMargin, "", DateTime.Today, DateTime.Today.AddMonths(18), "");
    }

    private Task OpenEditDraftModal(ProductionOrder o) => InitDraftModalAsync(
        o.Id, o.OrderNumber, o.ProductFormulaId, o.PlannedQuantity, o.ExpectedLossMarginPercent, o.BatchCode,
        o.PlannedDate.ToLocalTime().Date, (o.ExpirationDate ?? o.PlannedDate.AddMonths(18)).ToLocalTime().Date, o.Notes);

    private async Task InitDraftModalAsync(int id, string num, int formulaId, int qty, decimal? loss, string batch, DateTime planned, DateTime exp, string notes)
    {
        _editingOrderId = id; _editingOrderNumber = num; _selectedFormulaId = formulaId;
        _plannedQuantity = qty; _lossMargin = loss; _batchCode = batch;
        _plannedDate = planned; _draftExpirationDate = exp; _draftNotes = notes;
        _simulation = null; _showCreate = true;
        if (_selectedFormulaId > 0) await SimulateAsync();
    }

    private async Task OnFormulaChangedAsync()
    {
        if (_formulas.FirstOrDefault(f => f.Id == _selectedFormulaId) is not { } f) return;
        _plannedQuantity = f.BaseYieldQuantity;
        _lossMargin = f.StandardLossMarginPercent;
        await SimulateAsync();
    }

    private async Task SimulateAsync()
    {
        try { _simulation = await ProductionOrderService.SimulateAsync(_selectedFormulaId, _plannedQuantity, _lossMargin); }
        catch (Exception ex) { _simulation = null; await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task SaveDraftAsync()
    {
        try
        {
            var plannedUtc = DateTime.SpecifyKind(_plannedDate.Date, DateTimeKind.Utc);
            var expirationUtc = DateTime.SpecifyKind(_draftExpirationDate.Date, DateTimeKind.Utc);
            if (_editingOrderId == 0)
                await ProductionOrderService.CreateDraftAsync(new(_selectedFormulaId, _plannedQuantity, _lossMargin, plannedUtc, expirationUtc, _batchCode, _draftNotes));
            else
                await ProductionOrderService.UpdateDraftAsync(new(_editingOrderId, _selectedFormulaId, _plannedQuantity, _lossMargin, plannedUtc, expirationUtc, _batchCode, _draftNotes));

            _showCreate = false;
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync(_editingOrderId == 0 ? "Orden creada" : "Borrador actualizado", "Orden planificada correctamente.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task StartAsync(int orderId)
    {
        LoadingService.Show("Iniciando producción...");
        try
        {
            await ProductionOrderService.StartProductionAsync(orderId);
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Producción iniciada", "Insumos descontados y registrados en WIP.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
        finally { LoadingService.Hide(); }
    }

    private void OpenCompleteModal(ProductionOrder o)
    {
        _completeTarget = o; _actualYield = o.ExpectedResultingQuantity; _actualOverhead = o.ActualOverheadCost;
        _expirationDate = (o.ExpirationDate ?? DateTime.Today.AddMonths(18)).ToLocalTime().Date;
        _completionNotes = o.Notes;
        _completeLines = o.Materials.Select(m => new MaterialConsumptionInput(
            m.RawMaterialId, m.RawMaterial?.Name ?? $"Material #{m.RawMaterialId}",
            m.RawMaterial != null ? UnitConverter.GetShortLabel(m.RawMaterial.UnitOfMeasure) : "ud",
            m.PlannedQuantity, m.ActualQuantityUsed, m.RawMaterial?.CurrentStock ?? 0m)).ToList();
        _showComplete = true;
    }

    private async Task ConfirmCompleteAsync()
    {
        if (_completeTarget == null) return;
        LoadingService.Show("Cerrando lote...");
        try
        {
            var actualMats = _completeLines.ToDictionary(l => l.RawMaterialId, l => l.ActualQty);
            var expirationUtc = DateTime.SpecifyKind(_expirationDate.Date, DateTimeKind.Utc);
            await ProductionOrderService.CompleteProductionAsync(new(_completeTarget.Id, _actualYield, _actualOverhead, expirationUtc, _completionNotes, actualMats));
            _showComplete = false;
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Lote completado", "Producto terminado ingresado al catálogo y contabilizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
        finally { LoadingService.Hide(); }
    }

    private async Task CancelAsync(int orderId)
    {
        var confirm = await Swal.FireAsync(new SweetAlertOptions
        {
            Title = "¿Cancelar orden de producción?",
            Text = "Si la orden está en proceso, se devolverán los insumos al almacén y se revertirá el WIP.",
            Icon = SweetAlertIcon.Warning, ShowCancelButton = true, ConfirmButtonText = "Sí, cancelar", CancelButtonText = "Volver"
        });
        if (!confirm.IsConfirmed) return;
        try
        {
            await ProductionOrderService.CancelProductionAsync(orderId, "Cancelada desde panel");
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Orden cancelada", "Inventario y WIP actualizados.", SweetAlertIcon.Info);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private static string GetStatusBadge(ProductionOrderStatus status) => status switch
    {
        ProductionOrderStatus.Draft => "bg-secondary",
        ProductionOrderStatus.InProgress => "bg-warning text-dark",
        ProductionOrderStatus.Completed => "bg-success",
        _ => "bg-danger"
    };

    private sealed class MaterialConsumptionInput(int id, string name, string unitLabel, decimal plannedQty, decimal actualQty, decimal availableStock)
    {
        public int RawMaterialId { get; } = id;
        public string Name { get; } = name;
        public string UnitLabel { get; } = unitLabel;
        public decimal PlannedQty { get; } = plannedQty;
        public decimal ActualQty { get; set; } = actualQty;
        public decimal AvailableStock { get; } = availableStock;
    }
}
