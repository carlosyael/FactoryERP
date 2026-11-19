using FactoryERP.Core.Entities;
using FactoryERP.Core.Models;

namespace FactoryERP.Core.Interfaces;

/// <summary>
/// Contrato extensible para emisión de comprobantes fiscales electrónicos (DGII e-CF).
/// </summary>
public interface ITaxDocumentProvider
{
    bool IsImplemented { get; }
    string StatusDescription { get; }
    Task<TaxDocumentResultDto> IssueForSaleAsync(Sale sale);
}
