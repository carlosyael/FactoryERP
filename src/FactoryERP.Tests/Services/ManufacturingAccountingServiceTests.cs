using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class ManufacturingAccountingServiceTests
{
    [Fact]
    public async Task ManufacturingAndCommercialLifecycle_AllJournalEntriesStayBalanced()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var accounting = new ManufacturingAccountingService(factory);
        var reports = new FinancialReportService(factory);

        var purchaseEntry = await accounting.PostPurchaseReceiptAsync(new PurchaseOrder
        {
            OrderNumber = "OC-0001",
            PurchaseDate = DateTime.UtcNow,
            PaymentMethod = PaymentMethod.Credit,
            SubTotal = 10000m,
            TaxAmount = 1800m,
            Total = 11800m
        });

        var startEntry = await accounting.PostProductionStartAsync(
            new ProductionOrder { OrderNumber = "OP-0001", BatchCode = "LOT-01", StartedAt = DateTime.UtcNow },
            materialCost: 4000m);

        // Overhead real modificado al cierre (700 vs estimado): WIP debe quedar en 0.00
        var completeEntry = await accounting.PostProductionCompletionAsync(new ProductionOrder
        {
            OrderNumber = "OP-0001",
            BatchCode = "LOT-01",
            CompletedAt = DateTime.UtcNow,
            ActualMaterialCost = 4000m,
            ActualOverheadCost = 700m,
            CapitalizedInventoryCost = 4400m,
            AbnormalLossCost = 300m
        });

        var saleEntry = await accounting.PostSaleAsync(new Sale
        {
            SaleNumber = "V-0001",
            SaleDate = DateTime.UtcNow,
            PaymentMethod = PaymentMethod.Cash,
            SubTotal = 6000m,
            DiscountAmount = 0m,
            TaxAmount = 1080m,
            Total = 7080m,
            TotalCogs = 3000m
        });

        foreach (var entry in new[] { purchaseEntry, startEntry, completeEntry, saleEntry })
        {
            entry.Status.Should().Be(JournalEntryStatus.Posted);
            entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
            entry.Lines.Sum(l => l.Debit).Should().BeGreaterThan(0m);
        }

        completeEntry.Lines.Should().Contain(l => l.Account!.Code == AccountCodes.FinishedGoodsInventory && l.Debit == 4400m);
        completeEntry.Lines.Should().Contain(l => l.Account!.Code == AccountCodes.AbnormalProductionLoss && l.Debit == 300m);
        completeEntry.Lines.Should().Contain(l => l.Account!.Code == AccountCodes.WorkInProgressInventory && l.Credit == 4000m);
        completeEntry.Lines.Should().Contain(l => l.Account!.Code == AccountCodes.ManufacturingOverheadApplied && l.Credit == 700m);

        var trial = await reports.GetTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        trial.IsBalanced.Should().BeTrue();
        var wipRow = trial.Rows.First(r => r.Code == AccountCodes.WorkInProgressInventory);
        (wipRow.TotalDebit - wipRow.TotalCredit).Should().Be(0m);
    }
}
