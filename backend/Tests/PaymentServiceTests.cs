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
            Options.Create(new MercadoPagoOptions()),
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
}
