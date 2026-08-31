using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Factory for the admin user-management integration tests (AUM-001…AUM-004).
/// Sets required configuration via environment variables and loads the
/// Development environment so the real (migrated) database is used.
/// Same pattern as <see cref="AdminUserCreationApiFactory"/>.
/// </summary>
public class AdminUserManagementApiFactory : WebApplicationFactory<Program>
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
/// Integration tests for the admin user-management endpoints:
/// PUT /api/admin/users/{userId}/role (AUM-001) and
/// POST /api/admin/users/{userId}/reset-password (AUM-003), plus the
/// SinAcceso / next-login session semantics (AUM-002, AUM-004).
/// Mutates process-global env vars → serialized via the EnvConfigTests collection.
/// </summary>
[Collection("EnvConfigTests")]
public class AdminUserManagementIntegrationTests : IClassFixture<AdminUserManagementApiFactory>
{
    private readonly AdminUserManagementApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUserManagementIntegrationTests(AdminUserManagementApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Always include CSRF header for state-changing requests
        _client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");
    }

    private static bool HasLiveDatabase()
    {
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

    #region AUM-001 — PUT /api/admin/users/{userId}/role

    [Fact]
    public async Task PutAdminUsersRole_WithAdminCookie_ReturnsOkAndPersistsRole()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();
        var target = await SeedUserAsync(UserRole.Staff);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{target.UserId}/role")
        {
            Content = JsonContent.Create(new { role = "Organizador" })
        };
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserSummary>();
        Assert.NotNull(body);
        Assert.Equal(UserRole.Organizador, body.Role);

        // Persisted role is the source of truth for the NEXT login (AUM-004).
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await context.Users.AsNoTracking().SingleAsync(u => u.Id == target.UserId);
        Assert.Equal(UserRole.Organizador, persisted.Role);
    }

    [Fact]
    public async Task PutAdminUsersRole_SelfEdit_ReturnsBadRequest()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, adminId, _) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{adminId}/role")
        {
            Content = JsonContent.Create(new { role = "Staff" })
        };
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutAdminUsersRole_UnknownUser_ReturnsNotFound()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{Guid.NewGuid()}/role")
        {
            Content = JsonContent.Create(new { role = "Staff" })
        };
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutAdminUsersRole_WithoutCsrfHeader_ReturnsBadRequest()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();
        var target = await SeedUserAsync(UserRole.Staff);

        // A client WITHOUT the X-CSRF-PROTECT default header (contrary to the ctor).
        var bareClient = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{target.UserId}/role")
        {
            Content = JsonContent.Create(new { role = "Staff" })
        };
        request.Headers.Add("Cookie", adminCookie);
        var response = await bareClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CSRF header required", body);
    }

    #endregion

    #region Helpers

    private async Task<(string Cookie, Guid AdminId, string Email)> SeedAdminAndLoginAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var admin = await authService.CreateUserAsync(
            "Admin User",
            $"admin-{Guid.NewGuid()}@example.com",
            "password123",
            UserRole.Admin);
        Assert.True(admin.Success, $"Failed to seed admin: {admin.Error}");

        var cookie = await LoginAsync(admin.Email, "password123");
        return (cookie, admin.UserId, admin.Email);
    }

    private async Task<CreateUserResult> SeedUserAsync(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var user = await authService.CreateUserAsync(
            "Target User",
            $"target-{Guid.NewGuid()}@example.com",
            "password123",
            role);
        Assert.True(user.Success, $"Failed to seed user: {user.Error}");
        return user;
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

        // Extract the token from Set-Cookie (token is an httpOnly cookie, not in body)
        // and return it as a "token=..." Cookie header value.
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith("token=", StringComparison.OrdinalIgnoreCase))
                {
                    return cookie.Split(';')[0];
                }
            }
        }

        throw new InvalidOperationException("Login response did not contain a token cookie");
    }

    #endregion
}
