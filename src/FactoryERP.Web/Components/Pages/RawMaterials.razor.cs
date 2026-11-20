using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class RawMaterials
{
    [Inject] private IRawMaterialService RawMaterialService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<RawMaterial> _items = new();
    private string _currencySymbol = "RD$";
    private string _search = string.Empty;
    private string _categoryFilter = string.Empty;
    private bool _onlyLowStock;
    private bool _showInactive;
    private RawMaterial _editing = new();
    private bool _showForm;
    private bool _showAdjust;
    private RawMaterial? _adjustTarget;
    private decimal _newStockValue;
    private string _adjustReason = "Conteo físico";

    private IEnumerable<RawMaterial> FilteredItems => _items.Where(item =>
        (_showInactive || item.IsActive) &&
        (!_onlyLowStock || item.IsLowStock) &&
        (string.IsNullOrWhiteSpace(_categoryFilter) || (Enum.TryParse<RawMaterialCategory>(_categoryFilter, out var cat) && item.Category == cat)) &&
        (string.IsNullOrWhiteSpace(_search) ||
         item.Sku.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ||
         item.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase)));

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
        await LoadAsync();
    }

    private async Task LoadAsync() => _items = await RawMaterialService.GetAllAsync(includeInactive: true);

    private void OpenNewModal()
    {
        _editing = new RawMaterial();
        _showForm = true;
    }

    private void OpenEditModal(RawMaterial item)
    {
        _editing = new RawMaterial
        {
            Id = item.Id, Sku = item.Sku, Name = item.Name, Description = item.Description,
            Category = item.Category, UnitOfMeasure = item.UnitOfMeasure,
            CurrentStock = item.CurrentStock, MinimumStockLevel = item.MinimumStockLevel,
            AverageCost = item.AverageCost, IsActive = item.IsActive
        };
        _showForm = true;
    }

    private void OpenAdjustModal(RawMaterial item)
    {
        _adjustTarget = item;
        _newStockValue = item.CurrentStock;
        _adjustReason = "Conteo físico";
        _showAdjust = true;
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_editing.Id == 0) await RawMaterialService.CreateAsync(_editing);
            else await RawMaterialService.UpdateAsync(_editing);
            _showForm = false;
            await LoadAsync();
            await Swal.FireAsync("Guardado", "Materia prima registrada correctamente.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task ConfirmAdjustAsync()
    {
        if (_adjustTarget == null) return;
        try
        {
            await RawMaterialService.AdjustStockAsync(_adjustTarget.Id, _newStockValue, _adjustReason);
            _showAdjust = false;
            await LoadAsync();
            await Swal.FireAsync("Stock actualizado", "Inventario ajustado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }
}
