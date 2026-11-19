using System.Text;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FactoryERP.Web.Components.Pages;

public partial class PublicCatalog
{
    [Inject] private ICatalogService CatalogService { get; set; } = default!;
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;

    private BusinessProfileDto? _profile;
    private List<PublicCatalogItemDto> _items = new();
    private List<string> _lines = new();
    private readonly Dictionary<int, int> _cart = new();
    private PublicCatalogItemDto? _detailItem;
    private string _search = "";
    private string _selectedLine = "";
    private bool _onlyInStock;

    protected override async Task OnInitializedAsync()
    {
        _profile = await BusinessProfileService.GetProfileAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _items = await CatalogService.GetPublicCatalogAsync(_search, _selectedLine, _onlyInStock);
        _lines = await CatalogService.GetProductLinesAsync();
    }

    private void AddToCart(int productId)
    {
        _cart[productId] = _cart.TryGetValue(productId, out int qty) ? qty + 1 : 1;
    }

    private string BuildWhatsAppUrl()
    {
        string phone = new string((_profile?.WhatsAppNumber ?? "").Where(char.IsDigit).ToArray());
        var sb = new StringBuilder();
        sb.AppendLine($"Hola {_profile?.BusinessName}, me interesa realizar este pedido:");
        foreach (var (productId, qty) in _cart)
        {
            var item = _items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null) sb.AppendLine($"- {qty}x {item.Name} ({item.Presentation})");
        }
        string encoded = Uri.EscapeDataString(sb.ToString());
        return string.IsNullOrWhiteSpace(phone) ? $"https://wa.me/?text={encoded}" : $"https://wa.me/{phone}?text={encoded}";
    }
}
