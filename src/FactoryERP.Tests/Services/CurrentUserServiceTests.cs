using System.Security.Claims;
using FactoryERP.Core.Constants;
using FactoryERP.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FactoryERP.Tests.Services;

public class CurrentUserServiceTests
{
    [Fact]
    public void CurrentUserService_UsesHttpContextFirstAndFallsBackToCircuitAccessor()
    {
        var accessor = new HttpContextAccessor();
        var service = new CurrentUserService(accessor);

        service.UserId.Should().BeNull();
        service.UserName.Should().BeNull();

        var circuitIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "circuit-user-1"), new Claim(ClaimTypes.Name, "circuit@erp.local")],
            "Cookies");
        service.SetUser(new ClaimsPrincipal(circuitIdentity));

        service.UserId.Should().Be("circuit-user-1");
        service.UserName.Should().Be("circuit@erp.local");

        var httpIdentity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "http-user-9"), new Claim(ClaimTypes.Name, "http@erp.local")],
            "Cookies");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(httpIdentity) };

        service.UserId.Should().Be("http-user-9");
        service.UserName.Should().Be("http@erp.local");
    }

    [Fact]
    public void PermissionsDefinitions_CoverAllPermissionCodesUniquelyWithMetadata()
    {
        var all = Permissions.GetAll();
        var defs = Permissions.GetDefinitions();

        defs.Should().HaveCount(all.Count);
        defs.Select(d => d.Code).Should().OnlyHaveUniqueItems();
        defs.Select(d => d.Code).Should().BeEquivalentTo(all);
        defs.Should().OnlyContain(d =>
            !string.IsNullOrWhiteSpace(d.Module) &&
            !string.IsNullOrWhiteSpace(d.DisplayName) &&
            !string.IsNullOrWhiteSpace(d.Description));
    }
}
