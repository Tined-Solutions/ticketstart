using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Factory for admin user creation integration tests.
/// Sets required configuration via environment variables and loads the Development environment
/// so the real (migrated) database is used.
/// </summary>
public class AdminUserCreationApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalValues = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);
        builder.UseEnvironment("Development");

        // Provide non-placeholder config values so the host can start.
        SetConfigEnvVar("Resend__ApiKey", "test-resend-api-key");
        SetConfigEnvVar("Resend__FromEmail", "tickets@example.com");
        SetConfigEnvVar("CloudflareR2__AccessKey", "test-r2-access-key");
        SetConfigEnvVar("CloudflareR2__SecretKey", "test-r2-secret-key");
        SetConfigEnvVar("CloudflareR2__ServiceUrl", "https://test-account.r2.cloudflarestorage.com");
        SetConfigEnvVar("Jwt__SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789");

        // Background services try to connect to the real database; remove them
        // from the integration-test host to keep tests fast and isolated.
        builder.ConfigureServices(services =>
        {
            var hostedServices = services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList();
            foreach (var hosted in hostedServices)
            {
                services.Remove(hosted);
            }
        });
    }

    private void SetConfigEnvVar(string name, string value)
    {
        _originalValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var (name, original) in _originalValues)
        {
            Environment.SetEnvironmentVariable(name, original);
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Integration tests for the admin-only user creation endpoint (Batch 2, JD-C1).
/// </summary>
[Collection("EnvConfigTests")]
public class AdminUserCreationIntegrationTests : IClassFixture<AdminUserCreationApiFactory>
{
    private readonly AdminUserCreationApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUserCreationIntegrationTests(AdminUserCreationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Always include CSRF header for state-changing requests
        _client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");
    }

    private static bool HasLiveDatabase()
    {
        // The integration tests run from the backend directory during dotnet test,
        // but the ContentRoot used by the factory resolves relative to the assembly.
        // Use the same path resolution as AdminUserCreationApiFactory to get the backend root.
        var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var backendRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", ".."));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(backendRoot)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return !string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("YOUR_");
    }

    [Fact]
    public async Task PostAdminUsers_WithoutAuth_ReturnsUnauthorized()
    {
        if (!HasLiveDatabase()) return;
        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/users", new
        {
            name = "Juan Perez",
            email = "juan@example.com",
            password = "password123",
            role = "Organizador"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminUsers_WithNonAdminToken_ReturnsForbidden()
    {
        if (!HasLiveDatabase()) return;
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var organizer = await authService.CreateUserAsync(
            "Organizador User",
            $"org-{Guid.NewGuid()}@example.com",
            "password123",
            UserRole.Organizador);
        Assert.True(organizer.Success, $"Failed to seed organizer: {organizer.Error}");

        var token = await LoginAsync(organizer.Email, "password123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/users", new
        {
            name = "Juan Perez",
            email = $"juan-{Guid.NewGuid()}@example.com",
            password = "password123",
            role = "Organizador"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminUsers_WithAdminToken_ReturnsCreated()
    {
        if (!HasLiveDatabase()) return;
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var admin = await authService.CreateUserAsync(
            "Admin User",
            $"admin-{Guid.NewGuid()}@example.com",
            "password123",
            UserRole.Admin);
        Assert.True(admin.Success, $"Failed to seed admin: {admin.Error}");

        var token = await LoginAsync(admin.Email, "password123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/users", new
        {
            name = "Juan Perez",
            email = $"juan-{Guid.NewGuid()}@example.com",
            password = "password123",
            role = "Organizador"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TicketeraOnline.Api.Controllers.AdminUserResponse>();
        Assert.NotNull(body);
        Assert.Equal("Juan Perez", body.Name);
        Assert.Equal(UserRole.Organizador, body.Role);
    }

    [Fact]
    public async Task PostAdminUsers_WithInvalidEmail_ReturnsBadRequest()
    {
        if (!HasLiveDatabase()) return;
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var admin = await authService.CreateUserAsync(
            "Admin User",
            $"admin-{Guid.NewGuid()}@example.com",
            "password123",
            UserRole.Admin);
        Assert.True(admin.Success, $"Failed to seed admin: {admin.Error}");

        var token = await LoginAsync(admin.Email, "password123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/users", new
        {
            name = "Invalid Email",
            email = "not-an-email",
            password = "password123",
            role = "Organizador"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAdminUsers_WithShortPassword_ReturnsBadRequest()
    {
        if (!HasLiveDatabase()) return;
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var admin = await authService.CreateUserAsync(
            "Admin User",
            $"admin-{Guid.NewGuid()}@example.com",
            "password123",
            UserRole.Admin);
        Assert.True(admin.Success, $"Failed to seed admin: {admin.Error}");

        var token = await LoginAsync(admin.Email, "password123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/admin/users", new
        {
            name = "Short Password",
            email = $"short-{Guid.NewGuid()}@example.com",
            password = "1234567",
            role = "Organizador"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var loginClient = _factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        response.EnsureSuccessStatusCode();

        // Extract token from Set-Cookie header (token is now httpOnly cookie, not in body)
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith("token=", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the token value: "token=VALUE; path=/; ..."
                    var parts = cookie.Split(';');
                    var tokenPart = parts[0]; // "token=VALUE"
                    var tokenValue = tokenPart.Substring("token=".Length);
                    return tokenValue;
                }
            }
        }

        throw new InvalidOperationException("Login response did not contain a token cookie");
    }

    private record LoginResponse(Guid UserId, string Role, string Name);
}
