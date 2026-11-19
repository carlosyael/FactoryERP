using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Interfaces;
using FactoryERP.Core.Models;

namespace FactoryERP.Infrastructure.Services;

/// <summary>
/// Implementación desacoplada para futura integración de facturación electrónica DGII e-CF.
/// </summary>
public sealed class PendingElectronicInvoiceProvider : ITaxDocumentProvider
{
    public bool IsImplemented => false;
    public string StatusDescription => "Facturación electrónica (DGII e-CF) pendiente de implementación.";

    public Task<TaxDocumentResultDto> IssueForSaleAsync(Sale sale)
    {
        var result = new TaxDocumentResultDto(
            ElectronicInvoiceStatus.PendingImplementation,
            null,
            StatusDescription);
        return Task.FromResult(result);
    }
}
