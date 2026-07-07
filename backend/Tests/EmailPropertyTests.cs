using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for email delivery functionality.
/// Validates Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 12.4
/// </summary>
public class EmailPropertyTests
{
    private readonly Mock<IResendClient> _mockResendClient;
    private readonly Mock<ITicketService> _mockTicketService;
    private readonly Mock<ILogger<EmailService>> _mockLogger;
    private readonly IOptions<ResendOptions> _options;
    private readonly EmailService _emailService;

    public EmailPropertyTests()
    {
        _mockResendClient = new Mock<IResendClient>();
        _mockTicketService = new Mock<ITicketService>();
        _mockLogger = new Mock<ILogger<EmailService>>();
        _options = Options.Create(new ResendOptions
        {
            ApiKey = "test-resend-api-key",
            FromEmail = "tickets@ticketera.example.com",
            MaxRetryAttempts = 3,
            RetryDelayMilliseconds = 0
        });

        _emailService = new EmailService(
            _mockResendClient.Object,
            _mockTicketService.Object,
            _mockLogger.Object,
            _options);
    }

    private static Event CreateEvent(string name = "Test Event")
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test event description",
            Date = new DateTime(2026, 8, 15, 20, 0, 0, DateTimeKind.Utc),
            Location = "Test Location",
            ImageUrl = "https://example.com/image.jpg",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static TicketType CreateTicketType(Event eventEntity, string name = "General Admission", decimal price = 100m)
    {
        return new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = name,
            Price = price,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow,
            Event = eventEntity
        };
    }

    private static Ticket CreateTicket(Event eventEntity, TicketType ticketType, string qrCodeData, string email = "buyer@example.com")
    {
        return new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = email,
            PurchaserDNI = "12345678",
            QRCodeData = qrCodeData,
            IsUsed = false,
            UsedAt = null,
            CreatedAt = DateTime.UtcNow,
            Event = eventEntity,
            TicketType = ticketType
        };
    }

    #region Property 22: Email Contains All Ticket QR Codes

    [Fact]
    public async Task Property22_TicketEmail_ContainsAllQRCodes_ForMultipleTickets()
    {
        var eventEntity = CreateEvent("Music Festival");
        var ticketType = CreateTicketType(eventEntity, "General Admission", 100m);
        var tickets = new[]
        {
            CreateTicket(eventEntity, ticketType, "qr-data-1"),
            CreateTicket(eventEntity, ticketType, "qr-data-2"),
            CreateTicket(eventEntity, ticketType, "qr-data-3")
        };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns<string>(qr => $"base64-{qr}");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-qr-123" });

        var result = await _emailService.SendTicketEmailAsync("buyer@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Contains("base64-qr-data-1", captured!.Html);
        Assert.Contains("base64-qr-data-2", captured.Html);
        Assert.Contains("base64-qr-data-3", captured.Html);
        Assert.Contains("data:image/png;base64,", captured.Html);
    }

    [Fact]
    public async Task Property22_TicketEmail_ContainsQRCode_ForSingleTicket()
    {
        var eventEntity = CreateEvent("Solo Concert");
        var ticketType = CreateTicketType(eventEntity, "VIP", 250m);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "single-qr-data") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage("single-qr-data"))
            .Returns("base64-single-qr");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-single-123" });

        var result = await _emailService.SendTicketEmailAsync("single@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Contains("base64-single-qr", captured!.Html);
    }

    [Fact]
    public async Task Property22_TicketEmail_GeneratesQRImageForEachTicket()
    {
        var eventEntity = CreateEvent("Comedy Night");
        var ticketType = CreateTicketType(eventEntity, "Standard", 50m);
        var tickets = new[]
        {
            CreateTicket(eventEntity, ticketType, "qr-a"),
            CreateTicket(eventEntity, ticketType, "qr-b")
        };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns<string>(qr => $"img-{qr}");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-gen-123" });

        await _emailService.SendTicketEmailAsync("gen@example.com", tickets, eventEntity);

        _mockTicketService.Verify(t => t.GenerateQRCodeImage("qr-a"), Times.Once);
        _mockTicketService.Verify(t => t.GenerateQRCodeImage("qr-b"), Times.Once);
        Assert.Contains("img-qr-a", captured!.Html);
        Assert.Contains("img-qr-b", captured.Html);
    }

    #endregion

    #region Property 23: Email Contains Event Details

    [Fact]
    public async Task Property23_TicketEmail_ContainsEventNameDateAndLocation()
    {
        var eventEntity = CreateEvent("Tech Conference 2026");
        var ticketType = CreateTicketType(eventEntity);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "qr-event") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-event-qr");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-event-123" });

        var result = await _emailService.SendTicketEmailAsync("event@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Contains(eventEntity.Name, captured!.Html);
        Assert.Contains(eventEntity.Date.ToString("yyyy-MM-dd"), captured.Html);
        Assert.Contains(eventEntity.Location, captured.Html);
    }

    [Fact]
    public async Task Property23_TicketEmail_EventDetails_SurviveDifferentEvents()
    {
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Jazz in the Park",
            Description = "Outdoor jazz festival",
            Date = new DateTime(2026, 9, 20, 19, 30, 0, DateTimeKind.Utc),
            Location = "Central Park Stage",
            ImageUrl = "",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var ticketType = CreateTicketType(eventEntity, "General", 75m);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "qr-jazz") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-jazz-qr");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-jazz-123" });

        await _emailService.SendTicketEmailAsync("jazz@example.com", tickets, eventEntity);

        Assert.NotNull(captured);
        Assert.Contains("Jazz in the Park", captured!.Html);
        Assert.Contains("2026-09-20", captured.Html);
        Assert.Contains("Central Park Stage", captured.Html);
    }

    #endregion

    #region Property 24: Email Contains Purchase Confirmation

    [Fact]
    public async Task Property24_TicketEmail_ContainsTotalAmountAndTicketCount()
    {
        var eventEntity = CreateEvent("Food & Wine Expo");
        var ticketType = CreateTicketType(eventEntity, "Tasting Pass", 120m);
        var tickets = new[]
        {
            CreateTicket(eventEntity, ticketType, "qr-purchase-1"),
            CreateTicket(eventEntity, ticketType, "qr-purchase-2")
        };
        var expectedTotal = 240m;

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-purchase-qr");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-purchase-123" });

        var result = await _emailService.SendTicketEmailAsync("purchase@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Contains(expectedTotal.ToString("0.00", CultureInfo.InvariantCulture), captured!.Html);
        Assert.Contains(tickets.Length.ToString(), captured.Html);
        Assert.Contains(ticketType.Name, captured.Html);
    }

    [Fact]
    public async Task Property24_TicketEmail_PurchaseConfirmation_ForMixedTicketTypes()
    {
        var eventEntity = CreateEvent("Gaming Convention");
        var vipType = CreateTicketType(eventEntity, "VIP Pass", 300m);
        var standardType = CreateTicketType(eventEntity, "Standard Pass", 100m);
        var tickets = new[]
        {
            CreateTicket(eventEntity, vipType, "qr-vip"),
            CreateTicket(eventEntity, standardType, "qr-standard")
        };
        var expectedTotal = 400m;

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-mixed-qr");

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-mixed-123" });

        await _emailService.SendTicketEmailAsync("mixed@example.com", tickets, eventEntity);

        Assert.NotNull(captured);
        Assert.Contains(expectedTotal.ToString("0.00", CultureInfo.InvariantCulture), captured!.Html);
        Assert.Contains("VIP Pass", captured.Html);
        Assert.Contains("Standard Pass", captured.Html);
    }

    #endregion

    #region Property 25: Email Delivery Retry on Failure

    [Fact]
    public async Task Property25_TicketEmail_RetriesOnFailure_AndSucceedsEventually()
    {
        var eventEntity = CreateEvent("Retry Event");
        var ticketType = CreateTicketType(eventEntity);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "qr-retry") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-retry-qr");

        var attempts = 0;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts < 3)
                    throw new HttpRequestException("Resend API unavailable");
                return new ResendEmailResponse { Id = "email-retry-123" };
            });

        var result = await _emailService.SendTicketEmailAsync("retry@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        Assert.Equal(3, attempts);
        _mockResendClient.Verify(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Property25_TicketEmail_LogsEachFailedAttempt_AndFinalSuccess()
    {
        var eventEntity = CreateEvent("Log Retry Event");
        var ticketType = CreateTicketType(eventEntity);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "qr-log-retry") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-log-retry-qr");

        var attempts = 0;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts < 2)
                    throw new HttpRequestException("Temporary failure");
                return new ResendEmailResponse { Id = "email-log-retry-123" };
            });

        var result = await _emailService.SendTicketEmailAsync("logretry@example.com", tickets, eventEntity);

        Assert.True(result.Success);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email delivery failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email sent successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Property25_TicketEmail_ReturnsFailureAfterMaxRetriesExceeded()
    {
        var eventEntity = CreateEvent("Max Retry Event");
        var ticketType = CreateTicketType(eventEntity);
        var tickets = new[] { CreateTicket(eventEntity, ticketType, "qr-max-retry") };

        _mockTicketService
            .Setup(t => t.GenerateQRCodeImage(It.IsAny<string>()))
            .Returns("base64-max-retry-qr");

        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Resend API permanently unavailable"));

        var result = await _emailService.SendTicketEmailAsync("maxretry@example.com", tickets, eventEntity);

        Assert.False(result.Success);
        Assert.Contains("Resend API permanently unavailable", result.Error);
        _mockResendClient.Verify(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    #endregion

    #region Property 40: Refund Notification Email

    [Fact]
    public async Task Property40_RefundEmail_ContainsRecipientAmountAndReason()
    {
        const string recipient = "refund@example.com";
        const decimal amount = 199.99m;
        const string reason = "Event cancelled by organizer";

        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-refund-123" });

        var result = await _emailService.SendRefundNotificationAsync(recipient, amount, reason);

        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.Equal(recipient, captured!.To);
        Assert.Contains(amount.ToString("0.00", CultureInfo.InvariantCulture), captured.Html);
        Assert.Contains(reason, captured.Html);
        Assert.Contains("Refund", captured.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property40_RefundEmail_UsesConfiguredFromAddress()
    {
        ResendEmailRequest? captured = null;
        _mockResendClient
            .Setup(c => c.SendEmailAsync(It.IsAny<ResendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new ResendEmailResponse { Id = "email-refund-from-123" });

        await _emailService.SendRefundNotificationAsync("from@example.com", 50m, "Duplicate charge");

        Assert.NotNull(captured);
        Assert.Equal(_options.Value.FromEmail, captured!.From);
    }

    #endregion
}
