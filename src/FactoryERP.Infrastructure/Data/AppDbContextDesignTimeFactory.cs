using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FactoryERP.Infrastructure.Data;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        string? envConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        string connectionString = string.IsNullOrWhiteSpace(envConnection)
            ? "Server=localhost,1433;Database=FactoryERP_Design;User Id=sa;Password=DesignTimeOnly123!;TrustServerCertificate=True;"
            : envConnection;

        optionsBuilder.UseSqlServer(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
