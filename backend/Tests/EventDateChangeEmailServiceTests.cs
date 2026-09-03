using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Services;
using TicketeraOnline.Api.Services.Templates;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for EmailService.SendEventDateChangeNotificationAsync.
/// Validates spec EDC-003: template rendered with correct args, sender identity
/// uses Resend:FromName ("Ticketera") and refund contact uses Resend:FromEmail.
/// </summary>
public class EventDateChangeEmailServiceTests
{
    private readonly Mock<IResendClient> _mockResendClient;
    private readonly Mock<ITicketService> _mockTicketService;
    private readonly Mock<ILogger<EmailService>> _mockLogger;
    private readonly BrevoOptions _brevoOptions;
    private readonly EmailService _emailService;

    public EventDateChangeEmailServiceTests()
    {
        _mockResendClient = new Mock<IResendClient>();
        _mockTicketService = new Mock<ITicketService>();
        _mockLogger = new Mock<ILogger<EmailService>>();

        _brevoOptions = new BrevoOptions
        {
            ApiKey = "test-api-key",
            FromEmail = "tickets@ticketera.com",
            FromName = "Ticketera",
            MaxRetryAttempts = 1,
            RetryDelayMilliseconds = 100
        };

        _emailService = new EmailService(
            _mockResendClient.Object,
            _mockTicketService.Object,
            _mockLogger.Object,
            Options.Create(_brevoOptions));
    }

    [Fact]
    public async Task SendEventDateChangeNotificationAsync_SendsEmail_AndReturnsSuccess()
    {
        _mockResendClient
            .Setup(r => r.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ResendEmailResponse { Id = "resend-id-123" });

        var result = await _emailService.SendEventDateChangeNotificationAsync(
            recipientEmail: "buyer@test.com",
            eventName: "Rock Fest",
            oldDate: new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc),
            newDate: new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc));

        Assert.True(result.Success);
        Assert.Null(result.Error);

        _mockResendClient.Verify(
            r => r.SendEmailAsync(
                It.Is<ResendEmailRequest>(req =>
                    req.To == "buyer@test.com" &&
                    req.Html.Contains("Rock Fest") &&
                    req.Html.Contains("15/10/2026") &&
                    req.Html.Contains("01/11/2026") &&
                    req.Html.Contains("tickets@ticketera.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEventDateChangeNotificationAsync_UsesResolvedFrom()
    {
        _mockResendClient
            .Setup(r => r.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ResendEmailResponse { Id = "resend-id-456" });

        await _emailService.SendEventDateChangeNotificationAsync(
            "buyer@test.com", "Rock Fest",
            new DateTime(2026, 10, 15), new DateTime(2026, 11, 1));

        _mockResendClient.Verify(
            r => r.SendEmailAsync(
                It.Is<ResendEmailRequest>(req =>
                    req.From == "\"Ticketera\" <tickets@ticketera.com>"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEventDateChangeNotificationAsync_SubjectContainsEventName()
    {
        _mockResendClient
            .Setup(r => r.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ResendEmailResponse { Id = "resend-id-789" });

        await _emailService.SendEventDateChangeNotificationAsync(
            "buyer@test.com", "Rock Fest",
            new DateTime(2026, 10, 15), new DateTime(2026, 11, 1));

        _mockResendClient.Verify(
            r => r.SendEmailAsync(
                It.Is<ResendEmailRequest>(req =>
                    req.Subject.Contains("Rock Fest")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// TRIANGULATION: Send failure returns Success=false with error message.
    /// </summary>
    [Fact]
    public async Task SendEventDateChangeNotificationAsync_WhenResendFails_ReturnsFailure()
    {
        _mockResendClient
            .Setup(r => r.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var result = await _emailService.SendEventDateChangeNotificationAsync(
            "buyer@test.com", "Rock Fest",
            new DateTime(2026, 10, 15), new DateTime(2026, 11, 1));

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("Network error", result.Error);
    }

    /// <summary>
    /// TRIANGULATION: Different event parameters produce different content.
    /// </summary>
    [Fact]
    public async Task SendEventDateChangeNotificationAsync_DifferentEvent_DifferentContent()
    {
        string? capturedHtml = null;
        _mockResendClient
            .Setup(r => r.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((req, _) => capturedHtml = req.Html)
            .ReturnsAsync(() => new ResendEmailResponse { Id = "resend-id" });

        await _emailService.SendEventDateChangeNotificationAsync(
            "buyer@test.com", "Jazz Night",
            new DateTime(2026, 8, 1), new DateTime(2026, 9, 15));

        Assert.NotNull(capturedHtml);
        Assert.Contains("Jazz Night", capturedHtml);
        Assert.Contains("01/08/2026", capturedHtml);
        Assert.Contains("15/09/2026", capturedHtml);
    }
}
