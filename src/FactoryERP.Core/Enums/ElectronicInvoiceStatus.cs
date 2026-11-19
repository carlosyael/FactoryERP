namespace FactoryERP.Core.Enums;

/// <summary>
/// Estado de comprobante fiscal electrónico preparado para futura integración DGII.
/// </summary>
public enum ElectronicInvoiceStatus
{
    NotApplicable = 0,
    PendingImplementation = 1,
    Pending = 2,
    Accepted = 3,
    Rejected = 4
}
