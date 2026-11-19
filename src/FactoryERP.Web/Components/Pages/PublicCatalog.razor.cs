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
    private readonly Dictionary<int, (PublicCatalogItemDto Item, int Qty)> _cart = new();
    private PublicCatalogItemDto? _detailItem;
    private bool _showCart;
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

    private void AddToCart(PublicCatalogItemDto item)
    {
        int next = _cart.TryGetValue(item.ProductId, out var existing) ? existing.Qty + 1 : 1;
        _cart[item.ProductId] = (item, next);
    }

    private void ChangeQty(int productId, int delta)
    {
        if (!_cart.TryGetValue(productId, out var existing)) return;
        int next = existing.Qty + delta;
        if (next <= 0) _cart.Remove(productId);
        else _cart[productId] = (existing.Item, next);
    }

    private string BuildWhatsAppUrl()
    {
        string phone = new string((_profile?.WhatsAppNumber ?? "").Where(char.IsDigit).ToArray());
        var sb = new StringBuilder();
        sb.AppendLine($"Hola {_profile?.BusinessName}, me interesa realizar este pedido:");
        foreach (var (_, entry) in _cart)
        {
            sb.AppendLine($"- {entry.Qty}x {entry.Item.Name} ({entry.Item.Presentation})");
        }
        string encoded = Uri.EscapeDataString(sb.ToString());
        return string.IsNullOrWhiteSpace(phone) ? $"https://wa.me/?text={encoded}" : $"https://wa.me/{phone}?text={encoded}";
    }
}
