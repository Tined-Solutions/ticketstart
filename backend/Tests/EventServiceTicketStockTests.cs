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
/// ATE-001 fault-injection context: persists within the ambient transaction, then
/// throws BEFORE the service can commit, exercising the replacement rollback path.
/// </summary>
internal class FaultingReplaceTicketTypesDbContext : EventServiceTicketStockTestDbContext
{
    public FaultingReplaceTicketTypesDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await base.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("Injected failure after SaveChangesAsync");
    }
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
            new Mock<IR2StorageClient>().Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));
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

    private async Task<Reservation> AddReservationAsync(Guid eventId, Guid ticketTypeId, int quantity,
        ReservationStatus status = ReservationStatus.Active, DateTime? expiresAt = null)
    {
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = quantity,
            PurchaserDNI = "12345678",
            PurchaserEmail = "reservation@test.com",
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        return reservation;
    }

    private async Task<(Event Event, TicketType TypeA, TicketType TypeB)> CreateEventWithTwoTicketTypes(
        EventStatus status = EventStatus.Pending)
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
            Name = "Replace Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = status
        };
        _context.Events.Add(eventEntity);

        var typeA = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "A",
            Price = 50.00m,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        var typeB = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "B",
            Price = 80.00m,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.AddRange(typeA, typeB);

        await _context.SaveChangesAsync();
        return (eventEntity, typeA, typeB);
    }

    private static ReplaceTicketTypesRequest ReplaceRequest(params ReplaceTicketTypeRequest[] items)
        => new() { TicketTypes = items.ToList() };

    private static EventService BuildService(ApplicationDbContext context)
    {
        var configurationData = new Dictionary<string, string?>
        {
            { "CloudflareR2:BucketName", "test-bucket" },
            { "CloudflareR2:PublicUrl", "https://test.r2.dev" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        return new EventService(
            context,
            new TestLogger<EventService>(),
            configuration,
            new Mock<IR2StorageClient>().Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System,
            Options.Create(new HideExpiredEventsOptions()));
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
                new Mock<IR2StorageClient>().Object, new Mock<IEventNotificationQueue>().Object, TimeProvider.System,
                Options.Create(new HideExpiredEventsOptions()));
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
                reservationServiceOptions,
                TimeProvider.System,
                Options.Create(new HideExpiredEventsOptions()));
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

    #region Refunded tickets excluded from availability (APR-005)

    [Fact]
    public async Task GetEventByIdAsync_RefundedTickets_DoNotCountAsSold()
    {
        // Arrange — capacity 10, 3 sold tickets of which 1 refunded (APR-005)
        var (eventEntity, ticketType, _) = await CreateTestEventWithTicketType(10);
        await AddSoldTicketsAsync(eventEntity.Id, ticketType.Id, 2);

        var refunded = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            PurchaserEmail = "refunded@test.com",
            PurchaserDNI = "12345678",
            QRCodeData = $"qr-{Guid.NewGuid():N}",
            IsUsed = false,
            IsRefunded = true,
            RefundedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow
        };
        _context.Tickets.Add(refunded);
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert — sold = 2 (refunded excluded) → available = 10 - 2 = 8
        var tt = Assert.Single(result!.TicketTypes);
        Assert.Equal(8, tt.Available);
    }

    #endregion

    #region ATE-001…ATE-006: ReplaceTicketTypesAsync (atomic full replacement)

    [Fact]
    public async Task ReplaceTicketTypesAsync_MixedAddEditDelete_ReplacesCompleteListAndRecomputesAvailability()
    {
        // Arrange — Pending event with types A and B (ATE-001 mixed add/edit/delete)
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();

        // Act — rename/re-price A, insert C, omit B
        var result = await _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
            new ReplaceTicketTypeRequest(typeA.Id, "A Prime", 75m, 120),
            new ReplaceTicketTypeRequest(null, "C", 200m, 10)));

        // Assert — A updated, C created, B deleted
        Assert.Equal(2, result.Count);
        var updatedA = result.Single(tt => tt.Id == typeA.Id);
        Assert.Equal("A Prime", updatedA.Name);
        Assert.Equal(75m, updatedA.Price);
        Assert.Equal(120, updatedA.Quantity);
        Assert.Equal(120, updatedA.Available);

        var createdC = result.Single(tt => tt.Name == "C");
        Assert.NotEqual(Guid.Empty, createdC.Id);
        Assert.Equal(10, createdC.Available);

        var persistedTypes = await _context.TicketTypes.AsNoTracking()
            .Where(tt => tt.EventId == eventEntity.Id).ToListAsync();
        Assert.Equal(2, persistedTypes.Count);
        Assert.DoesNotContain(persistedTypes, tt => tt.Id == typeB.Id);
        Assert.Equal("A Prime", persistedTypes.Single(tt => tt.Id == typeA.Id).Name);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_RejectedWithoutHistory_Succeeds()
    {
        // ATE-003 non-approved without history is editable (Rejected variant)
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes(EventStatus.Rejected);

        var result = await _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
            new ReplaceTicketTypeRequest(typeA.Id, "A2", 60m, 30)));

        Assert.Equal(30, result.Single().Quantity);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_EmptyList_ThrowsArgumentException_NoMutation()
    {
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, new ReplaceTicketTypesRequest()));

        Assert.True(await _context.TicketTypes.AsNoTracking().AnyAsync(tt => tt.Id == typeA.Id));
        Assert.True(await _context.TicketTypes.AsNoTracking().AnyAsync(tt => tt.Id == typeB.Id));
    }

    [Theory]
    [InlineData("", 50, 10)]
    [InlineData("   ", 50, 10)]
    [InlineData("A", -1, 10)]
    [InlineData("A", 50, 0)]
    [InlineData("A", 50, -1)]
    [InlineData("A", 50, 1001)]
    public async Task ReplaceTicketTypesAsync_InvalidPayload_ThrowsArgumentException_NoMutation(
        string name, decimal price, int quantity)
    {
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, name, price, quantity))));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == typeA.Id);
        Assert.Equal("A", persisted.Name);
        Assert.Equal(100, persisted.Quantity);
        Assert.True(await _context.TicketTypes.AsNoTracking().AnyAsync(tt => tt.Id == typeB.Id));
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_NameTooLong_ThrowsArgumentException_NoMutation()
    {
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes();
        var longName = new string('N', 101);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, longName, 50m, 10))));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == typeA.Id);
        Assert.Equal("A", persisted.Name);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_LegacyTotalAboveCap_ThrowsArgumentException()
    {
        // ATE-004: an existing total accumulated above 1000 by add-stock is explicitly rejected
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes();
        typeA.Quantity = 1500;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A", 50m, 1500))));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == typeA.Id);
        Assert.Equal(1500, persisted.Quantity);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_PriceZero_IsAccepted()
    {
        // ATE-004: price >= 0 parity with CreateEventAsync
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes();

        var result = await _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
            new ReplaceTicketTypeRequest(typeA.Id, "Free", 0m, 10)));

        Assert.Equal(0m, result.Single().Price);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_ForeignId_ThrowsArgumentException_NoMutation()
    {
        // ATE-005: an id belonging to another event is rejected
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();
        var (_, foreignType, _) = await CreateEventWithTwoTicketTypes();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(foreignType.Id, "X", 10m, 5))));

        var names = await _context.TicketTypes.AsNoTracking()
            .Where(tt => tt.EventId == eventEntity.Id).Select(tt => tt.Name).ToListAsync();
        Assert.Equal(new[] { "A", "B" }, names.OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_UnknownEvent_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _eventService.ReplaceTicketTypesAsync(Guid.NewGuid(), ReplaceRequest(
                new ReplaceTicketTypeRequest(null, "A", 10m, 5))));
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_ApprovedEvent_ThrowsTicketTypesNotEditable_NoMutation()
    {
        // ATE-002: Approved fails eligibility even without history
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes(EventStatus.Approved);

        await Assert.ThrowsAsync<TicketTypesNotEditableException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A2", 50m, 100))));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == typeA.Id);
        Assert.Equal("A", persisted.Name);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_PendingWithTicketHistory_ThrowsTicketTypesReferenced_NoMutation()
    {
        // ATE-003: Pending with history is blocked (Approved → Pending is reachable)
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();
        await AddSoldTicketsAsync(eventEntity.Id, typeA.Id, 1);

        await Assert.ThrowsAsync<TicketTypesReferencedException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A2", 50m, 100))));

        Assert.True(await _context.TicketTypes.AsNoTracking().AnyAsync(tt => tt.Id == typeA.Id));
        Assert.True(await _context.TicketTypes.AsNoTracking().AnyAsync(tt => tt.Id == typeB.Id));
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_PendingWithReservationHistory_ThrowsTicketTypesReferenced()
    {
        // ATE-003: ANY Reservation row counts as history, independent of status
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes();
        await AddReservationAsync(eventEntity.Id, typeA.Id, 2);

        await Assert.ThrowsAsync<TicketTypesReferencedException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A2", 50m, 100))));
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_RejectedWithRefundedTicket_ThrowsTicketTypesReferenced()
    {
        // ATE-003: refunded tickets still protect the type (Restrict FK)
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes(EventStatus.Rejected);
        await AddSoldTicketsAsync(eventEntity.Id, typeA.Id, 1);

        var ticket = await _context.Tickets.FirstAsync(t => t.TicketTypeId == typeA.Id);
        ticket.IsRefunded = true;
        ticket.RefundedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<TicketTypesReferencedException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A2", 50m, 100))));
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_PastApprovedEvent_ThrowsEventFinalized_NotNotEditable()
    {
        // ATE-002/PEM-002: the finalized guard runs BEFORE eligibility
        var (eventEntity, typeA, _) = await CreateEventWithTwoTicketTypes(EventStatus.Approved);
        eventEntity.Date = DateTime.UtcNow.AddDays(-2);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<EventFinalizedException>(() =>
            _eventService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A2", 50m, 100))));

        var persisted = await _context.TicketTypes.AsNoTracking().SingleAsync(tt => tt.Id == typeA.Id);
        Assert.Equal("A", persisted.Name);
    }

    [Fact]
    public async Task ReplaceTicketTypesAsync_SaveFailure_RollsBackEntireReplacement()
    {
        // ATE-001: a mid-transaction failure leaves the prior list unchanged
        var (eventEntity, typeA, typeB) = await CreateEventWithTwoTicketTypes();

        var faultOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var faultContext = new FaultingReplaceTicketTypesDbContext(faultOptions);
        var faultingService = BuildService(faultContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            faultingService.ReplaceTicketTypesAsync(eventEntity.Id, ReplaceRequest(
                new ReplaceTicketTypeRequest(typeA.Id, "A Prime", 75m, 120),
                new ReplaceTicketTypeRequest(null, "C", 200m, 10))));

        _context.ChangeTracker.Clear();
        var persistedTypes = await _context.TicketTypes.AsNoTracking()
            .Where(tt => tt.EventId == eventEntity.Id).ToListAsync();
        Assert.Equal(2, persistedTypes.Count);
        Assert.Equal("A", persistedTypes.Single(tt => tt.Id == typeA.Id).Name);
        Assert.Equal(100, persistedTypes.Single(tt => tt.Id == typeA.Id).Quantity);
        Assert.True(persistedTypes.Any(tt => tt.Id == typeB.Id));
    }

    #endregion
}
