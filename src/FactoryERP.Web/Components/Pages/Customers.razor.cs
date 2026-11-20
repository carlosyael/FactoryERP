using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Customers
{
    [Inject] private ICustomerService CustomerService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<Customer> _customers = new();
    private Customer _editing = new();
    private bool _showForm;
    private bool _showPay;
    private Customer? _payTarget;
    private decimal _payAmount;
    private PaymentMethod _payMethod = PaymentMethod.Cash;
    private string _payRef = "";

    private bool _showHistory;
    private Customer? _historyTarget;
    private List<CustomerPayment> _payments = new();

    protected override async Task OnInitializedAsync() => _customers = await CustomerService.GetAllAsync(true);

    private void OpenNewModal() { _editing = new Customer(); _showForm = true; }
    private void OpenEditModal(Customer c)
    {
        _editing = new Customer
        {
            Id = c.Id, Name = c.Name, RncCedula = c.RncCedula, CustomerType = c.CustomerType,
            Phone = c.Phone, WhatsApp = c.WhatsApp, Email = c.Email, Address = c.Address,
            CreditLimit = c.CreditLimit, CreditDays = c.CreditDays, IsActive = c.IsActive
        };
        _showForm = true;
    }

    private void OpenPayModal(Customer c)
    {
        _payTarget = c;
        _payAmount = c.CurrentBalance;
        _payRef = $"COB-{DateTime.Now:yyyyMMdd}";
        _showPay = true;
    }

    private async Task OpenHistoryModalAsync(Customer c)
    {
        _historyTarget = c;
        _payments = await CustomerService.GetPaymentsAsync(c.Id);
        _showHistory = true;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_editing.Id == 0) await CustomerService.CreateAsync(_editing);
            else await CustomerService.UpdateAsync(_editing);
            _showForm = false;
            _customers = await CustomerService.GetAllAsync(true);
            await Swal.FireAsync("Guardado", "Cliente actualizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task ConfirmPayAsync()
    {
        if (_payTarget == null) return;
        try
        {
            await CustomerService.RegisterPaymentAsync(_payTarget.Id, _payAmount, _payMethod, _payRef);
            _showPay = false;
            _customers = await CustomerService.GetAllAsync(true);
            await Swal.FireAsync("Cobro registrado", "Saldo CxC y contabilidad actualizados.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }
}
