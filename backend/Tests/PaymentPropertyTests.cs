using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for payment processing functionality.
/// Validates Requirements 5.1, 5.2, 5.3, 5.5, 5.6, 5.7, 5.8, 12.2, 12.3, 16.5
/// </summary>
public class PaymentPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMercadoPagoClient> _mockMpClient;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;
    private readonly TicketService _ticketService;
    private readonly IOptions<MercadoPagoOptions> _options;

    public PaymentPropertyTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(dbOptions);
        _mockMpClient = new Mock<IMercadoPagoClient>();
        _mockLogger = new Mock<ILogger<PaymentService>>();
        _options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "test-access-token",
            WebhookSecret = "test-webhook-secret-min-32-characters-long"
        });

        var ticketConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["QRCode:HmacSecretKey"] = "test-hmac-secret-key-minimum-32-characters-long-for-security" })
            .Build();

        var ticketLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TicketService>();
        _ticketService = new TicketService(_context, ticketConfig, ticketLogger);

        _paymentService = new PaymentService(
            _context,
            _mockMpClient.Object,
            _options,
            _ticketService,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(User User, Event Event, TicketType TicketType, Reservation Reservation)> SetupReservationAsync(
        int quantity = 2,
        int ticketQuantity = 10,
        decimal price = 50m,
        bool expired = false)
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
            ExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10),
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

    #region Property 14: Payment Preference Contains Complete Data

    [Fact]
    public async Task Property14_CreatePreference_IncludesReservationDetailsAndTotalAmount()
    {
        var (_, _, ticketType, reservation) = await SetupReservationAsync(quantity: 3, price: 75m);
        var expectedTotal = reservation.Quantity * ticketType.Price;

        MercadoPagoPreferenceRequest? capturedRequest = null;
        _mockMpClient
            .Setup(c => c.CreatePreferenceAsync(It.IsAny<MercadoPagoPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoPreferenceRequest, CancellationToken>((r, _) => capturedRequest = r)
            .ReturnsAsync(new MercadoPagoPreferenceResponse
            {
                Id = "pref-123",
                InitPoint = "https://mp.test/checkout/pref-123"
            });

        var result = await _paymentService.CreatePaymentPreferenceAsync(reservation.Id);

        Assert.NotNull(result);
        Assert.Equal("pref-123", result.PreferenceId);
        Assert.Equal("https://mp.test/checkout/pref-123", result.CheckoutUrl);
        Assert.NotNull(capturedRequest);
        Assert.Equal(reservation.Id.ToString(), capturedRequest!.ExternalReference);
        Assert.Single(capturedRequest.Items);
        Assert.Equal(reservation.Quantity, capturedRequest.Items[0].Quantity);
        Assert.Equal(ticketType.Price, capturedRequest.Items[0].UnitPrice);
        Assert.Equal(expectedTotal, capturedRequest.Items[0].Quantity * capturedRequest.Items[0].UnitPrice);
        Assert.Contains(ticketType.Name, capturedRequest.Items[0].Title);
    }

    [Fact]
    public async Task Property14_CreatePreference_RequiresActiveReservation()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(expired: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _paymentService.CreatePaymentPreferenceAsync(reservation.Id));

        Assert.Contains("active", exception.Message, StringComparison.OrdinalIgnoreCase);
        _mockMpClient.Verify(c => c.CreatePreferenceAsync(It.IsAny<MercadoPagoPreferenceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Property 15: Successful Payment Creates Tickets

    [Fact]
    public async Task Property15_ApprovedWebhook_ConfirmsReservationAndCreatesTickets()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 3);

        _mockMpClient
            .Setup(c => c.CreatePreferenceAsync(It.IsAny<MercadoPagoPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPreferenceResponse
            {
                Id = "pref-123",
                InitPoint = "https://mp.test/checkout/pref-123"
            });

        var preference = await _paymentService.CreatePaymentPreferenceAsync(reservation.Id);

        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = reservation.Id.ToString(),
            Status = "approved"
        };
        var signature = ComputeSignature(payload, _options.Value.WebhookSecret);

        var result = await _paymentService.ProcessWebhookAsync(payload, signature);

        Assert.True(result.Success);
        Assert.Equal("pay-123", result.PaymentId);

        var updatedReservation = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Confirmed, updatedReservation!.Status);

        var tickets = await _context.Tickets.Where(t => t.EventId == reservation.EventId).ToListAsync();
        Assert.Equal(reservation.Quantity, tickets.Count);
    }

    [Fact]
    public async Task Property15_ApprovedWebhook_TicketsCarryReservationDNIAndAreLookupable()
    {
        // Regression: tickets created via the approved-payment webhook must carry the reservation's real DNI.
        var (user, _, _, reservation) = await SetupReservationAsync(quantity: 2);
        reservation.PurchaserDNI = "44332211";
        await _context.SaveChangesAsync();

        var payload = new WebhookPayload
        {
            PaymentId = "pay-dni",
            ExternalReference = reservation.Id.ToString(),
            Status = "approved"
        };
        var signature = ComputeSignature(payload, _options.Value.WebhookSecret);

        var result = await _paymentService.ProcessWebhookAsync(payload, signature);

        Assert.True(result.Success);

        var tickets = await _context.Tickets.Where(t => t.EventId == reservation.EventId).ToListAsync();
        Assert.Equal(reservation.Quantity, tickets.Count);
        Assert.All(tickets, t =>
        {
            Assert.Equal(user.Email, t.PurchaserEmail);
            Assert.Equal(reservation.PurchaserDNI, t.PurchaserDNI);
            Assert.NotEqual("00000000", t.PurchaserDNI);
        });

        var lookedUp = await _ticketService.LookupTicketsAsync(user.Email, reservation.PurchaserDNI);
        Assert.Equal(tickets.Count, lookedUp.Count());
    }

    #endregion

    #region Property 16: Failed Payment Releases Reservation

    [Fact]
    public async Task Property16_RejectedWebhook_CancelsReservation()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 2);

        var payload = new WebhookPayload
        {
            PaymentId = "pay-rejected",
            ExternalReference = reservation.Id.ToString(),
            Status = "rejected"
        };
        var signature = ComputeSignature(payload, _options.Value.WebhookSecret);

        var result = await _paymentService.ProcessWebhookAsync(payload, signature);

        Assert.True(result.Success);

        var updatedReservation = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Cancelled, updatedReservation!.Status);

        var tickets = await _context.Tickets.Where(t => t.EventId == reservation.EventId).ToListAsync();
        Assert.Empty(tickets);
    }

    #endregion

    #region Property 17: Webhook Signature Validation

    [Fact]
    public void Property17_ValidSignature_AcceptsWebhook()
    {
        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var signature = ComputeHmacSha256(payloadJson, _options.Value.WebhookSecret);

        var isValid = PaymentService.ValidateWebhookSignature(payloadJson, signature, _options.Value.WebhookSecret);

        Assert.True(isValid);
    }

    [Fact]
    public void Property17_InvalidSignature_RejectsWebhook()
    {
        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);

        var isValid = PaymentService.ValidateWebhookSignature(payloadJson, "invalid-signature", _options.Value.WebhookSecret);

        Assert.False(isValid);
    }

    [Fact]
    public async Task Property17_InvalidSignature_ReturnsUnauthorized()
    {
        var (_, _, _, reservation) = await SetupReservationAsync();

        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = reservation.Id.ToString(),
            Status = "approved"
        };

        var result = await _paymentService.ProcessWebhookAsync(payload, "invalid-signature");

        Assert.False(result.Success);
        Assert.Contains("signature", result.Error!, StringComparison.OrdinalIgnoreCase);

        var updatedReservation = await _context.Reservations.FindAsync(reservation.Id);
        Assert.Equal(ReservationStatus.Active, updatedReservation!.Status);
    }

    #endregion

    #region Property 38: Stock Failure Triggers Refund

    [Fact]
    public async Task Property38_StockFailure_TriggersRefund()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 5, ticketQuantity: 5);

        // Simulate stock failure: reservation expired so inventory was released and sold to someone else
        reservation.Status = ReservationStatus.Expired;
        await _context.SaveChangesAsync();

        _mockMpClient
            .Setup(c => c.RefundPaymentAsync("pay-stock-fail", 250m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoRefundResponse
            {
                Id = "refund-1",
                PaymentId = "pay-stock-fail",
                Amount = 250m,
                Status = "approved"
            });

        var payload = new WebhookPayload
        {
            PaymentId = "pay-stock-fail",
            ExternalReference = reservation.Id.ToString(),
            Status = "approved"
        };
        var signature = ComputeSignature(payload, _options.Value.WebhookSecret);

        var result = await _paymentService.ProcessWebhookAsync(payload, signature);

        Assert.True(result.Success);
        _mockMpClient.Verify(c => c.RefundPaymentAsync("pay-stock-fail", 250m, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Property 39: Refund Logging

    [Fact]
    public async Task Property39_Refund_LogsRefundedTransaction()
    {
        var (_, _, _, reservation) = await SetupReservationAsync(quantity: 2, price: 100m);
        var amount = 200m;

        _mockMpClient
            .Setup(c => c.RefundPaymentAsync("pay-123", amount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoRefundResponse
            {
                Id = "refund-123",
                PaymentId = "pay-123",
                Amount = amount,
                Status = "approved"
            });

        var result = await _paymentService.InitiateRefundAsync("pay-123", amount, reservation.Id);

        Assert.True(result.Success);
        Assert.Equal("refund-123", result.RefundId);

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.MercadoPagoId == "pay-123" && t.Status == TransactionStatus.Refunded);

        Assert.NotNull(transaction);
        Assert.Equal(reservation.Id, transaction.ReservationId);
        Assert.Equal(amount, transaction.Amount);
    }

    #endregion

    private static string ComputeSignature(WebhookPayload payload, string secret)
    {
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
        return ComputeHmacSha256(payloadJson, secret);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
