using CurrieTechnologies.Razor.SweetAlert2;
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
    private decimal _defaultLossMargin = 5m;
    private ProductionSimulationDto? _simulation;
    private bool _showCreate;
    private int _selectedFormulaId;
    private int _plannedQuantity = 10;
    private decimal? _lossMargin;
    private string _batchCode = "";

    private bool _showComplete;
    private ProductionOrder? _completeTarget;
    private int _actualYield;
    private decimal _actualOverhead;
    private DateTime _expirationDate = DateTime.Today.AddMonths(18);

    protected override async Task OnInitializedAsync()
    {
        _defaultLossMargin = (await BusinessProfileService.GetProfileAsync()).DefaultLossMarginPercent;
        _orders = await ProductionOrderService.GetAllAsync();
        _formulas = await FormulaService.GetAllAsync();
    }

    private async Task OpenCreateModal()
    {
        var first = _formulas.FirstOrDefault();
        _selectedFormulaId = first?.Id ?? 0;
        _plannedQuantity = first?.BaseYieldQuantity ?? 10;
        _lossMargin = first?.StandardLossMarginPercent ?? _defaultLossMargin;
        _batchCode = "";
        _showCreate = true;
        if (_selectedFormulaId > 0) await SimulateAsync();
    }

    private async Task OnFormulaChangedAsync()
    {
        var formula = _formulas.FirstOrDefault(f => f.Id == _selectedFormulaId);
        if (formula != null)
        {
            _plannedQuantity = formula.BaseYieldQuantity;
            _lossMargin = formula.StandardLossMarginPercent;
            await SimulateAsync();
        }
    }

    private async Task SimulateAsync()
    {
        try
        {
            _simulation = await ProductionOrderService.SimulateAsync(_selectedFormulaId, _plannedQuantity, _lossMargin);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task CreateDraftAsync()
    {
        try
        {
            await ProductionOrderService.CreateDraftAsync(new CreateProductionOrderRequest(
                _selectedFormulaId, _plannedQuantity, _lossMargin, DateTime.UtcNow, DateTime.UtcNow.AddMonths(18), _batchCode));
            _showCreate = false;
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Orden creada", "Orden planificada en borrador.", SweetAlertIcon.Success);
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
        _completeTarget = o;
        _actualYield = o.ExpectedResultingQuantity;
        _actualOverhead = o.ActualOverheadCost;
        _expirationDate = o.ExpirationDate ?? DateTime.Today.AddMonths(18);
        _showComplete = true;
    }

    private async Task ConfirmCompleteAsync()
    {
        if (_completeTarget == null) return;
        LoadingService.Show("Cerrando lote...");
        try
        {
            await ProductionOrderService.CompleteProductionAsync(new CompleteProductionRequest(
                _completeTarget.Id, _actualYield, _actualOverhead, _expirationDate));
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
            Icon = SweetAlertIcon.Warning,
            ShowCancelButton = true,
            ConfirmButtonText = "Sí, cancelar",
            CancelButtonText = "Volver"
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
}
