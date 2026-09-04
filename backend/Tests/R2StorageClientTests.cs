using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Transport-level tests for the R2 HTTP client (EIM-001): the client MUST NOT
/// force any <see cref="SslProtocols"/> value — the SocketsHttpHandler must keep
/// <see cref="SslClientAuthenticationOptions.EnabledSslProtocols"/> at its default
/// (<see cref="SslProtocols.None"/> = OS-default negotiation, TLS 1.3 preferred).
/// Forcing Tls12 breaks the OpenSSL 3.x handshake against Cloudflare R2 on Linux
/// ("sslv3 alert handshake failure", error 0A000410); OS defaults succeed.
/// </summary>
public class R2StorageClientTests
{
    [Fact]
    public void Constructor_DoesNotForceEnabledSslProtocols()
    {
        // Arrange — minimal config so the constructor can build the client
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CloudflareR2:AccessKey", "test-access-key" },
                { "CloudflareR2:SecretKey", "test-secret-key" },
                { "CloudflareR2:ServiceUrl", "https://test-account.r2.cloudflarestorage.com" }
            })
            .Build();

        // Act
        var client = new R2StorageClient(configuration);

        // Assert — the handler's SslOptions must be untouched (OS defaults).
        // SslProtocols.None means "use the OS default negotiation" (TLS 1.3
        // preferred); anything else (e.g. Tls12) would be a regression.
        var handler = GetSocketsHttpHandler(client);
        Assert.Equal(SslProtocols.None, handler.SslOptions.EnabledSslProtocols);
    }

    [Fact]
    public void Constructor_UsesSocketsHttpHandler()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "CloudflareR2:AccessKey", "test-access-key" },
                { "CloudflareR2:SecretKey", "test-secret-key" },
                { "CloudflareR2:ServiceUrl", "https://test-account.r2.cloudflarestorage.com" }
            })
            .Build();

        // Act
        var client = new R2StorageClient(configuration);

        // Assert — SslOptions is reachable through a SocketsHttpHandler, so the
        // reflection below inspects the actual TLS configuration in use.
        Assert.IsType<SocketsHttpHandler>(GetSocketsHttpHandler(client));
    }

    /// <summary>
    /// Reaches the private HttpClient inside R2StorageClient and returns its
    /// underlying SocketsHttpHandler. The handler is not exposed publicly, so a
    /// regression to TLS forcing must be caught reflectively (EIM-001 scenario
    /// "no-protocol-forcing-remains").
    /// </summary>
    private static SocketsHttpHandler GetSocketsHttpHandler(R2StorageClient client)
    {
        var httpClientField = typeof(R2StorageClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("R2StorageClient._httpClient field not found");
        var httpClient = (HttpClient)httpClientField.GetValue(client)!;

        // HttpClient._handler is a PRIVATE field declared on the base class
        // HttpMessageInvoker — GetField on the derived type returns null, so the
        // hierarchy is walked explicitly.
        var handlerField = FindInstanceField(typeof(HttpClient), "_handler");
        return (SocketsHttpHandler)handlerField.GetValue(httpClient)!;
    }

    private static FieldInfo FindInstanceField(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                return field;
            }
        }

        throw new InvalidOperationException($"{type.Name}.{name} field not found");
    }
}