using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

public class PaymentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IMercadoPagoClient> _mockMpClient;
    private readonly Mock<ITicketService> _mockTicketService;
    private readonly PaymentService _paymentService;
    private readonly ReservationService _reservationService;

    private const string TokenSecret = "test-reservation-token-secret-key-minimum-32-characters";

    public PaymentServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(dbOptions);
        _mockMpClient = new Mock<IMercadoPagoClient>();
        _mockTicketService = new Mock<ITicketService>();

        var tokenOptions = Options.Create(new ReservationTokenOptions
        {
            TokenSecretKey = TokenSecret
        });

        _paymentService = new PaymentService(
            _context,
            _mockMpClient.Object,
            Options.Create(new MercadoPagoOptions
            {
                AccessToken = "test-access-token",
                FrontendUrl = "https://front.test"
            }),
            tokenOptions,
            _mockTicketService.Object,
            new Mock<IEmailService>().Object,
            new Mock<ILogger<PaymentService>>().Object,
            TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));

        _reservationService = new ReservationService(
            _context,
            new Mock<ILogger<ReservationService>>().Object,
            tokenOptions,
            TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreatePaymentPreferenceAsync_WithTokenBoundToDifferentReservation_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var tokenReservationId = Guid.NewGuid();
        var requestedReservationId = Guid.NewGuid();
        var token = _reservationService.GenerateReservationToken(tokenReservationId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _paymentService.CreatePaymentPreferenceAsync(requestedReservationId, token));

        _mockMpClient.Verify(
            client => client.CreatePreferenceAsync(It.IsAny<MercadoPagoPreferenceRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("https://front.test", "approved")]
    [InlineData("http://localhost:5173", null)]
    public async Task CreatePaymentPreferenceAsync_SetsAutoReturnOnlyForPublicHttpsFrontend(string frontendUrl, string? expectedAutoReturn)
    {
        // Arrange — seed an active reservation with event + ticket type
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
            Price = 50m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 2,
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

        var token = _reservationService.GenerateReservationToken(reservation.Id);

        MercadoPagoPreferenceRequest? captured = null;
        _mockMpClient
            .Setup(c => c.CreatePreferenceAsync(It.IsAny<MercadoPagoPreferenceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MercadoPagoPreferenceRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new MercadoPagoPreferenceResponse
            {
                Id = "pref-123",
                InitPoint = "https://mp.test/checkout/pref-123"
            });

        // Act — build the service with the frontend URL under test
        var result = await CreatePaymentService(frontendUrl).CreatePaymentPreferenceAsync(reservation.Id, token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("pref-123", result.PreferenceId);
        Assert.NotNull(captured);
        Assert.Equal(expectedAutoReturn, captured.AutoReturn);
        Assert.Contains("/checkout/success", captured.BackUrls!.Success);
        Assert.DoesNotContain("preference_id", captured.BackUrls!.Success);

        // The failure back URL must carry the event id, not a synthetic status:
        // Mercado Pago appends its own status query param and a seeded value
        // would shadow it on the return page.
        Assert.NotNull(captured.BackUrls!.Failure);
        Assert.Contains($"/checkout/return?event={reservation.EventId}", captured.BackUrls!.Failure);
        Assert.DoesNotContain("status=", captured.BackUrls!.Failure);

        // The pending back URL must not seed status either — MP owns that param.
        // `origin=pending` is our own non-colliding fallback marker.
        Assert.NotNull(captured.BackUrls!.Pending);
        Assert.Contains("/checkout/return?origin=pending", captured.BackUrls!.Pending);
        Assert.DoesNotContain("status=", captured.BackUrls!.Pending);
    }

    /// <summary>
    /// Builds a PaymentService sharing this fixture's context/mocks with a given
    /// FrontendUrl so the AutoReturn gating can be exercised.
    /// </summary>
    private PaymentService CreatePaymentService(string frontendUrl) => new(
        _context,
        _mockMpClient.Object,
        Options.Create(new MercadoPagoOptions { AccessToken = "test-access-token", FrontendUrl = frontendUrl }),
        Options.Create(new ReservationTokenOptions { TokenSecretKey = TokenSecret }),
        _mockTicketService.Object,
        new Mock<IEmailService>().Object,
        new Mock<ILogger<PaymentService>>().Object,
        TimeProvider.System,
        Options.Create(new HideExpiredEventsOptions()));

    #region WI7 — ConfirmPaymentAsync reasons (return from Mercado Pago)

    /// <summary>
    /// Seeds an active reservation with its user, event and ticket type so the
    /// approved-payment path can confirm it end to end.
    /// </summary>
    private async Task<Reservation> SeedActiveReservationAsync()
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
            Price = 50m,
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 2,
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

        return reservation;
    }

    /// <summary>
    /// Wires GetPreferenceAsync to resolve to the reservation and
    /// SearchPaymentsByExternalReferenceAsync to return one payment per status.
    /// </summary>
    private void SetupPreferenceAndPayments(Guid reservationId, params string[] paymentStatuses)
    {
        const string preferenceId = "pref-test";

        _mockMpClient
            .Setup(c => c.GetPreferenceAsync(preferenceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPreferenceDetail
            {
                Id = preferenceId,
                ExternalReference = reservationId.ToString()
            });

        var payments = paymentStatuses
            .Select((status, index) => new MercadoPagoPaymentInfo
            {
                Id = $"pay-{index + 1}",
                Status = status
            })
            .ToList();

        _mockMpClient
            .Setup(c => c.SearchPaymentsByExternalReferenceAsync(reservationId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payments);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NoPaymentsFound_SetsNoPaymentReason()
    {
        var reservation = await SeedActiveReservationAsync();
        SetupPreferenceAndPayments(reservation.Id);

        var result = await _paymentService.ConfirmPaymentAsync("pref-test");

        Assert.False(result.Success);
        Assert.Equal("no_payment", result.ConfirmReason);
        Assert.Equal("No payment found for this preference", result.Error);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_PendingPayment_SetsPaymentPendingReason()
    {
        var reservation = await SeedActiveReservationAsync();
        SetupPreferenceAndPayments(reservation.Id, "pending");

        var result = await _paymentService.ConfirmPaymentAsync("pref-test");

        Assert.False(result.Success);
        Assert.Equal("payment_pending", result.ConfirmReason);
        Assert.Equal("A payment is still pending for this preference", result.Error);
    }

    [Theory]
    [InlineData("in_process")]
    [InlineData("authorized")]
    [InlineData("PENDING")]
    public async Task ConfirmPaymentAsync_NonTerminalStatuses_SetsPaymentPendingReason(string status)
    {
        var reservation = await SeedActiveReservationAsync();
        SetupPreferenceAndPayments(reservation.Id, status);

        var result = await _paymentService.ConfirmPaymentAsync("pref-test");

        Assert.False(result.Success);
        Assert.Equal("payment_pending", result.ConfirmReason);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_RejectedOnlyPayment_SetsNoPaymentReason()
    {
        var reservation = await SeedActiveReservationAsync();
        SetupPreferenceAndPayments(reservation.Id, "rejected");

        var result = await _paymentService.ConfirmPaymentAsync("pref-test");

        Assert.False(result.Success);
        Assert.Equal("no_payment", result.ConfirmReason);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_ApprovedPayment_ReturnsSuccess()
    {
        var reservation = await SeedActiveReservationAsync();
        SetupPreferenceAndPayments(reservation.Id, "approved");

        _mockTicketService
            .Setup(s => s.CreateTicketsAsync(reservation.Id, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Ticket>());

        var result = await _paymentService.ConfirmPaymentAsync("pref-test");

        Assert.True(result.Success);
        Assert.Equal("pay-1", result.PaymentId);
        Assert.Null(result.ConfirmReason);
    }

    #endregion
}
