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
            new Mock<ITicketService>().Object,
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

    [Fact]
    public async Task CreatePaymentPreferenceAsync_RequestsAutoReturnOnApprovedPayment()
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

        // Act
        var result = await _paymentService.CreatePaymentPreferenceAsync(reservation.Id, token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("pref-123", result.PreferenceId);
        Assert.NotNull(captured);
        Assert.Equal("approved", captured.AutoReturn);
        Assert.Contains("/checkout/success", captured.BackUrls!.Success);
    }
}
