using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TicketeraOnline.Api.Data;

/// <summary>
/// Design-time factory for ApplicationDbContext.
/// Used by EF Core tools (migrations) to create DbContext instances.
/// Ensures migrations use the direct connection (port 5432) instead of pooled connection.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // Get migration connection string (port 5432 for direct connection)
        var connectionString = configuration.GetConnectionString("MigrationConnection")
            ?? throw new InvalidOperationException("MigrationConnection string is not configured");

        // Create DbContext options
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
