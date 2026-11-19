using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class SupplierServiceTests
{
    [Fact]
    public async Task CreateAndRegisterPayment_ValidatesBoundariesAndUpdatesBalance()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var accounting = new ManufacturingAccountingService(factory);
        var service = new SupplierService(factory, accounting);

        Func<Task> emptyName = () => service.CreateAsync(new Supplier { Name = " " });
        await emptyName.Should().ThrowAsync<ArgumentException>();

        var supplier = await service.CreateAsync(new Supplier { Name = "Envases RD", Rnc = "131000222", CurrentBalance = 1500m });

        Func<Task> zeroPay = () => service.RegisterPaymentAsync(supplier.Id, 0m, PaymentMethod.Cash, "P-0");
        await zeroPay.Should().ThrowAsync<ArgumentException>();

        Func<Task> creditPay = () => service.RegisterPaymentAsync(supplier.Id, 500m, PaymentMethod.Credit, "P-C");
        await creditPay.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> overPay = () => service.RegisterPaymentAsync(supplier.Id, 2000m, PaymentMethod.Cash, "P-O");
        await overPay.Should().ThrowAsync<InvalidOperationException>();

        await service.RegisterPaymentAsync(supplier.Id, 1500m, PaymentMethod.Cash, "P-FULL");
        var refreshed = await service.GetByIdAsync(supplier.Id);
        refreshed!.CurrentBalance.Should().Be(0m);

        var payments = await service.GetPaymentsAsync(supplier.Id);
        payments.Should().ContainSingle();

        var journal = new JournalEntryService(factory);
        var entries = await journal.GetAllAsync();
        entries.Should().ContainSingle();
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.AccountsPayable && l.Debit == 1500m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.Cash && l.Credit == 1500m);
    }
}
