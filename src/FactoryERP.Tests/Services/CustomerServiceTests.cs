using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class CustomerServiceTests
{
    [Fact]
    public async Task CreateAndRegisterPayment_UpdatesBalanceAndPostsJournalEntry()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();

        var accounting = new ManufacturingAccountingService(factory);
        var customerService = new CustomerService(factory, accounting);

        var salon = await customerService.CreateAsync(new Customer
        {
            Name = "Salón Belleza Radiante",
            RncCedula = "131999888",
            CustomerType = CustomerType.Salon,
            CreditLimit = 10000m,
            CurrentBalance = 2500m
        });

        Func<Task> overPay = () => customerService.RegisterPaymentAsync(salon.Id, 3000m, PaymentMethod.Cash, "REC-1");
        await overPay.Should().ThrowAsync<InvalidOperationException>();

        var payment = await customerService.RegisterPaymentAsync(salon.Id, 1500m, PaymentMethod.BankTransfer, "TRF-99");
        payment.Amount.Should().Be(1500m);

        var updated = await customerService.GetByIdAsync(salon.Id);
        updated!.CurrentBalance.Should().Be(1000m);

        var journal = new JournalEntryService(factory);
        var entries = await journal.GetAllAsync();
        entries.Should().ContainSingle();
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.Bank && l.Debit == 1500m);
        entries[0].Lines.Should().Contain(l => l.Account!.Code == AccountCodes.AccountsReceivable && l.Credit == 1500m);
    }

    [Fact]
    public async Task CreateAndRegisterPayment_ValidatesEmptyNameAndInvalidPaymentBoundaries()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var accounting = new ManufacturingAccountingService(factory);
        var service = new CustomerService(factory, accounting);

        Func<Task> emptyName = () => service.CreateAsync(new Customer { Name = "  ", RncCedula = "000" });
        await emptyName.Should().ThrowAsync<ArgumentException>();

        var c = await service.CreateAsync(new Customer { Name = "Cliente BVA", RncCedula = "101000", CurrentBalance = 500m });
        Func<Task> zeroPay = () => service.RegisterPaymentAsync(c.Id, 0m, PaymentMethod.Cash, "C-0");
        await zeroPay.Should().ThrowAsync<ArgumentException>();

        Func<Task> creditPay = () => service.RegisterPaymentAsync(c.Id, 100m, PaymentMethod.Credit, "C-C");
        await creditPay.Should().ThrowAsync<InvalidOperationException>();

        await service.RegisterPaymentAsync(c.Id, 500m, PaymentMethod.Cash, "C-FULL");
        (await service.GetByIdAsync(c.Id))!.CurrentBalance.Should().Be(0m);
        (await service.GetPaymentsAsync(c.Id)).Should().ContainSingle();
    }
}
