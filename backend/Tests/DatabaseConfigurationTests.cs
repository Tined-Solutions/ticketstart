using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketeraOnline.Api.Data;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Tests to verify database connection configuration
/// Validates Requirements 15.1, 15.5
/// </summary>
public class DatabaseConfigurationTests
{
    [Fact]
    public void DbContext_ShouldBeConfiguredWithPostgreSQL()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var dbContext = serviceProvider.GetService<ApplicationDbContext>();

        // Assert
        Assert.NotNull(dbContext);
        Assert.IsType<ApplicationDbContext>(dbContext);
    }

    [Fact]
    public void ConnectionString_ShouldHavePoolingEnabled()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Act
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Pooling=true", connectionString);
        Assert.Contains("Port=6543", connectionString); // Transaction mode pooler
    }

    [Fact]
    public void MigrationConnectionString_ShouldUseDirectConnection()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Act
        var migrationConnectionString = configuration.GetConnectionString("MigrationConnection");

        // Assert
        Assert.NotNull(migrationConnectionString);
        Assert.Contains("Port=5432", migrationConnectionString); // Direct connection
    }

    [Fact]
    public void ConnectionString_ShouldHaveProperPoolingConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Act
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Assert
        Assert.NotNull(connectionString);
        Assert.Contains("Minimum Pool Size=0", connectionString);
        Assert.Contains("Maximum Pool Size=100", connectionString);
        Assert.Contains("Connection Lifetime=0", connectionString);
        Assert.Contains("Connection Idle Lifetime=300", connectionString);
        Assert.Contains("Timeout=30", connectionString);
    }
}
