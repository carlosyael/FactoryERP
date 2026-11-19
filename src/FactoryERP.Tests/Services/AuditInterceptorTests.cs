using FactoryERP.Core.Entities;
using FactoryERP.Core.Interfaces;
using FactoryERP.Infrastructure.Data;
using FactoryERP.Infrastructure.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FactoryERP.Tests.Services;

public class AuditInterceptorTests
{
    [Fact]
    public async Task SavingChanges_RecordsAuditTrailAndMasksSensitiveSettings()
    {
        var userMock = new Mock<ICurrentUserService>();
        userMock.Setup(u => u.UserId).Returns("auditor-1");

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditInterceptor(userMock.Object))
            .Options;

        await using var ctx = new AppDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var secretSetting = new SystemSetting { Key = "Api.SecretToken", Value = "SuperSecret123!" };
        var mat = new RawMaterial { Sku = "AUD-1", Name = "Aceite Auditoría" };
        ctx.SystemSettings.Add(secretSetting);
        ctx.RawMaterials.Add(mat);
        await ctx.SaveChangesAsync();

        mat.CreatedBy.Should().Be("auditor-1");
        mat.Name = "Aceite Actualizado";
        await ctx.SaveChangesAsync();
        mat.UpdatedBy.Should().Be("auditor-1");

        var logs = await ctx.AuditLogs.OrderBy(l => l.Id).ToListAsync();
        logs.Should().Contain(l => l.EntityName == nameof(RawMaterial) && l.Action == "Added");
        logs.Should().Contain(l => l.EntityName == nameof(RawMaterial) && l.Action == "Modified");
        var secretLog = logs.First(l => l.EntityName == nameof(SystemSetting));
        secretLog.NewValues.Should().Contain("********");
        secretLog.NewValues.Should().NotContain("SuperSecret123!");
    }
}
