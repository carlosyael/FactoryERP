using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Suppliers
{
    [Inject] private ISupplierService SupplierService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<Supplier> _suppliers = new();
    private Supplier _editing = new();
    private bool _showForm;
    private bool _showPay;
    private Supplier? _payTarget;
    private decimal _payAmount;
    private PaymentMethod _payMethod = PaymentMethod.BankTransfer;
    private string _payRef = "";

    private bool _showHistory;
    private Supplier? _historyTarget;
    private List<SupplierPayment> _payments = new();

    protected override async Task OnInitializedAsync() => _suppliers = await SupplierService.GetAllAsync(true);

    private void OpenNewModal() { _editing = new Supplier(); _showForm = true; }
    private void OpenEditModal(Supplier s)
    {
        _editing = new Supplier { Id = s.Id, Name = s.Name, Rnc = s.Rnc, Phone = s.Phone, Email = s.Email, ContactPerson = s.ContactPerson, Address = s.Address, IsActive = s.IsActive };
        _showForm = true;
    }

    private void OpenPayModal(Supplier s)
    {
        _payTarget = s;
        _payAmount = s.CurrentBalance;
        _payRef = $"PAG-{DateTime.Now:yyyyMMdd}";
        _showPay = true;
    }

    private async Task OpenHistoryModalAsync(Supplier s)
    {
        _historyTarget = s;
        _payments = await SupplierService.GetPaymentsAsync(s.Id);
        _showHistory = true;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_editing.Id == 0) await SupplierService.CreateAsync(_editing);
            else await SupplierService.UpdateAsync(_editing);
            _showForm = false;
            _suppliers = await SupplierService.GetAllAsync(true);
            await Swal.FireAsync("Guardado", "Suplidor actualizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task ConfirmPayAsync()
    {
        if (_payTarget == null) return;
        try
        {
            await SupplierService.RegisterPaymentAsync(_payTarget.Id, _payAmount, _payMethod, _payRef);
            _showPay = false;
            _suppliers = await SupplierService.GetAllAsync(true);
            await Swal.FireAsync("Pago registrado", "CxP y asiento contable actualizados.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }
}
