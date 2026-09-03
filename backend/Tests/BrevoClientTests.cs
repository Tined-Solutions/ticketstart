using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for BrevoClient: payload mapping to Brevo's API v3 shape
/// (sender name/email split, htmlContent, attachment base64/contentId),
/// the api-key header, and error handling on non-success responses.
/// </summary>
public class BrevoClientTests
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

    private static BrevoClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new FakeHandler(responder))
        {
            BaseAddress = new Uri("https://api.brevo.com/v3/"),
        };
        var options = Options.Create(new BrevoOptions
        {
            ApiKey = "brevo-test-key",
        });
        return new BrevoClient(httpClient, options, NullLogger<BrevoClient>.Instance);
    }

    [Fact]
    public async Task SendEmailAsync_ParsesSenderAndMapsPayload_ToBrevoShape()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-123" }),
            };
        });

        var request = new ResendEmailRequest
        {
            From = "\"TicketStart\" <tickets@ticketera.com>",
            To = "cliente@example.com",
            Subject = "Tus entradas",
            Html = "<p>Hola</p>",
            Attachments =
            [
                new ResendAttachment
                {
                    Filename = "qr-ticket-1.png",
                    Content = "aGVsbG8=",
                    ContentId = "qr-ticket-1",
                },
            ],
        };

        var response = await client.SendEmailAsync(request);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(new Uri("https://api.brevo.com/v3/smtp/email"), captured.RequestUri);
        Assert.Equal("brevo-test-key", captured.Headers.GetValues("api-key").Single());
        Assert.Equal("msg-123", response.Id);

        var payload = await captured!.Content!.ReadFromJsonAsync<BrevoPayloadSnapshot>();
        Assert.NotNull(payload);
        Assert.Equal("TicketStart", payload.Sender!.Name);
        Assert.Equal("tickets@ticketera.com", payload.Sender.Email);
        Assert.Equal("cliente@example.com", Assert.Single(payload.To!).Email);
        Assert.Equal("Tus entradas", payload.Subject);
        Assert.Equal("<p>Hola</p>", payload.HtmlContent);
        var attachment = Assert.Single(payload.Attachments!);
        Assert.Equal("qr-ticket-1.png", attachment.Name);
        Assert.Equal("aGVsbG8=", attachment.Content);
        Assert.Equal("qr-ticket-1", attachment.ContentId);
    }

    [Fact]
    public async Task SendEmailAsync_OmitsNullContentId_WhenNoAttachment()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-456" }),
            };
        });

        var request = new ResendEmailRequest
        {
            From = "tickets@ticketera.com",
            To = "cliente@example.com",
            Subject = "Reembolso",
            Html = "<p>Ok</p>",
        };

        await client.SendEmailAsync(request);

        var payload = await captured!.Content!.ReadFromJsonAsync<BrevoPayloadSnapshot>();
        Assert.NotNull(payload);
        Assert.Null(payload.Attachments);
        Assert.Equal(string.Empty, payload.Sender!.Name);
    }

    [Fact]
    public async Task SendEmailAsync_NonSuccess_ThrowsHttpRequestException()
    {
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"code\":\"invalid_parameter\"}"),
            });

        var request = new ResendEmailRequest
        {
            From = "tickets@ticketera.com",
            To = "cliente@example.com",
            Subject = "Test",
            Html = "<p>Test</p>",
        };

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendEmailAsync(request));

        Assert.Contains("400", exception.Message);
    }

    private sealed class BrevoPayloadSnapshot
    {
        public BrevoSenderSnapshot? Sender { get; set; }
        public List<BrevoRecipientSnapshot>? To { get; set; }
        public string? Subject { get; set; }
        public string? HtmlContent { get; set; }
        public List<BrevoAttachmentSnapshot>? Attachments { get; set; }
    }

    private sealed class BrevoSenderSnapshot
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    private sealed class BrevoRecipientSnapshot
    {
        public string? Email { get; set; }
    }

    private sealed class BrevoAttachmentSnapshot
    {
        public string? Name { get; set; }
        public string? Content { get; set; }
        public string? ContentId { get; set; }
    }
}