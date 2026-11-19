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
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<ProductFormula> _formulas = new();
    private List<Product> _products = new();
    private List<RawMaterial> _materials = new();
    private readonly Dictionary<int, (decimal MaterialCost, decimal UnitCost)> _formulaSummary = new();
    private ProductFormula _editing = new();
    private List<EditableFormulaItem> _items = new();
    private bool _showModal;

    protected override async Task OnInitializedAsync()
    {
        _products = await ProductService.GetAllAsync();
        _materials = await RawMaterialService.GetAllAsync();
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
        _editing = new ProductFormula
        {
            ProductId = _products.FirstOrDefault()?.Id ?? 0,
            Version = "1.0",
            BaseYieldQuantity = 10,
            StandardLossMarginPercent = 5m
        };
        _items = _materials.Count > 0 ? [new() { RawMaterialId = _materials[0].Id, QuantityRequired = 100m, UnitOfMeasure = _materials[0].UnitOfMeasure }] : [];
        _showModal = true;
    }

    private void OpenEditModal(ProductFormula f)
    {
        _editing = new ProductFormula
        {
            Id = f.Id, ProductId = f.ProductId, Name = f.Name, Version = f.Version,
            BaseYieldQuantity = f.BaseYieldQuantity, StandardLossMarginPercent = f.StandardLossMarginPercent,
            OverheadCostPerBatch = f.OverheadCostPerBatch, Instructions = f.Instructions, IsActive = f.IsActive
        };
        _items = f.Items.Select(i => new EditableFormulaItem
        {
            RawMaterialId = i.RawMaterialId, QuantityRequired = i.QuantityRequired, UnitOfMeasure = i.UnitOfMeasure
        }).ToList();
        _showModal = true;
    }

    private void AddItem()
    {
        if (_materials.Count > 0)
            _items.Add(new() { RawMaterialId = _materials[0].Id, QuantityRequired = 10m, UnitOfMeasure = _materials[0].UnitOfMeasure });
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
