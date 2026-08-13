using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Middleware;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Factory that points the host at the backend content root without forcing
/// any particular configuration values. Tests set the environment variables
/// they need for the scenario under test.
/// </summary>
public class ConfigurableApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);

        // Background services try to connect to the real database; remove them
        // from the integration-test host to keep tests fast and isolated.
        builder.ConfigureServices(services =>
        {
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
            }
        });
    }
}

/// <summary>
/// Tests for startup configuration validation, safe parsing, password policy,
/// and error-log redaction introduced in Batch 1 (JD-S1, JD-S7, JD-S8,
/// JD-W16, JD-W18, JD-SG15).
/// </summary>
[Collection("EnvConfigTests")]
public class ConfigValidationTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();

    public void Dispose()
    {
        foreach (var (name, original) in _originalValues)
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }

    private void SetEnv(string name, string? value)
    {
        if (!_originalValues.ContainsKey(name))
        {
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }

    private void SetValidConfig()
    {
        SetEnv("Resend__ApiKey", "test-resend-api-key");
        SetEnv("Resend__FromEmail", "tickets@example.com");
        SetEnv("CloudflareR2__AccessKey", "test-r2-access-key");
        SetEnv("CloudflareR2__SecretKey", "test-r2-secret-key");
        SetEnv("CloudflareR2__ServiceUrl", "https://test-account.r2.cloudflarestorage.com");
        SetEnv("Jwt__SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789");
        SetEnv("Jwt__Issuer", "TicketeraOnlineTest");
        SetEnv("Jwt__Audience", "TicketeraOnlineTestAudience");
    }

    #region JD-S1: JWT placeholder rejection

    [Fact]
    public void Startup_WithPlaceholderJwtSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        SetValidConfig();
        SetEnv("Jwt__SecretKey", "YOUR_JWT_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG_FOR_SECURITY");
        var factory = new ConfigurableApiFactory();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("JWT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_WithShortJwtSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        SetValidConfig();
        SetEnv("Jwt__SecretKey", "short");
        var factory = new ConfigurableApiFactory();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("32", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_WithValidJwtSecretKey_StartsSuccessfully()
    {
        // Arrange
        SetValidConfig();
        var factory = new ConfigurableApiFactory();

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    #endregion

    #region JD-SG15: GetRequiredValue helper

    [Fact]
    public void Startup_WithMissingRequiredConfigValue_ThrowsInvalidOperationException()
    {
        // Arrange
        SetValidConfig();
        SetEnv("Resend__ApiKey", "");
        var factory = new ConfigurableApiFactory();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Resend", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ApiKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_WithPresentRequiredConfigValue_StartsSuccessfully()
    {
        // Arrange
        SetValidConfig();
        var factory = new ConfigurableApiFactory();

        // Act
        using var client = factory.CreateClient();

        // Assert
        Assert.NotNull(client);
    }

    #endregion

    #region JD-S7: int.TryParse fallback for ExpirationMinutes

    [Fact]
    public async Task AuthService_WithNonNumericExpirationMinutes_FallsBackTo1440()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789"},
                {"Jwt:Issuer", "TicketeraOnlineTest"},
                {"Jwt:Audience", "TicketeraOnlineTestAudience"},
                {"Jwt:ExpirationMinutes", "not-a-number"}
            })
            .Build();

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>();
        var authService = new AuthService(context, configuration, logger);

        // Act
        var createResult = await authService.CreateUserAsync(
            "Expiration Test",
            "expiration@example.com",
            "password123",
            UserRole.Organizador);

        // Assert - user creation succeeds
        Assert.True(createResult.Success, $"User creation should succeed. Error: {createResult.Error}");
        Assert.NotEqual(Guid.Empty, createResult.UserId);

        // Login to obtain the JWT generated with the fallback expiration
        var loginResult = await authService.LoginAsync(new LoginRequest
        {
            Email = "expiration@example.com",
            Password = "password123"
        });
        Assert.True(loginResult.Success, $"Login should succeed. Error: {loginResult.Error}");
        Assert.NotEmpty(loginResult.Token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.ReadJwtToken(loginResult.Token);
        var expectedExpiration = DateTime.UtcNow.AddMinutes(1440);
        Assert.True(
            Math.Abs((token.ValidTo - expectedExpiration).TotalMinutes) < 5,
            $"Expected expiration close to 1440 minutes from now, but got {token.ValidTo:O}");
    }

    #endregion

    #region JD-W18: password minimum length 8

    [Fact]
    public async Task AuthService_Register_WithSevenCharacterPassword_IsRejected()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789"},
                {"Jwt:Issuer", "TicketeraOnlineTest"},
                {"Jwt:Audience", "TicketeraOnlineTestAudience"},
                {"Jwt:ExpirationMinutes", "1440"}
            })
            .Build();

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>();
        var authService = new AuthService(context, configuration, logger);

        // Act
        var result = await authService.CreateUserAsync(
            "Short Password",
            "shortpass@example.com",
            "1234567",
            UserRole.Organizador);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("8", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthService_CreateUser_WithEightCharacterPassword_IsAccepted()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"Jwt:SecretKey", "ThisIsAVerySecureSecretKeyForTestingPurposesOnly123456789"},
                {"Jwt:Issuer", "TicketeraOnlineTest"},
                {"Jwt:Audience", "TicketeraOnlineTestAudience"},
                {"Jwt:ExpirationMinutes", "1440"}
            })
            .Build();

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AuthService>();
        var authService = new AuthService(context, configuration, logger);

        // Act
        var result = await authService.CreateUserAsync(
            "Eight Password",
            "eightpass@example.com",
            "12345678",
            UserRole.Organizador);

        // Assert
        Assert.True(result.Success, $"User creation should succeed. Error: {result.Error}");
        Assert.NotEqual(Guid.Empty, result.UserId);
    }

    #endregion

    #region JD-W16: StackTrace redaction

    [Fact]
    public async Task GlobalExceptionHandler_LogsMessageOnlyAndRedactsStackTrace()
    {
        // Arrange
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = "/test";
        httpContext.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException("Something went wrong");

        // Act
        await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        // Assert
        var entry = logger.Entries.FirstOrDefault(e => e.LogLevel == LogLevel.Error);
        Assert.NotNull(entry);
        Assert.Contains("Something went wrong", entry.Message);
        Assert.DoesNotContain("   at ", entry.Message);

        var stackTraceScope = logger.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .FirstOrDefault(s => s.Any(kvp => kvp.Key == "StackTrace"));
        Assert.NotNull(stackTraceScope);
    }

    /// <summary>
    /// Simple in-memory logger that captures formatted log messages and scopes for assertions.
    /// </summary>
    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();
        public List<object> Scopes { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, EventId EventId, Exception? Exception, string Message);

    #endregion

    #region JD-S8: HttpClient BaseAddress set via AddHttpClient delegate

    [Fact]
    public void MercadoPagoClient_Constructor_DoesNotSetBaseAddress()
    {
        // Arrange
        var httpClient = new HttpClient();
        var options = Microsoft.Extensions.Options.Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-access-token"
        });
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<MercadoPagoClient>();

        // Act
        var client = new MercadoPagoClient(httpClient, options, logger);

        // Assert
        Assert.Null(httpClient.BaseAddress);
    }

    #endregion
}
