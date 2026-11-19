using FactoryERP.Core.Entities;
using FactoryERP.Core.Enums;
using FactoryERP.Infrastructure.Services;
using FactoryERP.Tests.Factories;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class ProductServiceTests
{
    [Fact]
    public async Task CreateAndUpdate_ValidatesDuplicateSkuNameAndPrices()
    {
        using var factory = DbContextSqliteFactory.Create();
        var service = new ProductService(factory);

        var p1 = await service.CreateAsync(new Product
        {
            Sku = "CAP-01",
            Name = "Shampoo Hidratante",
            RetailPrice = 800m,
            SalonPrice = 600m,
            WholesalePrice = 500m,
            Cost = 200m
        });

        p1.GetPriceForCustomerType(CustomerType.Retail).Should().Be(800m);
        p1.GetPriceForCustomerType(CustomerType.Salon).Should().Be(600m);
        p1.GetPriceForCustomerType(CustomerType.Wholesale).Should().Be(500m);
        p1.GetGrossMarginPercent().Should().Be(75m);

        var p2 = await service.CreateAsync(new Product { Sku = "CAP-02", Name = "Acondicionador", RetailPrice = 700m });
        p2.GetPriceForCustomerType(CustomerType.Salon).Should().Be(700m); // fallback a RetailPrice

        Func<Task> dupCreate = () => service.CreateAsync(new Product { Sku = "cap-01", Name = "Duplicado" });
        await dupCreate.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> negPrice = () => service.CreateAsync(new Product { Sku = "CAP-03", Name = "Negativo", RetailPrice = -10m });
        await negPrice.Should().ThrowAsync<ArgumentException>();

        Func<Task> emptyName = () => service.CreateAsync(new Product { Sku = "CAP-04", Name = "  " });
        await emptyName.Should().ThrowAsync<ArgumentException>();

        p2.Sku = "CAP-01";
        Func<Task> dupUpdate = () => service.UpdateAsync(p2);
        await dupUpdate.Should().ThrowAsync<InvalidOperationException>();
    }
}
