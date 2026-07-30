using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// TDD tests for the refactored webhook processing pipeline.
/// Validates: Spec scenarios — Real MP envelope → accepted, Missing data.id → 200 ACK,
/// Approved → tickets + email, Duplicate → idempotent, Rejected → failed path,
/// Email failure → logged + queued.
/// </summary>
public class PaymentServiceWebhookTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMercadoPagoClient> _mockMpClient;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;
    private readonly TicketService _ticketService;
    private readonly IOptions<MercadoPagoOptions> _options;
    private readonly IOptions<ReservationTokenOptions> _tokenOptions;

    public PaymentServiceWebhookTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(dbOptions);
        _mockMpClient = new Mock<IMercadoPagoClient>();
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<PaymentService>>();
        _options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-access-token",
            WebhookSecret = "test-webhook-secret-min-32-characters-long"
        });
        _tokenOptions = Options.Create(new ReservationTokenOptions
        {
            TokenSecretKey = "test-reservation-token-secret-key-minimum-32-characters"
        });

        var ticketConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["QRCode:HmacSecretKey"] = "test-hmac-secret-key-minimum-32-characters-long-for-security" })
            .Build();

        var ticketLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TicketService>();
        _ticketService = new TicketService(_context, ticketConfig, ticketLogger,
            new ServiceCollection().BuildServiceProvider());

        _paymentService = new PaymentService(
            _context,
            _mockMpClient.Object,
            _options,
            _tokenOptions,
            _ticketService,
            _mockEmailService.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(User User, Event Event, TicketType TicketType, Reservation Reservation)> SetupReservationAsync(
        int quantity = 2, int ticketQuantity = 10, decimal price = 50m)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "buyer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = price,
            Quantity = ticketQuantity,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = quantity,
            PurchaserDNI = "12345678",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.Events.Add(eventEntity);
        _context.TicketTypes.Add(ticketType);
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return (user, eventEntity, ticketType, reservation);
    }

    #region Task 1.1 — DTO Deserialization

    [Fact]
    public void Deserialize_RealMercadoPagoEnvelope_CorrectlyParsesActionAndDataId()
    {
        var mpJson = """
            {
                "action": "payment.updated",
                "type": "payment",
                "data": {
                    "id": "123456789"
                }
            }
            """;

        var envelope = JsonSerializer.Deserialize<MercadoPagoWebhookEnvelope>(mpJson);

        Assert.NotNull(envelope);
        Assert.Equal("payment.updated", envelope!.Action);
        Assert.Equal("payment", envelope.Type);
        Assert.NotNull(envelope.Data);
        Assert.Equal("123456789", envelope.Data!.Id);
    }

    [Fact]
    public void Deserialize_Envelope_MissingData_StillParses()
    {
        var mpJson = """{"action": "payment.updated", "type": "payment"}""";

        var envelope = JsonSerializer.Deserialize<MercadoPagoWebhookEnvelope>(mpJson);

        Assert.NotNull(envelope);
        Assert.Equal("payment.updated", envelope!.Action);
        Assert.Null(envelope.Data);
    }

    #endregion

    #region Task 3.2 — Approved webhook via envelope → tickets + email

    [Fact]
    public async Task Webhook_ApprovedPayment_CreatesTicketsAndSendsEmail()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 3);

        var paymentId = "pay-approved-new";
        _mockMpClient
            .Setup(c => c.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentDetail
            {
                Id = paymentId,
                Status = "approved",
                ExternalReference = reservation.Id.ToString(),
                TransactionAmount = 150
            });

        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = paymentId }
        };

        var result = await _paymentService.ProcessWebhookAsync(envelope, string.Empty);

        Assert.True(result.Success);
        Assert.Equal(paymentId, result.PaymentId);

        var updatedReservation = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, updatedReservation!.Status);

        var tickets = await _context.Tickets.Where(t => t.EventId == reservation.EventId).ToListAsync();
        Assert.Equal(reservation.Quantity, tickets.Count);

        _mockEmailService.Verify(
            e => e.SendTicketEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Ticket>>(), It.IsAny<Event>()),
            Times.Once);
    }

    #endregion

    #region Task 3.2 — Duplicate webhook → idempotent

    [Fact]
    public async Task Webhook_DuplicatePaymentId_ReturnsIdempotent200()
    {
        // RED: idempotency via GetPaymentByIdAsync path
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 2);

        var paymentId = "pay-dup-new";
        _mockMpClient
            .Setup(c => c.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentDetail
            {
                Id = paymentId,
                Status = "approved",
                ExternalReference = reservation.Id.ToString(),
                TransactionAmount = 100
            });

        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = paymentId }
        };

        // First call — should process
        var result1 = await _paymentService.ProcessWebhookAsync(envelope, string.Empty);
        Assert.True(result1.Success);

        // Second call — should be idempotent (existing transaction)
        var result2 = await _paymentService.ProcessWebhookAsync(envelope, string.Empty);
        Assert.True(result2.Success);
        Assert.Equal(paymentId, result2.PaymentId);

        var transactions = await _context.Transactions
            .Where(t => t.MercadoPagoId == paymentId && t.Status == TransactionStatus.Approved)
            .ToListAsync();
        Assert.Single(transactions);
    }

    #endregion

    #region Task 3.2 — Rejected payment → failed path

    [Fact]
    public async Task Webhook_RejectedPayment_CancelsReservation()
    {
        // RED: rejected status via GetPaymentByIdAsync
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 2);

        var paymentId = "pay-rejected-new";
        _mockMpClient
            .Setup(c => c.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentDetail
            {
                Id = paymentId,
                Status = "rejected",
                ExternalReference = reservation.Id.ToString(),
                TransactionAmount = 100
            });

        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = paymentId }
        };

        var result = await _paymentService.ProcessWebhookAsync(envelope, string.Empty);

        Assert.True(result.Success);

        var updatedReservation = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, updatedReservation!.Status);

        var tickets = await _context.Tickets.Where(t => t.EventId == reservation.EventId).ToListAsync();
        Assert.Empty(tickets);
    }

    #endregion

    #region Task 3.2 — Email failure → logged + queued

    [Fact]
    public async Task Webhook_EmailFails_LogsErrorAndQueuesRetry()
    {
        // RED: email catch block currently only logs; after GREEN it should queue
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 2);

        var paymentId = "pay-email-fail";
        _mockMpClient
            .Setup(c => c.GetPaymentByIdAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentDetail
            {
                Id = paymentId,
                Status = "approved",
                ExternalReference = reservation.Id.ToString(),
                TransactionAmount = 100
            });

        _mockEmailService
            .Setup(e => e.SendTicketEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Ticket>>(), It.IsAny<Event>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = paymentId }
        };

        var result = await _paymentService.ProcessWebhookAsync(envelope, string.Empty);

        // Webhook should STILL return success (payment was processed, email failure is non-fatal)
        Assert.True(result.Success);

        // Verify a pending_email_send row was queued
        var pending = await _context.PendingEmailSends
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        Assert.NotNull(pending);
        Assert.Equal("pending", pending!.Status);
        Assert.Equal(0, pending.Attempts);
        Assert.Contains("SMTP failure", pending.LastError);
        Assert.Equal(reservation.Id, pending.ReservationId);
    }

    #endregion
}
