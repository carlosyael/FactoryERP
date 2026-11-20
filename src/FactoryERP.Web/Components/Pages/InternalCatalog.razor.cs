using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FactoryERP.Web.Components.Pages;

public partial class InternalCatalog
{
    [Inject] private ICatalogService CatalogService { get; set; } = default!;
    [Inject] private IProductService ProductService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private IFileUploadService FileUploadService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;

    private List<InternalCatalogItemDto> _items = new();
    private List<string> _lines = new();
    private string _currency = "RD$";
    private string _search = "";
    private string _selectedLine = "";
    private Product _editing = new();
    private bool _showModal;

    protected override async Task OnInitializedAsync()
    {
        _currency = (await BusinessProfileService.GetProfileAsync()).CurrencySymbol;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _items = await CatalogService.GetInternalCatalogAsync(_search, _selectedLine);
        _lines = await CatalogService.GetProductLinesAsync();
    }

    private void OpenNewModal()
    {
        _editing = new Product { Line = "Reparación", Presentation = "16 oz", ShowInPublicCatalog = true, IsTaxable = true };
        _showModal = true;
    }

    private async Task OpenEditModalAsync(int productId)
    {
        var existing = await ProductService.GetByIdAsync(productId);
        if (existing == null) return;
        _editing = new Product
        {
            Id = existing.Id, Sku = existing.Sku, Name = existing.Name, Line = existing.Line,
            Presentation = existing.Presentation, Description = existing.Description, Benefits = existing.Benefits,
            UsageInstructions = existing.UsageInstructions, KeyIngredients = existing.KeyIngredients,
            ImageUrl = existing.ImageUrl, RetailPrice = existing.RetailPrice, SalonPrice = existing.SalonPrice,
            WholesalePrice = existing.WholesalePrice, TaxRate = existing.TaxRate, IsTaxable = existing.IsTaxable,
            MinimumStockLevel = existing.MinimumStockLevel, ShowInPublicCatalog = existing.ShowInPublicCatalog, IsActive = existing.IsActive
        };
        _showModal = true;
    }

    private async Task OnImageUploadAsync(InputFileChangeEventArgs e)
    {
        try
        {
            _editing.ImageUrl = await FileUploadService.SaveImageAsync(e.File, "products");
            await Swal.FireAsync("Imagen cargada", "Guarda el producto para confirmar la nueva foto.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error al subir imagen", ex.Message, SweetAlertIcon.Error); }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (_editing.Id == 0) await ProductService.CreateAsync(_editing);
            else await ProductService.UpdateAsync(_editing);
            _showModal = false;
            await LoadAsync();
            await Swal.FireAsync("Guardado", "Catálogo actualizado.", SweetAlertIcon.Success);
        }
        catch (Exception ex) { await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error); }
    }
}
