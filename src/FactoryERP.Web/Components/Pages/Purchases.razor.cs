using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Purchases
{
    [Inject] private IPurchaseOrderService PurchaseOrderService { get; set; } = default!;
    [Inject] private ISupplierService SupplierService { get; set; } = default!;
    [Inject] private IRawMaterialService RawMaterialService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<PurchaseOrder> _orders = new();
    private List<Supplier> _suppliers = new();
    private List<RawMaterial> _materials = new();
    private bool _showModal;
    private int _supplierId;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private string _notes = "";
    private List<EditableLine> _lines = new();

    protected override async Task OnInitializedAsync()
    {
        _orders = await PurchaseOrderService.GetAllAsync();
        _suppliers = await SupplierService.GetAllAsync();
        _materials = await RawMaterialService.GetAllAsync();
    }

    private void OpenNewModal()
    {
        _supplierId = _suppliers.FirstOrDefault()?.Id ?? 0;
        _lines = _materials.Count > 0 ? [new() { RawMaterialId = _materials[0].Id, Quantity = 10, UnitCost = _materials[0].AverageCost }] : [];
        _notes = "";
        _showModal = true;
    }

    private void AddLine()
    {
        if (_materials.Count > 0) _lines.Add(new() { RawMaterialId = _materials[0].Id, Quantity = 1, UnitCost = _materials[0].AverageCost });
    }

    private async Task SaveAsync()
    {
        try
        {
            var req = new ReceivePurchaseRequest(
                _supplierId, _paymentMethod, DateTime.UtcNow, _notes,
                _lines.Select(l => new CreatePurchaseOrderItemRequest(l.RawMaterialId, l.Quantity, l.UnitCost, l.IsTaxable)).ToList());
            await PurchaseOrderService.ReceivePurchaseAsync(req);
            _showModal = false;
            _orders = await PurchaseOrderService.GetAllAsync();
            await Swal.FireAsync("Compra registrada", "Stock, CPP y asiento contable actualizados.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private sealed class EditableLine
    {
        public int RawMaterialId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public bool IsTaxable { get; set; } = true;
    }
}
