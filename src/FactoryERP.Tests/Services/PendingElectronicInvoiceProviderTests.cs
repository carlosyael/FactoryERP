using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class PendingElectronicInvoiceProviderTests
{
    [Fact]
    public async Task IssueForSaleAsync_ReturnsPendingImplementationStatus()
    {
        var provider = new PendingElectronicInvoiceProvider();
        provider.IsImplemented.Should().BeFalse();
        provider.StatusDescription.Should().Contain("pendiente");

        var result = await provider.IssueForSaleAsync(new Sale { SaleNumber = "VTA-01", Total = 1000m });
        result.Status.Should().Be(ElectronicInvoiceStatus.PendingImplementation);
        result.DocumentNumber.Should().BeNull();
    }
}
