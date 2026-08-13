using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Factory that points the integration-test host at the backend content root
/// (the folder that contains Program.cs and appsettings.json).
/// </summary>
public class TicketeraApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalValues = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var backendRoot = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "..", "..", ".."));
        builder.UseContentRoot(backendRoot);

        // Provide minimal non-placeholder values via environment variables so the
        // host can start without relying on environment-specific secrets.
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
            var hostedServices = services.Where(d => d.ServiceType == typeof(IHostedService)).ToList();
            foreach (var descriptor in hostedServices)
            {
                services.Remove(descriptor);
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
/// Integration tests verifying that scaffold/template endpoints and controllers
/// have been removed from the application (JD-C7).
/// </summary>
[Collection("EnvConfigTests")]
public class ScaffoldRemovalTests : IClassFixture<TicketeraApiFactory>
{
    private readonly HttpClient _client;

    public ScaffoldRemovalTests(TicketeraApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/weatherforecast")]
    [InlineData("/api/testauthorization/public")]
    [InlineData("/api/testauthorization/protected")]
    [InlineData("/api/testauthorization/admin")]
    [InlineData("/api/testauthorization/organizador")]
    [InlineData("/api/testauthorization/staff")]
    [InlineData("/api/testauthorization/event/12345")]
    public async Task RemovedScaffoldEndpoints_ReturnNotFound(string path)
    {
        // Act
        var response = await _client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
