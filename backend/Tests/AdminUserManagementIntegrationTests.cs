using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketeraOnline.Api.Controllers;
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
    private readonly SemaphoreSlim _adminLock = new(1, 1);
    private (string Cookie, Guid AdminId, string Email)? _adminCredential;

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

    /// <summary>
    /// Seeds the shared admin user ONCE per factory (lazily) and returns its
    /// login cookie. Class tests share this credential so the whole class stays
    /// under the Login rate limiter's 10-requests-per-minute sliding window
    /// (11+ logins through one host would otherwise trip 429s mid-suite).
    /// </summary>
    public async Task<(string Cookie, Guid AdminId, string Email)> EnsureAdminAsync(
        Func<string, string, Task<string>> loginAsync)
    {
        await _adminLock.WaitAsync();
        try
        {
            if (_adminCredential == null)
            {
                using var scope = Services.CreateScope();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var admin = await authService.CreateUserAsync(
                    "Admin User",
                    $"admin-{Guid.NewGuid()}@example.com",
                    "password123",
                    UserRole.Admin);
                if (!admin.Success)
                {
                    throw new InvalidOperationException($"Failed to seed admin: {admin.Error}");
                }

                var cookie = await loginAsync(admin.Email, "password123");
                _adminCredential = (cookie, admin.UserId, admin.Email);
            }

            return _adminCredential.Value;
        }
        finally
        {
            _adminLock.Release();
        }
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

    #region AUM-003 — POST /api/admin/users/{userId}/reset-password

    [Fact]
    public async Task PostAdminUsersResetPassword_ReturnsUsableTempPassword_OldPasswordStopsWorking()
    {
        if (!HasLiveDatabase()) return;

        var target = await SeedUserAsync(UserRole.Staff);
        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{target.UserId}/reset-password");
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // S2 (verify remediation): the credential's only appearance must be
        // un-cacheable — pin the D11 `Cache-Control: no-store` response header.
        Assert.True(response.Headers.CacheControl.NoStore, "Reset 200 must set Cache-Control: no-store");
        var body = await response.Content.ReadFromJsonAsync<AdminResetPasswordResponse>();
        Assert.NotNull(body);
        Assert.InRange(body.TemporaryPassword.Length, 12, 16);
        Assert.All(body.TemporaryPassword, c => Assert.True(char.IsAsciiLetterOrDigit(c)));

        // The OLD password no longer authenticates…
        var oldLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = target.Email,
            password = "password123"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // …and the temporary credential logs in (out-of-band handoff works).
        var tempLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = target.Email,
            password = body.TemporaryPassword
        });
        Assert.Equal(HttpStatusCode.OK, tempLogin.StatusCode);
    }

    [Fact]
    public async Task PostAdminUsersResetPassword_AuditRowIsCredentialFree()
    {
        if (!HasLiveDatabase()) return;

        var target = await SeedUserAsync(UserRole.Staff);
        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{target.UserId}/reset-password");
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminResetPasswordResponse>();
        Assert.NotNull(body);

        // Inspect the persisted audit rows for this reset: the credential MUST
        // NOT appear anywhere (AUM-003 `credential-absent-audit-logs`).
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditRows = await context.AuditLogs
            .AsNoTracking()
            .Where(l => l.ResourceId == target.UserId && l.ActionType == AuditActionType.ResetPassword)
            .ToListAsync();

        Assert.NotEmpty(auditRows);
        foreach (var row in auditRows)
        {
            Assert.DoesNotContain(body.TemporaryPassword, row.Details);
        }
    }

    [Fact]
    public async Task PostAdminUsersResetPassword_UnknownUser_ReturnsNotFound()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{Guid.NewGuid()}/reset-password");
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // W1 (verify remediation): `reset-self-allowed` was covered only at
    // controller-unit level — this proves the end-to-end self-reset path
    // (real generator + hash + 200 body → working temporary credential).
    [Fact]
    public async Task PostAdminUsersResetPassword_SelfReset_ReturnsOk_AndTempCredentialLogsIn()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, adminId, adminEmail) = await SeedAdminAndLoginAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{adminId}/reset-password");
        request.Headers.Add("Cookie", adminCookie);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminResetPasswordResponse>();
        Assert.NotNull(body);
        Assert.InRange(body.TemporaryPassword.Length, 12, 16);

        // The temporary credential immediately authenticates the admin account.
        var tempLogin = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = adminEmail,
            password = body.TemporaryPassword
        });
        Assert.Equal(HttpStatusCode.OK, tempLogin.StatusCode);
    }

    // W2 (verify remediation): mirror the PUT missing-header negative test for
    // the reset POST — the CSRF middleware is method-based and must reject it.
    [Fact]
    public async Task PostAdminUsersResetPassword_WithoutCsrfHeader_ReturnsBadRequest()
    {
        if (!HasLiveDatabase()) return;

        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();
        var target = await SeedUserAsync(UserRole.Staff);

        // A client WITHOUT the X-CSRF-PROTECT default header (contrary to the ctor).
        var bareClient = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{target.UserId}/reset-password");
        request.Headers.Add("Cookie", adminCookie);
        var response = await bareClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CSRF header required", body);
    }

    #endregion

    #region AUM-002 / AUM-004 — SinAcceso + next-login session semantics

    [Fact]
    public async Task SinAcceso_LoginStillSucceeds_RoleGatedEndpointsReturn403OnlyAfterNextLogin()
    {
        if (!HasLiveDatabase()) return;

        // GIVEN a logged-in Staff user (cookie1 carries the frozen Staff claim)
        var target = await SeedUserAsync(UserRole.Staff);
        var oldCookie = await LoginAsync(target.Email, "password123");

        var staffAllowed = new HttpRequestMessage(HttpMethod.Get, "/api/events/manage");
        staffAllowed.Headers.Add("Cookie", oldCookie);
        var beforeChange = await _client.SendAsync(staffAllowed);
        Assert.Equal(HttpStatusCode.OK, beforeChange.StatusCode);

        // WHEN an admin changes the role to SinAcceso
        var (adminCookie, _, _) = await SeedAdminAndLoginAsync();
        var changeRole = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{target.UserId}/role")
        {
            Content = JsonContent.Create(new { role = "SinAcceso" })
        };
        changeRole.Headers.Add("Cookie", adminCookie);
        var changeResponse = await _client.SendAsync(changeRole);
        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        // THEN the OLD cookie keeps Staff authority (AUM-004: no session
        // revocation — the JWT role claim is frozen until the cookie expires).
        var oldCookieRequest = new HttpRequestMessage(HttpMethod.Get, "/api/events/manage");
        oldCookieRequest.Headers.Add("Cookie", oldCookie);
        var withOldCookie = await _client.SendAsync(oldCookieRequest);
        Assert.Equal(HttpStatusCode.OK, withOldCookie.StatusCode);

        // AND the NEXT login still succeeds (AUM-002: login has no role check)…
        var newCookie = await LoginAsync(target.Email, "password123");

        // …but role-gated endpoints now return 403 (no policy grants SinAcceso).
        var gatedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/events/manage");
        gatedRequest.Headers.Add("Cookie", newCookie);
        var withNewCookie = await _client.SendAsync(gatedRequest);
        Assert.Equal(HttpStatusCode.Forbidden, withNewCookie.StatusCode);
    }

    // C1 (verify remediation, AUM-002): EventOwnership is in the SHALL-grant-
    // nothing set — a SinAcceso user who still OWNS events (revoked organizer
    // keeps ownership rows after the role change) must get 403 on every
    // ownership-gated endpoint, on the owner path too.
    [Fact]
    public async Task SinAcceso_EventOwner_IsDenied403_OnAllOwnershipGatedEndpoints()
    {
        if (!HasLiveDatabase()) return;

        var owner = await SeedUserAsync(UserRole.SinAcceso);
        var eventId = await SeedEventAsync(owner.UserId);
        var ownerCookie = await LoginAsync(owner.Email, "password123");

        // GET /api/events/{id}/manage — owner event detail
        var manage = new HttpRequestMessage(HttpMethod.Get, $"/api/events/{eventId}/manage");
        manage.Headers.Add("Cookie", ownerCookie);
        var manageResponse = await _client.SendAsync(manage);
        Assert.Equal(HttpStatusCode.Forbidden, manageResponse.StatusCode);

        // PUT /api/events/{id} — owner event update
        var update = new HttpRequestMessage(HttpMethod.Put, $"/api/events/{eventId}")
        {
            Content = JsonContent.Create(new
            {
                name = "Owned Event",
                description = "Attempted edit by revoked owner",
                date = DateTime.UtcNow.AddDays(3),
                location = "Test Location"
            })
        };
        update.Headers.Add("Cookie", ownerCookie);
        var updateResponse = await _client.SendAsync(update);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);

        // POST /api/uploads/event-image — revoked owner: SinAcceso matches NO
        // role in RequireOrganizadorRole (Organizador/Admin) → 403 (EIM-002).
        // Also proves the new event-agnostic endpoint stays gated for revoked users.
        var upload = new HttpRequestMessage(HttpMethod.Post, "/api/uploads/event-image")
        {
            Content = new MultipartFormDataContent()
        };
        upload.Headers.Add("Cookie", ownerCookie);
        var uploadResponse = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);

        // EIM-006: the removed POST /api/events/{id}/image route no longer
        // resolves — 404, not the old Forbidden/Conflict.
        var oldImage = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/image")
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        };
        oldImage.Headers.Add("Cookie", ownerCookie);
        var oldImageResponse = await _client.SendAsync(oldImage);
        Assert.Equal(HttpStatusCode.NotFound, oldImageResponse.StatusCode);

        // GET /api/metrics/events/{id} — owner metrics
        var metrics = new HttpRequestMessage(HttpMethod.Get, $"/api/metrics/events/{eventId}");
        metrics.Headers.Add("Cookie", ownerCookie);
        var metricsResponse = await _client.SendAsync(metrics);
        Assert.Equal(HttpStatusCode.Forbidden, metricsResponse.StatusCode);
    }

    #endregion

    #region Helpers

    private async Task<(string Cookie, Guid AdminId, string Email)> SeedAdminAndLoginAsync()
    {
        // The admin is seeded once per factory and its cookie reused — the
        // Login rate limiter (10/min/IP) must not be exhausted by this class.
        return await _factory.EnsureAdminAsync(LoginAsync);
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

    /// <summary>
    /// Seeds an event owned by the given organizer id (verify remediation C1:
    /// exercises the EventOwnership owner path for a SinAcceso user).
    /// </summary>
    private async Task<Guid> SeedEventAsync(Guid organizerId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = new Event
        {
            Name = "Owned Event",
            Description = "Seeded for the SinAcceso owner-denial test",
            Date = DateTime.UtcNow.AddDays(7),
            Location = "Test Location",
            ImageUrl = string.Empty,
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Events.Add(evt);
        await context.SaveChangesAsync();
        return evt.Id;
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
