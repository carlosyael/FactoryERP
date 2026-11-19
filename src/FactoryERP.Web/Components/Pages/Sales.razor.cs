using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FactoryERP.Web.Components.Pages;

public partial class Sales
{
    [Inject] private ISaleService SaleService { get; set; } = default!;
    [Inject] private ICustomerService CustomerService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private ILoadingService LoadingService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private BusinessProfileDto? _profile;
    private List<Sale> _sales = new();
    private List<Customer> _customers = new();
    private List<Product> _products = new();
    private Sale? _selectedSale;
    private bool _showNewModal;
    private int _customerId;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private List<EditableSaleLine> _lines = new();

    protected override async Task OnInitializedAsync()
    {
        _profile = await BusinessProfileService.GetProfileAsync();
        _sales = await SaleService.GetAllAsync();
        _customers = await CustomerService.GetAllAsync();
        _products = await ProductService.GetAllAsync();
    }

    private void OpenNewModal()
    {
        _customerId = _customers.FirstOrDefault()?.Id ?? 0;
        _lines = _products.Count > 0 ? [new() { ProductId = _products[0].Id, Quantity = 1 }] : [];
        _showNewModal = true;
    }

    private void AddLine()
    {
        if (_products.Count > 0) _lines.Add(new() { ProductId = _products[0].Id, Quantity = 1 });
    }

    private (decimal SubTotal, decimal Discount, decimal Tax, decimal Total) ComputeSalePreview()
    {
        var customerType = _customers.FirstOrDefault(c => c.Id == _customerId)?.CustomerType ?? CustomerType.Retail;
        decimal sub = 0m, disc = 0m, tax = 0m, tot = 0m;
        decimal defaultTax = _profile?.DefaultTaxRate ?? 0.18m;
        decimal maxDisc = _profile?.MaxDiscountPercentage ?? 30m;

        foreach (var line in _lines)
        {
            var prod = _products.FirstOrDefault(p => p.Id == line.ProductId);
            if (prod == null || line.Quantity <= 0 || line.DiscountPercent < 0 || line.DiscountPercent > maxDisc) continue;
            decimal price = prod.GetPriceForCustomerType(customerType);
            decimal rate = prod.IsTaxable ? (prod.TaxRate > 0 ? prod.TaxRate : defaultTax) : 0m;
            var res = PricingCalculator.CalculateSaleLine(line.Quantity, price, line.DiscountPercent, maxDisc, prod.IsTaxable, rate);
            sub += res.GrossAmount;
            disc += res.DiscountAmount;
            tax += res.TaxAmount;
            tot += res.LineTotal;
        }
        return (sub, disc, tax, tot);
    }

    private async Task SaveSaleAsync()
    {
        LoadingService.Show("Procesando venta...");
        try
        {
            var req = new CreateSaleRequest(
                _customerId, _paymentMethod, DateTime.UtcNow, "",
                _lines.Select(l => new CreateSaleItemRequest(l.ProductId, l.Quantity, null, l.DiscountPercent)).ToList());
            _selectedSale = await SaleService.CreateSaleAsync(req);
            _showNewModal = false;
            _sales = await SaleService.GetAllAsync();
            _products = await ProductService.GetAllAsync();
            await Swal.FireAsync("Venta completada", $"Comprobante {_selectedSale.SaleNumber} emitido.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
        finally { LoadingService.Hide(); }
    }

    private async Task PrintReceiptAsync() => await JSRuntime.InvokeVoidAsync("printPage");

    private sealed class EditableSaleLine
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal DiscountPercent { get; set; }
    }
}
