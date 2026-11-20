using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Domain;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class Formulas
{
    [Inject] private IFormulaService FormulaService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IRawMaterialService RawMaterialService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<ProductFormula> _formulas = new();
    private List<Product> _products = new();
    private List<RawMaterial> _materials = new();
    private string _currencySymbol = "RD$";
    private decimal _defaultLossMargin = 5m;
    private string _search = string.Empty;
    private int _productFilter;
    private bool _showInactive;
    private readonly Dictionary<int, (decimal MaterialCost, decimal UnitCost)> _formulaSummary = new();
    private ProductFormula _editing = new();
    private List<EditableFormulaItem> _items = new();
    private bool _isSemiFinished;
    private int _targetProductId, _targetBulkMaterialId;
    private bool _showModal;

    private IEnumerable<ProductFormula> FilteredFormulas => _formulas.Where(f =>
        (_showInactive || f.IsActive) &&
        (_productFilter == 0 || (_productFilter == -1 ? f.IsSemiFinishedOutput : f.ProductId == _productFilter)) &&
        (string.IsNullOrWhiteSpace(_search) ||
         f.Name.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase) ||
         f.OutputDisplayName.Contains(_search.Trim(), StringComparison.OrdinalIgnoreCase)));

    protected override async Task OnInitializedAsync()
    {
        var profile = await BusinessProfileService.GetProfileAsync();
        _currencySymbol = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "RD$" : profile.CurrencySymbol;
        _defaultLossMargin = profile.DefaultLossMarginPercent;
        _products = await ProductService.GetAllAsync();
        _materials = await RawMaterialService.GetAllAsync(includeInactive: true);
        await LoadFormulasAsync();
    }

    private async Task LoadFormulasAsync()
    {
        _formulas = await FormulaService.GetAllAsync(true);
        _formulaSummary.Clear();
        foreach (var f in _formulas)
        {
            decimal matCost = await FormulaService.CalculateBaseMaterialCostAsync(f.Id);
            int expectedYield = ProductionCostCalculator.CalculateExpectedResultingQuantity(f.BaseYieldQuantity, f.StandardLossMarginPercent);
            decimal unitCost = expectedYield > 0
                ? Math.Round((matCost + f.OverheadCostPerBatch) / expectedYield, 2, MidpointRounding.AwayFromZero)
                : 0m;
            _formulaSummary[f.Id] = (matCost, unitCost);
        }
    }

    private void OpenNewModal()
    {
        var defaultBulk = _materials.FirstOrDefault(m => m.IsActive && m.Category == RawMaterialCategory.SemiFinished)
            ?? _materials.FirstOrDefault(m => m.IsActive)
            ?? _materials.FirstOrDefault();
        var defaultMat = _materials.FirstOrDefault(m => m.IsActive && m.Id != defaultBulk?.Id)
            ?? _materials.FirstOrDefault(m => m.IsActive)
            ?? _materials.FirstOrDefault();
        _isSemiFinished = false;
        _targetProductId = _products.FirstOrDefault()?.Id ?? 0;
        _targetBulkMaterialId = defaultBulk?.Id ?? 0;
        _editing = new ProductFormula
        {
            ProductId = _targetProductId > 0 ? _targetProductId : null,
            Version = "1.0",
            BaseYieldQuantity = 10,
            StandardLossMarginPercent = _defaultLossMargin
        };
        _items = defaultMat != null ? [new() { RawMaterialId = defaultMat.Id, QuantityRequired = 100m, UnitOfMeasure = defaultMat.UnitOfMeasure }] : [];
        _showModal = true;
    }

    private void OpenEditModal(ProductFormula f)
    {
        _isSemiFinished = f.IsSemiFinishedOutput;
        _targetProductId = f.ProductId ?? (_products.FirstOrDefault()?.Id ?? 0);
        _targetBulkMaterialId = f.OutputRawMaterialId ?? (_materials.FirstOrDefault(m => m.IsActive)?.Id ?? 0);
        _editing = new ProductFormula
        {
            Id = f.Id, ProductId = f.ProductId, OutputRawMaterialId = f.OutputRawMaterialId,
            Name = f.Name, Version = f.Version, BaseYieldQuantity = f.BaseYieldQuantity,
            StandardLossMarginPercent = f.StandardLossMarginPercent,
            OverheadCostPerBatch = f.OverheadCostPerBatch, Instructions = f.Instructions, IsActive = f.IsActive
        };
        _items = f.Items.Select(i => new EditableFormulaItem
        {
            RawMaterialId = i.RawMaterialId, QuantityRequired = i.QuantityRequired, UnitOfMeasure = i.UnitOfMeasure
        }).ToList();
        _showModal = true;
    }

    private async Task CloneVersionAsync(int formulaId)
    {
        try
        {
            var cloned = await FormulaService.CloneAsNewVersionAsync(formulaId);
            await LoadFormulasAsync();
            await Swal.FireAsync("Nueva versión creada", $"Versión v{cloned.Version} creada y lista para editar.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private void AddItem()
    {
        var defaultMat = _materials.FirstOrDefault(m => m.IsActive) ?? _materials.FirstOrDefault();
        if (defaultMat != null)
            _items.Add(new() { RawMaterialId = defaultMat.Id, QuantityRequired = 10m, UnitOfMeasure = defaultMat.UnitOfMeasure });
    }

    private void OnMaterialSelected(EditableFormulaItem item, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int matId))
        {
            item.RawMaterialId = matId;
            var mat = _materials.FirstOrDefault(m => m.Id == matId);
            if (mat != null) item.UnitOfMeasure = mat.UnitOfMeasure;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            _editing.ProductId = _isSemiFinished ? null : _targetProductId;
            _editing.OutputRawMaterialId = _isSemiFinished ? _targetBulkMaterialId : null;
            _editing.Items = _items.Select(i => new FormulaItem
            {
                RawMaterialId = i.RawMaterialId, QuantityRequired = i.QuantityRequired, UnitOfMeasure = i.UnitOfMeasure
            }).ToList();

            if (_editing.Id == 0) await FormulaService.CreateAsync(_editing);
            else await FormulaService.UpdateAsync(_editing);

            _showModal = false;
            await LoadFormulasAsync();
            await Swal.FireAsync("Fórmula guardada", "Receta de producción actualizada.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }

    private sealed class EditableFormulaItem
    {
        public int RawMaterialId { get; set; }
        public decimal QuantityRequired { get; set; }
        public UnitOfMeasure UnitOfMeasure { get; set; }
    }
}
