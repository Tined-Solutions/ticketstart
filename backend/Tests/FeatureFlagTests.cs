using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// EHE-009 tests for the HideExpiredEvents runtime feature flag (ADR-4):
/// - Missing config section -> fail-fast at startup (Program.cs guard).
/// - Section present without Enabled -> defaults to true (property initializer).
/// - Explicit Enabled=false binds correctly (runtime rollback knob).
/// Serialized with the other env-var-mutating classes (EnvConfigTests collection).
/// </summary>
[Collection("EnvConfigTests")]
public class FeatureFlagTests : IDisposable
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

    /// <summary>
    /// Satisfies every existing startup guard (Resend, R2, JWT) so the only
    /// missing piece under an empty content root is the HideExpiredEvents section.
    /// </summary>
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

    [Fact]
    public void Flag_MissingSection_FailsFast()
    {
        // Arrange: content root without appsettings.json -> no HideExpiredEvents section
        SetValidConfig();
        using var factory = new EmptyContentRootApiFactory();

        // Act & Assert: startup throws a clear configuration exception
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("HideExpiredEvents", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Flag_DefaultTrue()
    {
        // ADR-4: the section may omit Enabled — the property initializer keeps it active
        var options = new HideExpiredEventsOptions();
        Assert.True(options.Enabled);
    }

    [Fact]
    public void Flag_ExplicitFalse_BindsFalse()
    {
        // Runtime rollback knob: Enabled=false must bind from configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "HideExpiredEvents:Enabled", "false" }
            })
            .Build();

        var section = config.GetSection(HideExpiredEventsOptions.SectionName);
        Assert.True(section.Exists());
        var options = section.Get<HideExpiredEventsOptions>() ?? new HideExpiredEventsOptions();
        Assert.False(options.Enabled);
    }
}

/// <summary>
/// WebApplicationFactory whose content root is an empty temp directory, so no
/// appsettings.json is loaded and Program.cs sees no HideExpiredEvents section.
/// Background services are removed exactly like ConfigurableApiFactory.
/// </summary>
public class EmptyContentRootApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), "ticketera-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        builder.UseContentRoot(emptyRoot);

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
