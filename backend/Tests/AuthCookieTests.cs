using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

#region Integration Test Factory

/// <summary>
/// Factory for Auth cookie, CSRF, and rate-limit integration tests.
/// Sets required configuration and removes background services that need a real database.
/// </summary>
public class AuthCookieApiFactory : WebApplicationFactory<Program>
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

        // Remove background services that try to connect to a real database
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

#endregion

#region B6.1 — Auth Cookie Integration Tests

public class AuthCookieIntegrationTests : IClassFixture<AuthCookieApiFactory>
{
    private readonly AuthCookieApiFactory _factory;
    private readonly HttpClient _client;

    public AuthCookieIntegrationTests(AuthCookieApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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

    private async Task<string> LoginAndGetCookieAsync(string email, string password)
    {
        var loginClient = _factory.CreateClient();
        var response = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        // Extract the Set-Cookie header for the "token" cookie
        var setCookieHeaders = response.Headers.GetValues("Set-Cookie");
        foreach (var cookie in setCookieHeaders)
        {
            if (cookie.StartsWith("token=", StringComparison.OrdinalIgnoreCase))
            {
                return cookie;
            }
        }

        return string.Empty;
    }

    private void SetAuthCookie(string cookieValue)
    {
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", cookieValue);
    }

    private void SetCsrfHeader()
    {
        _client.DefaultRequestHeaders.Remove("X-CSRF-PROTECT");
        _client.DefaultRequestHeaders.Add("X-CSRF-PROTECT", "1");
    }

    /// <summary>
    /// B6.1 Test 1: Login sets httpOnly cookie with Secure, SameSite=Lax.
    /// The login response should contain a Set-Cookie header with
    /// HttpOnly, Secure, and SameSite=Lax attributes.
    /// </summary>
    [Fact]
    public async Task Login_SetsHttpOnlyCookie_WithSecureAndSameSite()
    {
        if (!HasLiveDatabase()) return;

        // Arrange — seed a user
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"login-test-{Guid.NewGuid()}@example.com";
        var createResult = await authService.CreateUserAsync("Cookie Test User", email, "password123", UserRole.Organizador);
        Assert.True(createResult.Success, $"Failed to seed user: {createResult.Error}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "password123"
        });

        // Assert — 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify Set-Cookie header exists and has correct attributes
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies),
            "Response should contain Set-Cookie header");
        var tokenCookie = cookies.FirstOrDefault(c => c.StartsWith("token=", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tokenCookie);
        Assert.Contains("HttpOnly", tokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", tokenCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", tokenCookie, StringComparison.OrdinalIgnoreCase);

        // Verify body contains user info but NO token
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"token\"", body);
        Assert.Contains("\"userId\"", body);
    }

    /// <summary>
    /// B6.1 Test 2: GET /auth/me returns user when authenticated via cookie.
    /// </summary>
    [Fact]
    public async Task AuthMe_Authenticated_ReturnsUserInfo()
    {
        if (!HasLiveDatabase()) return;

        // Arrange — seed a user and login to get cookie
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"me-test-{Guid.NewGuid()}@example.com";
        var createResult = await authService.CreateUserAsync("Me Test User", email, "password123", UserRole.Staff);
        Assert.True(createResult.Success, $"Failed to seed user: {createResult.Error}");

        var cookie = await LoginAndGetCookieAsync(email, "password123");
        Assert.NotEmpty(cookie);
        SetAuthCookie(cookie.Split(';')[0]); // Use just "token=value" part

        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.Equal(createResult.UserId, body.Id);
        Assert.Equal(email.ToLower(), body.Email);
        Assert.Equal("Me Test User", body.Name);
        Assert.Equal(UserRole.Staff, body.Role);
    }

    /// <summary>
    /// B6.1 Test 2b: GET /auth/me returns 401 when not authenticated.
    /// </summary>
    [Fact]
    public async Task AuthMe_Unauthenticated_Returns401()
    {
        if (!HasLiveDatabase()) return;

        // Act — no cookie or auth header
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// B6.1 Test 3: POST /auth/logout clears the cookie.
    /// </summary>
    [Fact]
    public async Task Logout_ClearsCookie()
    {
        if (!HasLiveDatabase()) return;

        // Arrange — seed and login
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"logout-test-{Guid.NewGuid()}@example.com";
        var createResult = await authService.CreateUserAsync("Logout Test User", email, "password123", UserRole.Organizador);
        Assert.True(createResult.Success, $"Failed to seed user: {createResult.Error}");

        var cookie = await LoginAndGetCookieAsync(email, "password123");
        Assert.NotEmpty(cookie);
        SetAuthCookie(cookie.Split(';')[0]);
        SetCsrfHeader();

        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert — 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify Set-Cookie clears the token cookie (empty value or expired)
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies),
            "Response should contain Set-Cookie header");
        var clearCookie = cookies.FirstOrDefault(c => c.StartsWith("token=", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(clearCookie);
        // Should have expired or empty value
        Assert.True(
            clearCookie.Contains("token=;", StringComparison.OrdinalIgnoreCase) ||
            clearCookie.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase) ||
            clearCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase),
            $"Cookie should be cleared but got: {clearCookie}");
    }

    /// <summary>
    /// B6.1 Test 4: CSRF middleware rejects POST without X-CSRF-PROTECT header.
    /// </summary>
    [Fact]
    public async Task CsrfMiddleware_RejectsPost_WithoutHeader()
    {
        if (!HasLiveDatabase()) return;

        // Arrange — seed and login for auth
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"csrf-test-{Guid.NewGuid()}@example.com";
        var createResult = await authService.CreateUserAsync("CSRF Test User", email, "password123", UserRole.Organizador);
        Assert.True(createResult.Success, $"Failed to seed user: {createResult.Error}");

        var cookie = await LoginAndGetCookieAsync(email, "password123");
        Assert.NotEmpty(cookie);
        SetAuthCookie(cookie.Split(';')[0]);
        // Do NOT set CSRF header

        // Act — POST /auth/logout without CSRF header
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert — should be rejected
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CSRF", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// B6.1 Test 4b: CSRF middleware allows POST /webhook without header.
    /// </summary>
    [Fact]
    public async Task CsrfMiddleware_AllowsWebhook_WithoutHeader()
    {
        if (!HasLiveDatabase()) return;

        // Act — POST /webhook without CSRF header
        var response = await _client.PostAsync("/webhook", new StringContent("{}"));

        // Assert — should NOT be 400 (it may be 404 or 200, but NOT a CSRF rejection)
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// B6.1 Test 4c: CSRF middleware allows GET without header.
    /// </summary>
    [Fact]
    public async Task CsrfMiddleware_AllowsGet_WithoutHeader()
    {
        if (!HasLiveDatabase()) return;

        // Act — GET without CSRF header
        var response = await _client.GetAsync("/api/auth/me");

        // Assert — should NOT be a CSRF rejection (401/404 is fine)
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

#endregion

#region B6.1 — Rate Limit Integration Tests (isolated factory)

/// <summary>
/// Dedicated factory for rate-limit tests so the rate limiter is not
/// polluted by requests from other integration tests.
/// </summary>
public class AuthRateLimitApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalValues = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);
        builder.UseEnvironment("Development");

        SetConfigEnvVar("Resend__ApiKey", "test-resend-api-key");
        SetConfigEnvVar("Resend__FromEmail", "tickets@example.com");
        SetConfigEnvVar("CloudflareR2__AccessKey", "test-r2-access-key");
        SetConfigEnvVar("CloudflareR2__SecretKey", "test-r2-secret-key");
        SetConfigEnvVar("CloudflareR2__ServiceUrl", "https://test-account.r2.cloudflarestorage.com");
        SetConfigEnvVar("Jwt__SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789");

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

public class AuthRateLimitIntegrationTests : IClassFixture<AuthRateLimitApiFactory>
{
    private readonly AuthRateLimitApiFactory _factory;
    private readonly HttpClient _client;

    public AuthRateLimitIntegrationTests(AuthRateLimitApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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

    /// <summary>
    /// B6.1 Test 5: Login rate limit — 11th login in 1 minute returns 429.
    /// </summary>
    [Fact]
    public async Task LoginRateLimit_BlocksAfter10Requests_PerMinute()
    {
        if (!HasLiveDatabase()) return;

        // Arrange — seed a user
        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var email = $"ratelimit-login-{Guid.NewGuid()}@example.com";
        var createResult = await authService.CreateUserAsync("RateLimit User", email, "password123", UserRole.Organizador);
        Assert.True(createResult.Success, $"Failed to seed user: {createResult.Error}");

        // Act — send 10 login requests (all should succeed or return 401 for bad password, but not get rate-limited)
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                email,
                password = "password123"
            });
            // Should not be rate-limited yet
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // 11th request should be rate-limited
        var rateLimitedResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "password123"
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
    }

    /// <summary>
    /// B6.1 Test 6: Reservation rate limit — 6th reservation in 1 minute returns 429.
    /// </summary>
    [Fact]
    public async Task ReservationRateLimit_BlocksAfter5Requests_PerMinute()
    {
        if (!HasLiveDatabase()) return;

        // Act — send 5 reservation requests (may fail with 400/404, but NOT 429)
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/reservations", new
            {
                eventId = Guid.NewGuid(),
                ticketTypeId = Guid.NewGuid(),
                quantity = 1,
                purchaserDNI = "12345678",
                purchaserEmail = $"test-{i}@example.com",
                confirmEmail = $"test-{i}@example.com"
            });
            // Should not be rate-limited yet
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // 6th request should be rate-limited
        var rateLimitedResponse = await _client.PostAsJsonAsync("/api/reservations", new
        {
            eventId = Guid.NewGuid(),
            ticketTypeId = Guid.NewGuid(),
            quantity = 1,
            purchaserDNI = "12345678",
            purchaserEmail = "test-toomany@example.com",
            confirmEmail = "test-toomany@example.com"
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
    }
}

#endregion

#region B6.1 — Auth Controller Unit Tests (Cookie)

public class AuthControllerCookieUnitTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerCookieUnitTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _controller = new AuthController(_mockAuthService.Object, _mockLogger.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
    }

    /// <summary>
    /// B6.1 Unit Test 1: Login response sets httpOnly cookie and does NOT include token in body.
    /// </summary>
    [Fact]
    public async Task Login_SetsHttpOnlyCookie_AndRemovesTokenFromBody()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new LoginRequest { Email = "test@example.com", Password = "password123" };
        _mockAuthService.Setup(s => s.LoginAsync(request))
            .ReturnsAsync(new AuthResult
            {
                Success = true,
                Token = "fake-jwt-token-value",
                UserId = userId,
                Role = UserRole.Organizador,
                Name = "Test User"
            });

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

        // Verify cookie was set on the HttpContext response
        var setCookieHeader = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("token=fake-jwt-token-value", setCookieHeader);
        Assert.Contains("HttpOnly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", setCookieHeader, StringComparison.OrdinalIgnoreCase);

        // Verify body does NOT contain token
        var body = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.DoesNotContain("\"token\"", body);
        Assert.Contains("\"userId\"", body);
        Assert.Contains("\"role\"", body);
        Assert.Contains("\"name\"", body);
    }

    /// <summary>
    /// B6.1 Unit Test 2: GET /auth/me returns user info from claims when authenticated.
    /// </summary>
    [Fact]
    public void AuthMe_Authenticated_ReturnsUserFromClaims()
    {
        // Arrange — set up authenticated user with claims
        var userId = Guid.NewGuid();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new(System.Security.Claims.ClaimTypes.Email, "me@example.com"),
            new(System.Security.Claims.ClaimTypes.Name, "Me Test User"),
            new(System.Security.Claims.ClaimTypes.Role, "Staff")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(identity)
            }
        };

        // Act
        var result = _controller.Me();

        // Assert
        var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var body = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        Assert.Contains(userId.ToString(), body);
        Assert.Contains("me@example.com", body);
        Assert.Contains("Me Test User", body);
        Assert.Contains("Staff", body);
    }

    /// <summary>
    /// B6.1 Unit Test 2b: GET /auth/me returns 401 when not authenticated.
    /// </summary>
    [Fact]
    public void AuthMe_Unauthenticated_Returns401()
    {
        // Arrange — no authenticated user (default)

        // Act
        var result = _controller.Me();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedResult>(result);
    }

    /// <summary>
    /// B6.1 Unit Test 3: POST /auth/logout clears the cookie.
    /// </summary>
    [Fact]
    public void Logout_ClearsAuthCookie()
    {
        // Act
        var result = _controller.Logout();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkResult>(result);

        // Verify cookie is cleared (empty value, any past expiry)
        var setCookieHeader = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("token=;", setCookieHeader);
        // The cookie must either have a past expires date or Max-Age=0
        Assert.True(
            setCookieHeader.Contains("expires=", StringComparison.OrdinalIgnoreCase) ||
            setCookieHeader.Contains("Max-Age=0", StringComparison.OrdinalIgnoreCase),
            $"Cookie should have an expiry or Max-Age but got: {setCookieHeader}");
    }
}

#endregion

/// <summary>
/// Response DTO for /auth/me endpoint (used in test assertions).
/// </summary>
public record MeResponse(
    Guid Id,
    string Email,
    string Name,
    UserRole Role
);
