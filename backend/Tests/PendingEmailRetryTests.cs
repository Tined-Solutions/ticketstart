using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// TDD tests for the email retry queue (QueueEmailRetryAsync + RetryPendingEmailsAsync).
/// Validates spec scenarios: Failure enqueued, Admin retry → re-sent, Exhaustion → marked exhausted.
/// </summary>
public class PendingEmailRetryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMercadoPagoClient> _mockMpClient;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;
    private readonly TicketService _ticketService;
    private readonly IOptions<MercadoPagoOptions> _options;
    private readonly IOptions<ReservationTokenOptions> _tokenOptions;

    public PendingEmailRetryTests()
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
            _mockLogger.Object,
            TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Task 3.3 — Failure enqueued

    [Fact]
    public async Task QueueEmailRetryAsync_InsertsPendingRow()
    {
        var reservationId = Guid.NewGuid();
        var ticketIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await _paymentService.QueueEmailRetryAsync(
            reservationId,
            "pay-001",
            "buyer@test.com",
            ticketIds,
            "SmtpException: timeout");

        var row = await _context.PendingEmailSends
            .FirstOrDefaultAsync(p => p.PaymentId == "pay-001");

        Assert.NotNull(row);
        Assert.Equal(reservationId, row!.ReservationId);
        Assert.Equal("buyer@test.com", row.RecipientEmail);
        Assert.Equal(ticketIds.Length, row.TicketIds.Count);
        Assert.Equal("SmtpException: timeout", row.LastError);
        Assert.Equal(0, row.Attempts);
        Assert.Equal(5, row.MaxAttempts);
        Assert.Equal("pending", row.Status);
    }

    #endregion

    #region Task 3.3 — Admin retry → email re-sent

    [Fact]
    public async Task RetryPendingEmailsAsync_ResendsAndUpdatesStatus()
    {
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        _context.Events.Add(new Event
        {
            Id = eventId, Name = "Test", Description = "D",
            Date = DateTime.UtcNow.AddDays(1), Location = "L",
            OrganizerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _context.TicketTypes.Add(new TicketType
        {
            Id = ticketTypeId, EventId = eventId, Name = "GA",
            Price = 50m, Quantity = 100, CreatedAt = DateTime.UtcNow
        });
        _context.Reservations.Add(new Reservation
        {
            Id = reservationId, EventId = eventId, TicketTypeId = ticketTypeId,
            Quantity = 1, PurchaserDNI = "111", ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active, CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = ticketId, EventId = eventId, TicketTypeId = ticketTypeId,
            ReservationId = reservationId, PurchaserEmail = "retry@test.com",
            PurchaserDNI = "111", QRCodeData = "qr1", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.PendingEmailSends.Add(new PendingEmailSend
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            PaymentId = "pay-retry",
            RecipientEmail = "retry@test.com",
            TicketIds = [ticketId],
            Attempts = 1,
            MaxAttempts = 5,
            Status = "pending",
            LastError = "Previous failure",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var response = await _paymentService.RetryPendingEmailsAsync();

        Assert.NotNull(response);
        Assert.Equal(1, response.Attempted);
        Assert.Equal(1, response.Sent);

        var updated = await _context.PendingEmailSends.FindAsync(
            (await _context.PendingEmailSends.FirstAsync()).Id);
        Assert.Equal("sent", updated!.Status);
    }

    [Fact]
    public async Task RetryPendingEmailsAsync_Exhaustion_MarksExhausted()
    {
        // TRIANGULATION: row at max-1 attempts, send fails → becomes exhausted
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        _context.Events.Add(new Event
        {
            Id = eventId, Name = "Test", Description = "D",
            Date = DateTime.UtcNow.AddDays(1), Location = "L",
            OrganizerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        _context.TicketTypes.Add(new TicketType
        {
            Id = ticketTypeId, EventId = eventId, Name = "GA",
            Price = 50m, Quantity = 100, CreatedAt = DateTime.UtcNow
        });
        _context.Reservations.Add(new Reservation
        {
            Id = reservationId, EventId = eventId, TicketTypeId = ticketTypeId,
            Quantity = 1, PurchaserDNI = "222", ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active, CreatedAt = DateTime.UtcNow
        });
        _context.Tickets.Add(new Ticket
        {
            Id = ticketId, EventId = eventId, TicketTypeId = ticketTypeId,
            ReservationId = reservationId, PurchaserEmail = "exhaust@test.com",
            PurchaserDNI = "222", QRCodeData = "qr2", IsRefunded = false,
            CreatedAt = DateTime.UtcNow
        });
        _context.PendingEmailSends.Add(new PendingEmailSend
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            PaymentId = "pay-exhaust",
            RecipientEmail = "exhaust@test.com",
            TicketIds = [ticketId],
            Attempts = 4,
            MaxAttempts = 5,
            Status = "pending",
            LastError = "Previous failure",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _mockEmailService
            .Setup(e => e.SendTicketEmailAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Ticket>>(), It.IsAny<Event>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Still failing"));

        var response = await _paymentService.RetryPendingEmailsAsync();

        Assert.Equal(1, response.Attempted);
        Assert.Equal(0, response.Sent);
        Assert.Equal(1, response.Failed);
        Assert.Equal(1, response.Exhausted);

        var updated = await _context.PendingEmailSends.FindAsync(
            (await _context.PendingEmailSends.FirstAsync()).Id);
        Assert.Equal("exhausted", updated!.Status);
        Assert.Equal(5, updated.Attempts);
    }

    #endregion
}
