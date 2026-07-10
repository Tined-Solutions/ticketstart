using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for ReservationService
/// Validates Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 12.6
/// </summary>
public class ReservationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReservationService _reservationService;
    private readonly ILogger<ReservationService> _logger;

    private const string TestPurchaserDNI = "12345678";

    public ReservationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _logger = new TestLogger<ReservationService>();
        var tokenOptions = Options.Create(new ReservationTokenOptions
        {
            TokenSecretKey = "test-reservation-token-secret-key-minimum-32-characters"
        });
        _reservationService = new ReservationService(_context, _logger, tokenOptions);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Test Data Helpers

    private async Task<(Event Event, TicketType TicketType)> CreateTestEventWithTickets(int ticketQuantity = 100)
    {
        var organizerId = Guid.NewGuid();
        var user = new User
        {
            Id = organizerId,
            Email = "organizer@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = organizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);

        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General Admission",
            Price = 50.00m,
            Quantity = ticketQuantity,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);

        await _context.SaveChangesAsync();

        return (eventEntity, ticketType);
    }

    #endregion

    #region CreateReservationAsync Tests

    [Fact]
    public void GenerateReservationToken_WithSameId_ReturnsConsistentToken()
    {
        // Arrange
        var reservationId = Guid.NewGuid();

        // Act
        var token1 = _reservationService.GenerateReservationToken(reservationId);
        var token2 = _reservationService.GenerateReservationToken(reservationId);

        // Assert
        Assert.NotNull(token1);
        Assert.NotEmpty(token1);
        Assert.Equal(token1, token2);
    }

    [Fact]
    public void GenerateReservationToken_DifferentIds_ReturnsDifferentTokens()
    {
        // Arrange
        var reservationId1 = Guid.NewGuid();
        var reservationId2 = Guid.NewGuid();

        // Act
        var token1 = _reservationService.GenerateReservationToken(reservationId1);
        var token2 = _reservationService.GenerateReservationToken(reservationId2);

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public async Task CreateReservationAsync_WithValidData_CreatesReservationWith10MinuteExpiration()
    {
        // Arrange - Validates Requirement 4.1
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var userId = Guid.NewGuid();
        var quantity = 5;
        var purchaserDNI = "12345678";
        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _reservationService.CreateReservationAsync(userId, eventEntity.Id, ticketType.Id, quantity, purchaserDNI);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(eventEntity.Id, result.EventId);
        Assert.Equal(ticketType.Id, result.TicketTypeId);
        Assert.Equal(quantity, result.Quantity);
        Assert.Equal(purchaserDNI, result.PurchaserDNI);
        Assert.Equal(ReservationStatus.Active, result.Status);
        
        // Validate 10-minute expiration (Requirement 4.1)
        var expectedExpiration = beforeCreate.AddMinutes(10);
        Assert.InRange(result.ExpiresAt, expectedExpiration.AddSeconds(-5), expectedExpiration.AddSeconds(5));
    }

    [Fact]
    public async Task CreateReservationAsync_WithPurchaserDNI_StoresDNI()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var userId = Guid.NewGuid();
        var purchaserDNI = "87654321";

        // Act
        var result = await _reservationService.CreateReservationAsync(userId, eventEntity.Id, ticketType.Id, 3, purchaserDNI);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(purchaserDNI, result.PurchaserDNI);
    }

    [Fact]
    public async Task CreateReservationAsync_WithNullUserId_CreatesGuestReservation()
    {
        // Arrange - Validates Requirement 4.3 (guest purchases)
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var quantity = 3;

        // Act
        var result = await _reservationService.CreateReservationAsync(null, eventEntity.Id, ticketType.Id, quantity, TestPurchaserDNI);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.UserId);
        Assert.Equal(quantity, result.Quantity);
        Assert.Equal(ReservationStatus.Active, result.Status);
    }

    [Fact]
    public async Task CreateReservationAsync_DecrementsAvailableInventory()
    {
        // Arrange - Validates Requirement 4.2, 4.4
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var quantity = 10;

        // Act - Create first reservation
        await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, quantity, TestPurchaserDNI);

        // Assert - Check that subsequent reservation calculation accounts for active reservation
        var reservation2 = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 15, TestPurchaserDNI);
        Assert.NotNull(reservation2);
        
        // Try to reserve more than available (100 - 10 - 15 = 75 available)
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 76, TestPurchaserDNI));
    }

    [Fact]
    public async Task CreateReservationAsync_WithInvalidQuantity_ThrowsArgumentException()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 0, TestPurchaserDNI));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, -5, TestPurchaserDNI));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task CreateReservationAsync_ThrowsOnEmptyOrWhitespaceDNI(string invalidDNI)
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 1, invalidDNI));

        Assert.Contains("Purchaser DNI is required", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_ThrowsOnNullDNI()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 1, null!));

        Assert.Contains("Purchaser DNI is required", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_ThrowsOnDniOver50Chars()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var longDNI = new string('1', 51);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 1, longDNI));

        Assert.Contains("must not exceed 50 characters", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_AcceptsDniAtExactly50Chars()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var dni50 = new string('9', 50);

        // Act
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 1, dni50);

        // Assert
        Assert.NotNull(reservation);
        Assert.Equal(dni50, reservation.PurchaserDNI);
    }

    [Fact]
    public async Task CreateReservationAsync_WithInsufficientTickets_ThrowsArgumentException()
    {
        // Arrange - Validates Requirement 4.4
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(10);
        var quantity = 15; // More than available

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, quantity, TestPurchaserDNI));
        
        Assert.Contains("Insufficient tickets available", exception.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_WithNonExistentTicketType_ThrowsKeyNotFoundException()
    {
        // Arrange
        var (eventEntity, _) = await CreateTestEventWithTickets(100);
        var nonExistentTicketTypeId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, nonExistentTicketTypeId, 5, TestPurchaserDNI));
    }

    [Fact]
    public async Task CreateReservationAsync_ConsidersSoldTickets_InInventoryCalculation()
    {
        // Arrange - Validates Requirement 4.2
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        // Create sold tickets (confirmed purchases)
        for (int i = 0; i < 20; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"buyer{i}@test.com",
                PurchaserDNI = $"12345678{i}",
                QRCodeData = $"QR{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act - Try to reserve 85 tickets (100 total - 20 sold = 80 available)
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 85, TestPurchaserDNI));

        // Can reserve 80
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 80, TestPurchaserDNI);
        Assert.NotNull(reservation);
    }

    [Fact]
    public async Task CreateReservationAsync_ExcludesExpiredReservations_FromInventoryCalculation()
    {
        // Arrange - Validates Requirement 4.5
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        // Create an expired reservation
        var expiredReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 30,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        _context.Reservations.Add(expiredReservation);
        await _context.SaveChangesAsync();

        // Act - Should be able to reserve tickets, ignoring expired reservation
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 100, TestPurchaserDNI);
        
        // Assert
        Assert.NotNull(reservation);
        Assert.Equal(100, reservation.Quantity);
    }

    #endregion

    #region ValidateReservationAsync Tests

    [Fact]
    public async Task ValidateReservationAsync_WithActiveNonExpiredReservation_ReturnsTrue()
    {
        // Arrange - Validates Requirement 4.4
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 5, TestPurchaserDNI);

        // Act
        var result = await _reservationService.ValidateReservationAsync(reservation.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateReservationAsync_WithExpiredReservation_ReturnsFalse()
    {
        // Arrange - Validates Requirement 4.4
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        var expiredReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        _context.Reservations.Add(expiredReservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reservationService.ValidateReservationAsync(expiredReservation.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateReservationAsync_WithConfirmedReservation_ReturnsFalse()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        var confirmedReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(confirmedReservation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _reservationService.ValidateReservationAsync(confirmedReservation.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateReservationAsync_WithNonExistentReservation_ReturnsFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _reservationService.ValidateReservationAsync(nonExistentId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ReleaseExpiredReservationsAsync Tests

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_ReleasesExpiredActiveReservations()
    {
        // Arrange - Validates Requirement 4.5
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        // Create expired active reservations
        var expiredReservation1 = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 10,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        
        var expiredReservation2 = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        
        _context.Reservations.AddRange(expiredReservation1, expiredReservation2);
        await _context.SaveChangesAsync();

        // Act
        var releasedCount = await _reservationService.ReleaseExpiredReservationsAsync();

        // Assert
        Assert.Equal(2, releasedCount);
        
        var updatedReservation1 = await _context.Reservations.FindAsync(expiredReservation1.Id);
        var updatedReservation2 = await _context.Reservations.FindAsync(expiredReservation2.Id);
        
        Assert.Equal(ReservationStatus.Expired, updatedReservation1!.Status);
        Assert.Equal(ReservationStatus.Expired, updatedReservation2!.Status);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_IgnoresNonActiveReservations()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        // Create expired reservations with different statuses
        var expiredConfirmed = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        
        var expiredCancelled = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 3,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReservationStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        };
        
        _context.Reservations.AddRange(expiredConfirmed, expiredCancelled);
        await _context.SaveChangesAsync();

        // Act
        var releasedCount = await _reservationService.ReleaseExpiredReservationsAsync();

        // Assert
        Assert.Equal(0, releasedCount);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_IgnoresNonExpiredReservations()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        // Create non-expired active reservation
        var activeReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), // Still valid
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Reservations.Add(activeReservation);
        await _context.SaveChangesAsync();

        // Act
        var releasedCount = await _reservationService.ReleaseExpiredReservationsAsync();

        // Assert
        Assert.Equal(0, releasedCount);
        
        var reservation = await _context.Reservations.FindAsync(activeReservation.Id);
        Assert.Equal(ReservationStatus.Active, reservation!.Status);
    }

    [Fact]
    public async Task ReleaseExpiredReservationsAsync_WithNoExpiredReservations_ReturnsZero()
    {
        // Arrange
        await CreateTestEventWithTickets(100);

        // Act
        var releasedCount = await _reservationService.ReleaseExpiredReservationsAsync();

        // Assert
        Assert.Equal(0, releasedCount);
    }

    #endregion

    #region ConfirmReservationAsync Tests

    [Fact]
    public async Task ConfirmReservationAsync_WithActiveReservation_MarksAsConfirmed()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 5, TestPurchaserDNI);

        // Act
        var result = await _reservationService.ConfirmReservationAsync(reservation.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task ConfirmReservationAsync_WithNonExistentReservation_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _reservationService.ConfirmReservationAsync(nonExistentId));
    }

    [Fact]
    public async Task ConfirmReservationAsync_WithExpiredReservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        var expiredReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        _context.Reservations.Add(expiredReservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _reservationService.ConfirmReservationAsync(expiredReservation.Id));
        
        Assert.Contains("expired", exception.Message.ToLower());
    }

    [Fact]
    public async Task ConfirmReservationAsync_WithCancelledReservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        var cancelledReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = ReservationStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(cancelledReservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _reservationService.ConfirmReservationAsync(cancelledReservation.Id));
    }

    #endregion

    #region CancelReservationAsync Tests

    [Fact]
    public async Task CancelReservationAsync_WithActiveReservation_MarksAsCancelled()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 10, TestPurchaserDNI);

        // Act
        var result = await _reservationService.CancelReservationAsync(reservation.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_RestoresInventory()
    {
        // Arrange - Validates inventory restoration
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 20, TestPurchaserDNI);

        // Act
        await _reservationService.CancelReservationAsync(reservation.Id);

        // Assert - Should be able to reserve full quantity again
        var newReservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 100, TestPurchaserDNI);
        Assert.NotNull(newReservation);
    }

    [Fact]
    public async Task CancelReservationAsync_WithNonExistentReservation_ThrowsKeyNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _reservationService.CancelReservationAsync(nonExistentId));
    }

    [Fact]
    public async Task CancelReservationAsync_WithConfirmedReservation_ThrowsInvalidOperationException()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        
        var confirmedReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 5,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(confirmedReservation);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _reservationService.CancelReservationAsync(confirmedReservation.Id));
    }

    #endregion

    #region GetReservationByIdAsync Tests

    [Fact]
    public async Task GetReservationByIdAsync_WithExistingReservation_ReturnsReservation()
    {
        // Arrange
        var (eventEntity, ticketType) = await CreateTestEventWithTickets(100);
        var reservation = await _reservationService.CreateReservationAsync(Guid.NewGuid(), eventEntity.Id, ticketType.Id, 5, TestPurchaserDNI);

        // Act
        var result = await _reservationService.GetReservationByIdAsync(reservation.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reservation.Id, result.Id);
        Assert.NotNull(result.Event);
        Assert.NotNull(result.TicketType);
    }

    [Fact]
    public async Task GetReservationByIdAsync_WithNonExistentReservation_ReturnsNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _reservationService.GetReservationByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
