using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Test-specific DbContext. Kept as a thin ApplicationDbContext subclass for SQLite
/// test scenarios; no concurrency-token overrides are needed since TicketType no
/// longer carries a RowVersion.
/// </summary>
internal class ReservationStockTestDbContext : ApplicationDbContext
{
    public ReservationStockTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
}

/// <summary>
/// Tests for atomic stock-safe reservation creation.
/// Uses SQLite in-memory to exercise the transaction + write-lock path.
/// Availability is mathematical: Quantity - sold tickets - active unexpired reservations.
/// Validates REQ-7, REQ-8: atomic reservation and concurrent stock safety.
/// </summary>
public class ReservationStockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReservationStockTestDbContext _context;
    private readonly ReservationService _reservationService;

    private const string TestPurchaserDNI = "12345678";

    public ReservationStockTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ReservationStockTestDbContext(options);
        _context.Database.EnsureCreated();

        var logger = new TestLogger<ReservationService>();
        var tokenOptions = Options.Create(new ReservationTokenOptions
        {
            TokenSecretKey = "test-reservation-token-secret-key-minimum-32-characters"
        });
        _reservationService = new ReservationService(_context, logger, tokenOptions);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<(Event Event, TicketType TicketType, User User)> CreateTestEventWithTickets(int ticketQuantity = 10)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
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
            OrganizerId = user.Id,
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
        return (eventEntity, ticketType, user);
    }

    #region B3.2: Atomic Stock Reservation

    /// <summary>
    /// When stock is sufficient, CreateReservationAsync creates an active reservation.
    /// </summary>
    [Fact]
    public async Task CreateReservation_WithSufficientStock_CreatesActiveReservation()
    {
        // Arrange
        var (_, ticketType, _) = await CreateTestEventWithTickets(10);

        // Act
        var reservation = await _reservationService.CreateReservationAsync(
            null, ticketType.EventId, ticketType.Id, 3, TestPurchaserDNI);

        // Assert
        Assert.NotNull(reservation);
        Assert.Equal(3, reservation.Quantity);
        Assert.Equal(ReservationStatus.Active, reservation.Status);

        var activeReservations = await _context.Reservations
            .Where(r => r.Status == ReservationStatus.Active)
            .SumAsync(r => r.Quantity);
        Assert.Equal(3, activeReservations);
    }

    /// <summary>
    /// When stock is insufficient, CreateReservationAsync throws ArgumentException
    /// and no reservation is created.
    /// </summary>
    [Fact]
    public async Task CreateReservation_WithInsufficientStock_ThrowsArgumentException()
    {
        // Arrange
        var (_, ticketType, _) = await CreateTestEventWithTickets(5);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 10, TestPurchaserDNI));

        Assert.Contains("Insufficient", ex.Message, StringComparison.OrdinalIgnoreCase);

        // No reservation should have been inserted
        Assert.Equal(0, await _context.Reservations.CountAsync());
    }

    /// <summary>
    /// When stock is exhausted (fully occupied by an active reservation), the next reservation fails.
    /// </summary>
    [Fact]
    public async Task CreateReservation_WhenStockExhausted_ThrowsArgumentException()
    {
        // Arrange
        var (_, ticketType, _) = await CreateTestEventWithTickets(5);
        await _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 5, TestPurchaserDNI);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 1, TestPurchaserDNI));

        Assert.Contains("Insufficient", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Multiple reservations from the same stock pool consume availability mathematically.
    /// </summary>
    [Fact]
    public async Task MultipleReservations_ConsumeStockMathematically()
    {
        // Arrange
        var (_, ticketType, _) = await CreateTestEventWithTickets(10);

        // Act — reserve 3 + 4 + 2 = 9 tickets (1 remaining)
        var r1 = await _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 3, "11111111");
        var r2 = await _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 4, "22222222");
        var r3 = await _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 2, "33333333");

        // Assert
        Assert.Equal(3, r1.Quantity);
        Assert.Equal(4, r2.Quantity);
        Assert.Equal(2, r3.Quantity);

        var activeReserved = await _context.Reservations
            .Where(r => r.TicketTypeId == ticketType.Id &&
                        r.Status == ReservationStatus.Active &&
                        r.ExpiresAt > DateTime.UtcNow)
            .SumAsync(r => r.Quantity);
        Assert.Equal(9, activeReserved);

        // Last ticket should still be available
        var r4 = await _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 1, "44444444");
        Assert.NotNull(r4);

        // 11th should fail
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _reservationService.CreateReservationAsync(null, ticketType.EventId, ticketType.Id, 1, "55555555"));
    }

    /// <summary>
    /// Concurrent reservations with only 1 available ticket: exactly one succeeds.
    /// Uses SQLite shared-cache in-memory so concurrent connections see the same data.
    /// The no-op write-lock UPDATE inside the transaction serializes the check-then-insert.
    /// </summary>
    [Fact]
    public async Task ConcurrentReservations_WithOneAvailableTicket_ExactlyOneSucceeds()
    {
        // Arrange — seed the shared in-memory database with 1 ticket
        var sharedConnString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

        using var seedConnection = new SqliteConnection(sharedConnString);
        seedConnection.Open();

        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(seedConnection)
            .Options;
        using var seedContext = new ReservationStockTestDbContext(seedOptions);
        seedContext.Database.EnsureCreated();

        var seedUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Seed Organizer",
            Email = "seed@test.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        seedContext.Users.Add(seedUser);
        var seedEvent = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Seed Event",
            Description = "desc",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "loc",
            OrganizerId = seedUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        seedContext.Events.Add(seedEvent);
        var seedTicketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = seedEvent.Id,
            Name = "GA",
            Price = 50m,
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        };
        seedContext.TicketTypes.Add(seedTicketType);
        await seedContext.SaveChangesAsync();

        int successCount = 0;
        int failureCount = 0;
        var ticketTypeId = seedTicketType.Id;
        var eventId = seedEvent.Id;

        // Act — 3 concurrent reservation attempts, each with its own SQLite connection
        var tasks = Enumerable.Range(0, 3).Select(async _ =>
        {
            using var concurrentConnection = new SqliteConnection(sharedConnString);
            concurrentConnection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(concurrentConnection)
                .Options;

            using var concurrentContext = new ReservationStockTestDbContext(options);
            var logger = new TestLogger<ReservationService>();
            var tokenOpts = Options.Create(new ReservationTokenOptions
            {
                TokenSecretKey = "test-reservation-token-secret-key-minimum-32-characters"
            });
            var service = new ReservationService(concurrentContext, logger, tokenOpts);

            try
            {
                await service.CreateReservationAsync(null, eventId, ticketTypeId, 1, TestPurchaserDNI);
                Interlocked.Increment(ref successCount);
            }
            catch (ArgumentException)
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        await Task.WhenAll(tasks);

        // Assert — exactly 1 success, 2 failures
        Assert.Equal(1, successCount);
        Assert.Equal(2, failureCount);
    }

    #endregion
}
