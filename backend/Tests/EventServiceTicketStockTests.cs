using Amazon.S3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Test-specific DbContext for EventService stock tests (SQLite in-memory).
/// Exercises the transaction + no-op-UPDATE write-lock path (D-1 SQLite branch).
/// </summary>
internal class EventServiceTicketStockTestDbContext : ApplicationDbContext
{
    public EventServiceTicketStockTestDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
}

/// <summary>
/// Service-level RED tests for AddTicketStockAsync / AddTicketTypeAsync.
/// Validates ATS-002 (increment + validation), ATS-003 (concurrent serialization),
/// ATS-004 (new type + validation), ATS-006 (availability recompute).
/// </summary>
public class EventServiceTicketStockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventServiceTicketStockTestDbContext _context;
    private readonly EventService _eventService;

    public EventServiceTicketStockTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new EventServiceTicketStockTestDbContext(options);
        _context.Database.EnsureCreated();

        var configurationData = new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        _eventService = new EventService(
            _context,
            new TestLogger<EventService>(),
            configuration,
            new Mock<IAmazonS3>().Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<(Event Event, TicketType TicketType, User User)> CreateTestEventWithTicketType(int ticketQuantity = 100, string name = "General")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"organizer-{Guid.NewGuid():N}@test.com",
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
            Name = name,
            Price = 50.00m,
            Quantity = ticketQuantity,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);

        await _context.SaveChangesAsync();
        return (eventEntity, ticketType, user);
    }

    private async Task<int> AddSoldTicketsAsync(Guid eventId, Guid ticketTypeId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                PurchaserEmail = $"buyer{i}@test.com",
                PurchaserDNI = "12345678",
                QRCodeData = $"qr-{Guid.NewGuid():N}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
        return count;
    }

    #region ATS-002: Increment existing ticket type stock

    [Fact]
    public async Task AddTicketStockAsync_WithValidQuantity_PersistsIncrementAndRecomputesAvailability()
    {
        // Arrange — Quantity=100 (ATS-002 happy path)
        var (eventEntity, ticketType, _) = await CreateTestEventWithTicketType(100);

        // Act
        var result = await _eventService.AddTicketStockAsync(eventEntity.Id, ticketType.Id, 50);

        // Assert — returned shape: { id, name, price, quantity, available }
        Assert.Equal(ticketType.Id, result.Id);
        Assert.Equal(150, result.Quantity);
        Assert.Equal(150, result.Available); // no sold/reserved yet

        // Persisted in DB
        var persisted = await _context.TicketTypes.SingleAsync(tt => tt.Id == ticketType.Id);
        Assert.Equal(150, persisted.Quantity);
    }

    [Fact]
    public async Task AddTicketStockAsync_RecomputesAvailable_DeductingSoldTickets()
    {
        // Arrange — Quantity=10, 3 already sold → available should be 15-3=12 (ATS-006)
        var (eventEntity, ticketType, _) = await CreateTestEventWithTicketType(10);
        await AddSoldTicketsAsync(eventEntity.Id, ticketType.Id, 3);

        // Act
        var result = await _eventService.AddTicketStockAsync(eventEntity.Id, ticketType.Id, 5);

        // Assert — availability recomputed from the new quantity
        Assert.Equal(15, result.Quantity);
        Assert.Equal(12, result.Available);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task AddTicketStockAsync_InvalidQuantity_ThrowsArgumentException_AndLeavesQuantityUnchanged(int quantity)
    {
        // Arrange (ATS-002 invalid additionalQuantity: 0, negative, above 1000)
        var (eventEntity, ticketType, _) = await CreateTestEventWithTicketType(100);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.AddTicketStockAsync(eventEntity.Id, ticketType.Id, quantity));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));

        // Quantity unchanged
        var persisted = await _context.TicketTypes.SingleAsync(tt => tt.Id == ticketType.Id);
        Assert.Equal(100, persisted.Quantity);
    }

    [Fact]
    public async Task AddTicketStockAsync_MismatchedTicketTypeEvent_ThrowsKeyNotFoundException()
    {
        // Arrange — ticket type belongs to event A, call with event B (ATS-002 mismatch)
        var (eventA, ticketType, _) = await CreateTestEventWithTicketType(100);
        var eventB = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Other Event",
            Description = "desc",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "loc",
            OrganizerId = eventA.OrganizerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventB);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.AddTicketStockAsync(eventB.Id, ticketType.Id, 50));

        // Quantity unchanged
        var persisted = await _context.TicketTypes.SingleAsync(tt => tt.Id == ticketType.Id);
        Assert.Equal(100, persisted.Quantity);
    }

    [Fact]
    public async Task AddTicketStockAsync_UnknownTicketType_ThrowsKeyNotFoundException()
    {
        // Arrange
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.AddTicketStockAsync(eventEntity.Id, Guid.NewGuid(), 50));
    }

    #endregion

    #region ATS-003: Concurrent increment serialization

    [Fact]
    public async Task ConcurrentIncrementAndReservation_Serialize_NoLostUpdateNoOversell()
    {
        // Arrange — shared-cache SQLite so concurrent connections see the same row (ATS-003, D-1)
        var sharedConnString = $"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared";

        using var seedConnection = new SqliteConnection(sharedConnString);
        seedConnection.Open();

        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(seedConnection)
            .Options;
        using var seedContext = new EventServiceTicketStockTestDbContext(seedOptions);
        seedContext.Database.EnsureCreated();

        var seedUser = new User
        {
            Id = Guid.NewGuid(),
            Name = "Seed Organizer",
            Email = $"seed-{Guid.NewGuid():N}@test.com",
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
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        seedContext.TicketTypes.Add(seedTicketType);
        await seedContext.SaveChangesAsync();

        var eventId = seedEvent.Id;
        var ticketTypeId = seedTicketType.Id;
        var reservationServiceOptions = Options.Create(new ReservationTokenOptions
        {
            TokenSecretKey = "test-reservation-token-secret-key-minimum-32-characters"
        });

        // Act — concurrent increment (+5) and reservation (qty 8) against the same row
        var incrementTask = Task.Run(async () =>
        {
            using var concurrentConnection = new SqliteConnection(sharedConnString);
            concurrentConnection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(concurrentConnection)
                .Options;
            using var concurrentContext = new EventServiceTicketStockTestDbContext(options);
            var configData = new Dictionary<string, string?>
            {
                { "CloudflareR2:BucketName", "test-bucket" },
                { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var service = new EventService(
                concurrentContext,
                new TestLogger<EventService>(),
                configuration,
                new Mock<IAmazonS3>().Object);
            await service.AddTicketStockAsync(eventId, ticketTypeId, 5);
        });

        var reservationTask = Task.Run(async () =>
        {
            using var concurrentConnection = new SqliteConnection(sharedConnString);
            concurrentConnection.Open();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(concurrentConnection)
                .Options;
            using var concurrentContext = new EventServiceTicketStockTestDbContext(options);
            var service = new ReservationService(
                concurrentContext,
                new TestLogger<ReservationService>(),
                reservationServiceOptions);
            await service.CreateReservationAsync(null, eventId, ticketTypeId, 8, "12345678");
        });

        await Task.WhenAll(incrementTask, reservationTask);

        // Assert — no lost update: quantity = 10 + 5 = 15 (not 10)
        // AsNoTracking: the seed context still tracks the original entity, so read fresh DB state.
        var finalTicketType = await seedContext.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == ticketTypeId);
        Assert.Equal(15, finalTicketType.Quantity);

        // Assert — no oversell: reservation of 8 persisted against available stock
        var reservation = await seedContext.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.TicketTypeId == ticketTypeId);
        Assert.NotNull(reservation);
        Assert.Equal(8, reservation!.Quantity);
    }

    #endregion

    #region ATS-004: Create new ticket type

    [Fact]
    public async Task AddTicketTypeAsync_WithValidData_CreatesNewTicketType()
    {
        // Arrange (ATS-004 happy path)
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);

        // Act
        var result = await _eventService.AddTicketTypeAsync(eventEntity.Id, "VIP", 150m, 20);

        // Assert — returned shape { id, name, price, quantity, available }
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("VIP", result.Name);
        Assert.Equal(150m, result.Price);
        Assert.Equal(20, result.Quantity);
        Assert.Equal(20, result.Available);

        // Persisted and visible through the buyer catalog (ATS-004 "appears in buyer catalog")
        var persisted = await _context.TicketTypes.SingleAsync(tt => tt.Id == result.Id);
        Assert.Equal(eventEntity.Id, persisted.EventId);
        Assert.Equal("VIP", persisted.Name);
        Assert.Equal(20, persisted.Quantity);

        var catalog = await _eventService.GetEventByIdAsync(eventEntity.Id);
        Assert.NotNull(catalog);
        Assert.Contains(catalog!.TicketTypes, tt => tt.Id == result.Id && tt.Name == "VIP" && tt.Available == 20);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddTicketTypeAsync_EmptyName_ThrowsArgumentException_AndCreatesNoRow(string name)
    {
        // Arrange (ATS-004 invalid payload)
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);
        var before = await _context.TicketTypes.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.AddTicketTypeAsync(eventEntity.Id, name, 150m, 20));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    [Fact]
    public async Task AddTicketTypeAsync_NameTooLong_ThrowsArgumentException_AndCreatesNoRow()
    {
        // Arrange — name of 101 chars exceeds the 100-char cap
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);
        var longName = new string('N', 101);
        var before = await _context.TicketTypes.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.AddTicketTypeAsync(eventEntity.Id, longName, 150m, 20));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task AddTicketTypeAsync_NegativePrice_ThrowsArgumentException_AndCreatesNoRow(decimal price)
    {
        // Arrange (ATS-004 invalid payload)
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);
        var before = await _context.TicketTypes.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.AddTicketTypeAsync(eventEntity.Id, "VIP", price, 20));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public async Task AddTicketTypeAsync_InvalidQuantity_ThrowsArgumentException_AndCreatesNoRow(int quantity)
    {
        // Arrange (ATS-004 invalid payload)
        var (eventEntity, _, _) = await CreateTestEventWithTicketType(100);
        var before = await _context.TicketTypes.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.AddTicketTypeAsync(eventEntity.Id, "VIP", 150m, quantity));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    [Fact]
    public async Task AddTicketTypeAsync_UnknownEvent_ThrowsKeyNotFoundException_AndCreatesNoRow()
    {
        // Arrange (ATS-004 event existence)
        await CreateTestEventWithTicketType(100);
        var before = await _context.TicketTypes.CountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.AddTicketTypeAsync(Guid.NewGuid(), "VIP", 150m, 20));

        Assert.Equal(before, await _context.TicketTypes.CountAsync());
    }

    #endregion
}
