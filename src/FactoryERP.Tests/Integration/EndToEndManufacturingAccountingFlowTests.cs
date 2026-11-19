using FactoryERP.Core.Constants;
using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Core.Models;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace FactoryERP.Tests.Integration;

public class EndToEndManufacturingAccountingFlowTests
{
    [Fact]
    public async Task FullLifecycle_CapitalPurchaseProductionWithAbnormalLossSaleCollectionExpense_BalancesAllStatements()
    {
        using var factory = DbContextSqliteFactory.Create();
        var chart = new ChartOfAccountsService(factory);
        await chart.SeedStandardChartAsync();
        var accounts = (await chart.GetAllAsync()).ToDictionary(a => a.Code, a => a.Id);

        var settings = new SystemSettingService(factory, new MemoryCache(new MemoryCacheOptions()));
        var accounting = new ManufacturingAccountingService(factory);
        var journal = new JournalEntryService(factory);
        var reports = new FinancialReportService(factory);
        var supplierSvc = new SupplierService(factory, accounting);
        var materialSvc = new RawMaterialService(factory, accounting);
        var purchaseSvc = new PurchaseOrderService(factory, settings, accounting);
        var productSvc = new ProductService(factory);
        var formulaSvc = new FormulaService(factory);
        var prodSvc = new ProductionOrderService(factory, settings, accounting);
        var customerSvc = new CustomerService(factory, accounting);
        var saleSvc = new SaleService(factory, settings, accounting, new PendingElectronicInvoiceProvider());

        var now = DateTime.UtcNow;

        // 1. Aporte de capital inicial: RD$ 50,000 en Banco
        await journal.CreateAsync(new CreateJournalEntryRequest(now, "Capital inicial", "CAP-01", true,
            [new(accounts[AccountCodes.Bank], 50000m, 0m), new(accounts[AccountCodes.ShareCapital], 0m, 50000m)]));

        // 2. Compra a crédito de materia prima + envases con ITBIS 18% y abono parcial
        var supplier = await supplierSvc.CreateAsync(new Supplier { Name = "Química Industrial" });
        var baseMat = await materialSvc.CreateAsync(new RawMaterial { Sku = "E2E-BASE", Name = "Base", UnitOfMeasure = UnitOfMeasure.Kilogram });
        var bottleMat = await materialSvc.CreateAsync(new RawMaterial { Sku = "E2E-BOT", Name = "Envase", UnitOfMeasure = UnitOfMeasure.Unit });

        await purchaseSvc.ReceivePurchaseAsync(new ReceivePurchaseRequest(
            supplier.Id, PaymentMethod.Credit, now, "Compra inicial",
            [new(baseMat.Id, 10m, 500m, true), new(bottleMat.Id, 20m, 50m, true)])); // SubTotal 6,000 + ITBIS 1,080 = 7,080
        await supplierSvc.RegisterPaymentAsync(supplier.Id, 2080m, PaymentMethod.BankTransfer, "PAG-1"); // CxP queda en 5,000

        // 3. Producto + Fórmula + Producción de 20 uds (usa 10 kg = 5,000 + 20 env = 1,000 => Mat 6,000; CIF real 500)
        var product = await productSvc.CreateAsync(new Product
        {
            Sku = "E2E-CAP", Name = "Tratamiento Intensivo", RetailPrice = 1200m, SalonPrice = 900m, WholesalePrice = 750m, IsTaxable = true, TaxRate = 0.18m
        });
        var formula = await formulaSvc.CreateAsync(new ProductFormula
        {
            ProductId = product.Id, Name = "Fórmula E2E", Version = "1.0", BaseYieldQuantity = 10, StandardLossMarginPercent = 5m, OverheadCostPerBatch = 300m,
            Items = [new() { RawMaterialId = baseMat.Id, QuantityRequired = 5000m, UnitOfMeasure = UnitOfMeasure.Gram }, new() { RawMaterialId = bottleMat.Id, QuantityRequired = 10m, UnitOfMeasure = UnitOfMeasure.Unit }]
        });

        var op = await prodSvc.CreateDraftAsync(new CreateProductionOrderRequest(formula.Id, 20, 5m, now, now.AddMonths(12), "LOT-E2E-1"));
        await prodSvc.StartProductionAsync(op.Id);
        // Cierre con 16 uds (20% merma vs 7% permitido => 13% merma anormal sobre 6,500 = 845; Inv Term = 5,655; UnitCost = 353.4375)
        var completed = await prodSvc.CompleteProductionAsync(new CompleteProductionRequest(op.Id, 16, 500m, now.AddMonths(12)));
        completed.AbnormalLossCost.Should().Be(845m);
        completed.CapitalizedInventoryCost.Should().Be(5655m);

        // 4. Venta a Salón a crédito de 10 uds (10 * 900 = 9,000 + ITBIS 1,620 = 10,620) y cobro parcial de 4,620
        var salon = await customerSvc.CreateAsync(new Customer { Name = "Salón Élite", RncCedula = "101999111", CustomerType = CustomerType.Salon, CreditLimit = 20000m });
        var sale = await saleSvc.CreateSaleAsync(new CreateSaleRequest(salon.Id, PaymentMethod.Credit, now, "Pedido 1", [new(product.Id, 10)]));
        await customerSvc.RegisterPaymentAsync(salon.Id, 4620m, PaymentMethod.Cash, "COB-1"); // CxC queda en 6,000

        // 5. Gasto operativo de 400 en efectivo
        await journal.RecordExpenseAsync(AccountCodes.DistributionExpenses, AccountCodes.Cash, 400m, "Mensajería", "EXP-1", now);

        // 6. Verificación integral de estados financieros y saldos
        var trial = await reports.GetTrialBalanceAsync(now.AddDays(-1), now.AddDays(1));
        var income = await reports.GetIncomeStatementAsync(now.AddDays(-1), now.AddDays(1));
        var balance = await reports.GetBalanceSheetAsync(now.AddDays(1));

        trial.IsBalanced.Should().BeTrue();
        var wipRow = trial.Rows.First(r => r.Code == AccountCodes.WorkInProgressInventory);
        (wipRow.TotalDebit - wipRow.TotalCredit).Should().Be(0m);

        income.Revenue.Should().Be(9000m);
        income.CostOfGoodsSold.Should().Be(sale.TotalCogs);
        income.AbnormalLossCost.Should().Be(845m);

        balance.IsBalanced.Should().BeTrue();
        (balance.TotalLiabilities + balance.TotalEquity).Should().Be(balance.TotalAssets);
        (await customerSvc.GetByIdAsync(salon.Id))!.CurrentBalance.Should().Be(6000m);
        (await supplierSvc.GetByIdAsync(supplier.Id))!.CurrentBalance.Should().Be(5000m);
    }
}
