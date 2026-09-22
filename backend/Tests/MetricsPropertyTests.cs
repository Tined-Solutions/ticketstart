using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using ArbStatic = FsCheck.Fluent.Arb;
using GenStatic = FsCheck.Fluent.Gen;
using PropStatic = FsCheck.Fluent.Prop;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for organizer dashboard metrics.
/// Validates Requirements 11.2, 11.3, 11.4, 11.5, 11.6 and APR-017
/// (money-based revenue = Σ charged transactions − Σ recorded refunds).
/// </summary>
public class MetricsPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMetricsService _metricsService;

    public MetricsPropertyTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MetricsService>();

        _metricsService = new MetricsService(_context, logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Property 33: Dashboard Displays Owner's Events Only

    /// <summary>
    /// Property 33: Dashboard Displays Owner's Events Only
    /// For any organizador viewing the dashboard, only events owned by that organizador SHALL be displayed.
    /// **Validates: Requirements 11.2**
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetrics_ReturnsOnlyOwnersEvents()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        var owner = new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        var otherOrganizer = new User
        {
            Id = otherOrganizerId,
            Email = "other@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(owner, otherOrganizer);

        var ownerEvent1 = CreateEvent(ownerId, "Owner Event 1");
        var ownerEvent2 = CreateEvent(ownerId, "Owner Event 2");
        var otherEvent = CreateEvent(otherOrganizerId, "Other Event");

        _context.Events.AddRange(ownerEvent1, ownerEvent2, otherEvent);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetOrganizerMetricsAsync(ownerId);

        // Assert
        Assert.NotNull(metrics);
        var metricsList = metrics.ToList();
        Assert.Equal(2, metricsList.Count);
        Assert.Contains(metricsList, m => m.EventId == ownerEvent1.Id);
        Assert.Contains(metricsList, m => m.EventId == ownerEvent2.Id);
        Assert.DoesNotContain(metricsList, m => m.EventId == otherEvent.Id);
    }

    /// <summary>
    /// Property 33 (Edge Case): Organizer with no events receives empty metrics.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetrics_NoEvents_ReturnsEmpty()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var owner = new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(owner);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetOrganizerMetricsAsync(ownerId);

        // Assert
        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    #endregion

    #region Property 34: Tickets Sold Calculation Correctness

    /// <summary>
    /// Property 34: Tickets Sold Calculation Correctness
    /// For any event, the displayed tickets sold count SHALL equal the number of confirmed tickets in the database for that event.
    /// **Validates: Requirements 11.3**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TicketsSold_MatchesTicketCount()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Ticket Sales Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var scenarios = new[] { 0, 1, 5, 50 };
        foreach (var soldCount in scenarios)
        {
            // Clean previous tickets for this event to isolate the scenario
            var existingTickets = _context.Tickets.Where(t => t.EventId == eventEntity.Id).ToList();
            _context.Tickets.RemoveRange(existingTickets);
            await _context.SaveChangesAsync();

            for (int i = 0; i < soldCount; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    TicketTypeId = ticketType.Id,
                    PurchaserEmail = $"buyer{i}@example.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(soldCount, metrics.TicketsSold);
        }
    }

    #endregion

    #region Property 35: Revenue Calculation Correctness

    /// <summary>
    /// Property 35 (APR-017): Revenue Calculation Correctness
    /// For any event with no recorded refunds, the displayed total revenue SHALL equal
    /// the sum of the charged transaction amounts (Approved ∪ Refunded) of its
    /// confirmed reservations — the money-based formula, NOT TicketType.Price × tickets.
    /// **Validates: Requirements 11.4, APR-017**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TotalRevenue_MatchesSumOfChargedAmounts()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Revenue Event");
        var vipType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "VIP",
            Price = 200,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };
        var generalType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(vipType);
        eventEntity.TicketTypes.Add(generalType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Two confirmed purchases, no refunds: 5 VIP at 200 and 12 General at 100.
        SeedConfirmedPurchase(_context, eventEntity.Id, vipType.Id, quantity: 5, unitPrice: 200m);
        SeedConfirmedPurchase(_context, eventEntity.Id, generalType.Id, quantity: 12, unitPrice: 100m);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert — with no refunds, charged amounts equal the historical price sum
        Assert.NotNull(metrics);
        var expectedRevenue = (5 * vipType.Price) + (12 * generalType.Price);
        Assert.Equal(expectedRevenue, metrics.TotalRevenue);
        Assert.Equal(17, metrics.TicketsSold);
    }

    /// <summary>
    /// Property 35 (Edge Case): Event with no sold tickets has zero revenue.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_NoTicketsSold_RevenueIsZero()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "No Sales Event");
        eventEntity.TicketTypes.Add(new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 50,
            CreatedAt = DateTime.UtcNow
        });
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TicketsSold);
        Assert.Equal(0m, metrics.TotalRevenue);
    }

    #endregion

    #region Property 36: Remaining Inventory Calculation Correctness

    /// <summary>
    /// Property 36: Remaining Inventory Calculation Correctness
    /// For any event, the displayed remaining inventory SHALL equal the total ticket type quantities minus confirmed tickets sold minus active reservations.
    /// **Validates: Requirements 11.5**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_RemainingInventory_CalculationIsCorrect()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Inventory Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Sell 10 tickets
        for (int i = 0; i < 10; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                PurchaserEmail = $"buyer{i}@example.com",
                PurchaserDNI = $"DNI{i}",
                QRCodeData = $"QR-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Create 15 active reservations
        for (int i = 0; i < 15; i++)
        {
            _context.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = ticketType.Id,
                Quantity = 1,
                PurchaserDNI = $"RES{i}",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        var expectedRemainingInventory = ticketType.Quantity - 10 - 15;
        Assert.Equal(expectedRemainingInventory, metrics.RemainingInventory);
    }

    /// <summary>
    /// Property 36 (Edge Case): Expired reservations do not reduce remaining inventory.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_ExpiredReservations_DoNotReduceInventory()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Expired Reservations Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Add an expired active reservation (status Active but past expiration)
        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = ticketType.Id,
            Quantity = 30,
            PurchaserDNI = "EXPIRED",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15)
        });
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(ticketType.Quantity, metrics.RemainingInventory);
    }

    #endregion

    #region Property 37: Scanned Tickets Count Correctness

    /// <summary>
    /// Property 37: Scanned Tickets Count Correctness
    /// For any event, the displayed scanned tickets count SHALL equal the number of tickets marked as used (IsUsed = true).
    /// **Validates: Requirements 11.6**
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_TicketsScanned_MatchesUsedTickets()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Scan Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var scenarios = new[] { 0, 1, 5, 20 };
        foreach (var usedCount in scenarios)
        {
            // Clean previous tickets
            var existingTickets = _context.Tickets.Where(t => t.EventId == eventEntity.Id).ToList();
            _context.Tickets.RemoveRange(existingTickets);
            await _context.SaveChangesAsync();

            // Create 30 tickets, mark `usedCount` as used
            for (int i = 0; i < 30; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    TicketTypeId = ticketType.Id,
                    PurchaserEmail = $"buyer{i}@example.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = i < usedCount,
                    UsedAt = i < usedCount ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            // Act
            var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(usedCount, metrics.TicketsScanned);
        }
    }

    /// <summary>
    /// Property 36 (Multiple Ticket Types): Remaining inventory accounts for all ticket types.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_RemainingInventory_WorksForMultipleTicketTypes()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "organizer@example.com",
            PasswordHash = "dummy-hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var eventEntity = CreateEvent(organizerId, "Multi-Type Inventory Event");
        var vipType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "VIP",
            Price = 200,
            Quantity = 20,
            CreatedAt = DateTime.UtcNow
        };
        var generalType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100,
            Quantity = 80,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(vipType);
        eventEntity.TicketTypes.Add(generalType);
        _context.Events.Add(eventEntity);

        // Sell 5 VIP and 10 General
        for (int i = 0; i < 5; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = vipType.Id,
                PurchaserEmail = $"vip{i}@example.com",
                PurchaserDNI = $"VIP{i}",
                QRCodeData = $"QR-VIP-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        for (int i = 0; i < 10; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = generalType.Id,
                PurchaserEmail = $"general{i}@example.com",
                PurchaserDNI = $"GEN{i}",
                QRCodeData = $"QR-GEN-{Guid.NewGuid()}",
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Active reservation for 3 General tickets
        _context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = generalType.Id,
            Quantity = 3,
            PurchaserDNI = "RESERVED",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Status = ReservationStatus.Active,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        var expectedRemaining = (vipType.Quantity + generalType.Quantity) - 5 - 10 - 3;
        Assert.Equal(expectedRemaining, metrics.RemainingInventory);
    }

    #endregion

    #region Event Not Found

    [Fact]
    public async Task GetEventMetrics_NonExistentEvent_ReturnsNull()
    {
        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(Guid.NewGuid());

        // Assert
        Assert.Null(metrics);
    }

    #endregion

    #region EA-007 — EventMetrics carries Status

    [Fact]
    public async Task GetOrganizerMetrics_EachEventCarriesItsStatus()
    {
        // EA-007: the projection copies Status so the dashboard can render badges.
        var organizerId = Guid.NewGuid();
        var pending = CreateEvent(organizerId, "Pending Own");
        pending.Status = EventStatus.Pending;
        var approved = CreateEvent(organizerId, "Approved Own");
        approved.Status = EventStatus.Approved;
        var rejected = CreateEvent(organizerId, "Rejected Own");
        rejected.Status = EventStatus.Rejected;
        _context.Events.AddRange(pending, approved, rejected);
        await _context.SaveChangesAsync();

        // Act
        var metrics = (await _metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert
        Assert.Equal(3, metrics.Count);
        Assert.Equal(EventStatus.Pending, metrics.Single(m => m.EventId == pending.Id).Status);
        Assert.Equal(EventStatus.Approved, metrics.Single(m => m.EventId == approved.Id).Status);
        Assert.Equal(EventStatus.Rejected, metrics.Single(m => m.EventId == rejected.Id).Status);
    }

    [Fact]
    public async Task GetEventMetrics_SingleEvent_ReturnsItsStatus()
    {
        // EA-007: single-event metrics carry Status too.
        var organizerId = Guid.NewGuid();
        var eventEntity = CreateEvent(organizerId, "Rejected Single");
        eventEntity.Status = EventStatus.Rejected;
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(EventStatus.Rejected, metrics.Status);
    }

    #endregion

    private static Event CreateEvent(Guid organizerId, string name)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test event for metrics",
            Date = now.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    #region Refunded tickets excluded (APR-005) + money-based revenue (APR-017)

    /// <summary>
    /// APR-005 + APR-017: refunded tickets stop counting as sold; revenue is
    /// charged (250) − recorded refunds (50) = 200, NOT price × non-refunded tickets.
    /// </summary>
    [Fact]
    public async Task GetEventMetrics_RefundedTickets_ExcludedFromSoldAndRevenue()
    {
        // Arrange — one confirmed purchase of 5 tickets (250 charged), 2 used,
        // 1 refunded with a 50 Refunds ledger row.
        var organizerId = Guid.NewGuid();
        var eventEntity = CreateEvent(organizerId, "Refunded Event");
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 50,
            Quantity = 100,
            CreatedAt = DateTime.UtcNow
        };
        eventEntity.TicketTypes.Add(ticketType);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        SeedConfirmedPurchase(_context, eventEntity.Id, ticketType.Id, quantity: 5,
            unitPrice: 50m, usedTickets: 2, refundedTickets: 1, refundAmount: 50m);
        await _context.SaveChangesAsync();

        // Act
        var metrics = await _metricsService.GetEventMetricsAsync(eventEntity.Id);

        // Assert — sold = 4 (refunded excluded), revenue = 250 − 50, scanned = 2
        Assert.NotNull(metrics);
        Assert.Equal(4, metrics.TicketsSold);
        Assert.Equal(200m, metrics.TotalRevenue);
        Assert.Equal(2, metrics.TicketsScanned);
        // 100 inventory − 4 sold − 0 reservations = 96 remaining
        Assert.Equal(96, metrics.RemainingInventory);
    }

    #endregion

    #region APR-017 property — revenue = charged − Σ refunds, always ≥ 0

    private const decimal PropertyUnitPrice = 100m;
    private const int PropertyUnitPriceCents = 10000;   // PropertyUnitPrice × 100

    /// <summary>
    /// Arbitrary VALID single-op refund: N ∈ [1,4] tickets at 100, K ∈ [1,N], amount
    /// in integer cents ∈ [1, 100 × K] → 0 &lt; amount ≤ unit price × K (≤ 2 decimals).
    /// </summary>
    private static Gen<(int N, int K, decimal Amount)> ValidRefundGen() =>
        from n in GenStatic.Choose(1, 4)
        from k in GenStatic.Choose(1, n)
        from amountCents in GenStatic.Choose(1, PropertyUnitPriceCents * k)
        select (n, k, amountCents / 100m);

    /// <summary>
    /// APR-017: for arbitrary valid (K, amount) refunds, both the single-event and the
    /// organizer paths MUST return revenue == charged (N × unit price) − Σ refunds and
    /// revenue ≥ 0. The transaction flips to Refunded when K == N (D2), so the
    /// full-refund case also proves the flipped row is counted exactly once.
    /// </summary>
    [Property]
    public Property GetMetrics_RevenueEqualsChargedMinusRefunds_ForArbitraryValidRefunds()
    {
        return PropStatic.ForAll(ArbStatic.From(ValidRefundGen()), scenario =>
        {
            var (n, k, amount) = scenario;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            using var context = new ApplicationDbContext(options);
            try
            {
                var organizerId = Guid.NewGuid();
                var eventEntity = CreateEvent(organizerId, "Property Refund Event");
                context.Events.Add(eventEntity);

                var ticketType = new TicketType
                {
                    Id = Guid.NewGuid(),
                    EventId = eventEntity.Id,
                    Name = "General",
                    Price = PropertyUnitPrice,
                    Quantity = 100,
                    CreatedAt = DateTime.UtcNow
                };
                context.TicketTypes.Add(ticketType);

                SeedConfirmedPurchase(context, eventEntity.Id, ticketType.Id, quantity: n,
                    unitPrice: PropertyUnitPrice, refundedTickets: k, refundAmount: amount);
                context.SaveChanges();

                var service = new MetricsService(context, NullLogger<MetricsService>.Instance);
                var single = service.GetEventMetricsAsync(eventEntity.Id).GetAwaiter().GetResult();
                var organizer = service.GetOrganizerMetricsAsync(organizerId).GetAwaiter().GetResult().Single();

                var expected = n * PropertyUnitPrice - amount;

                return single != null
                    && single.TotalRevenue == expected
                    && single.TotalRevenue >= 0m
                    && organizer.TotalRevenue == expected
                    && organizer.TotalRevenue >= 0m;
            }
            finally
            {
                context.Database.EnsureDeleted();
            }
        });
    }

    #endregion

    #region APR-017 fixture helper

    /// <summary>
    /// Seeds one Confirmed reservation with its tickets and a single transaction.
    /// The transaction flips to Refunded only when every ticket is refunded (D2),
    /// mirroring AdminPurchaseService. Callers persist with SaveChanges.
    /// </summary>
    private static void SeedConfirmedPurchase(
        ApplicationDbContext context,
        Guid eventId,
        Guid ticketTypeId,
        int quantity,
        decimal unitPrice,
        int usedTickets = 0,
        int refundedTickets = 0,
        decimal refundAmount = 0m)
    {
        var now = DateTime.UtcNow;
        var reservationId = Guid.NewGuid();

        context.Reservations.Add(new Reservation
        {
            Id = reservationId,
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = quantity,
            PurchaserDNI = "31234561",
            PurchaserEmail = "buyer@example.com",
            ExpiresAt = now.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = now.AddDays(-5)
        });

        var refundedTicketIds = new List<Guid>();
        for (var i = 0; i < quantity; i++)
        {
            var ticketId = Guid.NewGuid();
            // Refunded tickets are the LAST K and used tickets the FIRST U, so a
            // ticket is never both used and refunded (the refund flow blocks that).
            var isRefunded = i >= quantity - refundedTickets;
            if (isRefunded)
            {
                refundedTicketIds.Add(ticketId);
            }

            context.Tickets.Add(new Ticket
            {
                Id = ticketId,
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                ReservationId = reservationId,
                PurchaserEmail = "buyer@example.com",
                PurchaserDNI = "31234561",
                QRCodeData = $"qr-{Guid.NewGuid():N}",
                IsUsed = i < usedTickets,
                UsedAt = i < usedTickets ? now : null,
                IsRefunded = isRefunded,
                RefundedAt = isRefunded ? now : null,
                CreatedAt = now.AddSeconds(i)
            });
        }

        context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            MercadoPagoId = $"mp-{Guid.NewGuid():N}",
            Amount = unitPrice * quantity,
            Status = refundedTickets >= quantity ? TransactionStatus.Refunded : TransactionStatus.Approved,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now.AddDays(-5)
        });

        if (refundedTickets > 0 && refundAmount > 0m)
        {
            context.Refunds.Add(new Refund
            {
                Id = Guid.NewGuid(),
                ReservationId = reservationId,
                TicketIds = refundedTicketIds.ToArray(),
                Quantity = refundedTickets,
                Amount = refundAmount,
                AdminId = Guid.NewGuid(),
                CreatedAt = now
            });
        }
    }

    #endregion
}
