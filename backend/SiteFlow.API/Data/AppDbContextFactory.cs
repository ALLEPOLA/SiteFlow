using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SiteFlow.API.Data;

// Keep migration tooling independent of web startup, seeding, and OS event logging.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(AppDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables().Build();
        var connection = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=SiteFlow;User=design;";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 36))).Options);
    }
}
