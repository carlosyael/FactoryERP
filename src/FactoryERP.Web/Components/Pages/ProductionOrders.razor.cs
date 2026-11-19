using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class ProductionOrders
{
    [Inject] private IProductionOrderService ProductionOrderService { get; set; } = default!;
    [Inject] private IFormulaService FormulaService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<ProductionOrder> _orders = new();
    private List<ProductFormula> _formulas = new();
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
        _orders = await ProductionOrderService.GetAllAsync();
        _formulas = await FormulaService.GetAllAsync();
    }

    private async Task OpenCreateModal()
    {
        _selectedFormulaId = _formulas.FirstOrDefault()?.Id ?? 0;
        _plannedQuantity = _formulas.FirstOrDefault()?.BaseYieldQuantity ?? 10;
        _lossMargin = _formulas.FirstOrDefault()?.StandardLossMarginPercent ?? 5m;
        _batchCode = "";
        _showCreate = true;
        if (_selectedFormulaId > 0) await SimulateAsync();
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
        try
        {
            await ProductionOrderService.StartProductionAsync(orderId);
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Producción iniciada", "Insumos descontados y registrados en WIP.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
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
        try
        {
            await ProductionOrderService.CompleteProductionAsync(new CompleteProductionRequest(
                _completeTarget.Id, _actualYield, _actualOverhead, _expirationDate));
            _showComplete = false;
            _orders = await ProductionOrderService.GetAllAsync();
            await Swal.FireAsync("Lote completado", "Producto terminado ingresado al catálogo y contabilizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task CancelAsync(int orderId)
    {
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
