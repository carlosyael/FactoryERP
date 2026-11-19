using CurrieTechnologies.Razor.SweetAlert2;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;
using FactoryERP.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace FactoryERP.Web.Components.Pages;

public partial class Settings
{
    [Inject] private IBusinessProfileService BusinessProfileService { get; set; } = default!;
    [Inject] private ITaxDocumentProvider TaxDocumentProvider { get; set; } = default!;
    [Inject] private IFileUploadService FileUploadService { get; set; } = default!;
    [Inject] private ILoadingService LoadingService { get; set; } = default!;
    [Inject] private SweetAlertService Swal { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private EditableProfileModel? _model;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        var dto = await BusinessProfileService.GetProfileAsync();
        _model = EditableProfileModel.FromDto(dto);
    }

    private async Task OnLogoUploadAsync(InputFileChangeEventArgs e)
    {
        if (_model == null) return;
        try
        {
            LoadingService.Show("Subiendo logotipo...");
            _model.LogoUrl = await FileUploadService.SaveImageAsync(e.File, "branding");
            await Swal.FireAsync("Logo cargado", "Guarda los cambios para aplicar el nuevo logo.", SweetAlertIcon.Success);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error);
        }
        finally
        {
            LoadingService.Hide();
        }
    }

    private async Task SaveAsync()
    {
        if (_model == null) return;
        _saving = true;
        LoadingService.Show("Guardando configuración...");
        try
        {
            await BusinessProfileService.UpdateProfileAsync(_model.ToDto());
            await Swal.FireAsync("Configuración actualizada", "Los datos del comercio han sido guardados.", SweetAlertIcon.Success);
        }
        catch (Exception ex)
        {
            await Swal.FireAsync("Error", ex.Message, SweetAlertIcon.Error);
        }
        finally
        {
            _saving = false;
            LoadingService.Hide();
        }
    }

    private async Task CopyPublicUrlAsync()
    {
        string url = $"{Navigation.BaseUri.TrimEnd('/')}/catalogo";
        await JSRuntime.InvokeVoidAsync("copyTextToClipboard", url);
        await Swal.FireAsync("Enlace copiado", url, SweetAlertIcon.Info);
    }

    private sealed class EditableProfileModel
    {
        public string BusinessName { get; set; } = "";
        public string Tagline { get; set; } = "";
        public string Rnc { get; set; } = "";
        public string Phone { get; set; } = "";
        public string WhatsAppNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string PrimaryColor { get; set; } = "#0f766e";
        public string CurrencySymbol { get; set; } = "RD$";
        public decimal DefaultTaxRate { get; set; } = 0.18m;
        public bool ElectronicInvoicingEnabled { get; set; }
        public decimal MaxDiscountPercentage { get; set; } = 30m;
        public decimal DefaultLossMarginPercent { get; set; } = 5m;
        public decimal AbnormalLossTolerancePercent { get; set; } = 2m;
        public bool PublicCatalogEnabled { get; set; } = true;
        public string CatalogWelcomeMessage { get; set; } = "";

        public static EditableProfileModel FromDto(BusinessProfileDto d) => new()
        {
            BusinessName = d.BusinessName, Tagline = d.Tagline, Rnc = d.Rnc, Phone = d.Phone,
            WhatsAppNumber = d.WhatsAppNumber, Email = d.Email, Address = d.Address,
            LogoUrl = d.LogoUrl, PrimaryColor = d.PrimaryColor, CurrencySymbol = d.CurrencySymbol,
            DefaultTaxRate = d.DefaultTaxRate, ElectronicInvoicingEnabled = d.ElectronicInvoicingEnabled,
            MaxDiscountPercentage = d.MaxDiscountPercentage, DefaultLossMarginPercent = d.DefaultLossMarginPercent,
            AbnormalLossTolerancePercent = d.AbnormalLossTolerancePercent, PublicCatalogEnabled = d.PublicCatalogEnabled,
            CatalogWelcomeMessage = d.CatalogWelcomeMessage
        };

        public BusinessProfileDto ToDto() => new(
            BusinessName, Tagline, Rnc, Phone, WhatsAppNumber, Email, Address, LogoUrl, PrimaryColor,
            CurrencySymbol, DefaultTaxRate, ElectronicInvoicingEnabled, MaxDiscountPercentage,
            DefaultLossMarginPercent, AbnormalLossTolerancePercent, PublicCatalogEnabled, CatalogWelcomeMessage);
    }
}
