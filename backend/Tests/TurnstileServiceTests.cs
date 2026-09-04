using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for TurnstileService JSON deserialization.
/// Regression guard: Cloudflare returns camelCase ("success", "error-codes")
/// while System.Text.Json defaults are case-sensitive — a valid token used
/// to be rejected because Success deserialized as false.
/// </summary>
public class TurnstileServiceTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private static TurnstileService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new FakeHandler(responder));
        var options = Options.Create(new TurnstileOptions { SecretKey = "test-secret" });
        return new TurnstileService(httpClient, options, NullLogger<TurnstileService>.Instance);
    }

    [Fact]
    public async Task VerifyTokenAsync_SuccessResponse_ReturnsTrue()
    {
        var service = CreateService(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"challenge_ts\":\"2026-09-03T00:00:00Z\",\"hostname\":\"ticketstart.pages.dev\",\"error-codes\":[]}"),
            });

        var result = await service.VerifyTokenAsync("valid-token");

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyTokenAsync_FailureResponse_ReturnsFalse()
    {
        var service = CreateService(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}"),
            });

        var result = await service.VerifyTokenAsync("bad-token");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyTokenAsync_NonJsonResponse_ReturnsFalse()
    {
        var service = CreateService(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("oops"),
            });

        var result = await service.VerifyTokenAsync("token");

        Assert.False(result);
    }
}