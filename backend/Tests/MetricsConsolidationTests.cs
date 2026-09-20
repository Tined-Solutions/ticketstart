using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// B7.2 RED + B7.3 GREEN: MetricsService consolidation tests.
/// Validates that GetOrganizerMetricsAsync returns correct metrics and
/// uses a consolidated query approach (not per-event N+1 loops).
/// </summary>
public class MetricsConsolidationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MetricsService> _logger;

    public MetricsConsolidationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new ApplicationDbContext(options);
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MetricsService>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// B7.2 RED: Verifies GetOrganizerMetricsAsync correctly aggregates metrics
    /// for multiple events owned by the organizer.
    /// The consolidated GroupBy implementation (B7.3) must produce identical results.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_ReturnsCorrectAggregatesForAllEvents()
    {
        // Arrange: seed organizer with 3 events, each with tickets and reservations
        var organizerId = Guid.NewGuid();
        var organizer = new User
        {
            Id = organizerId,
            Email = "org@example.com",
            PasswordHash = "hash",
            Role = UserRole.Organizador,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(organizer);

        var event1 = CreateEvent(organizerId, "Event 1");
        var event2 = CreateEvent(organizerId, "Event 2");
        var event3 = CreateEvent(organizerId, "Event 3");

        _context.Events.AddRange(event1, event2, event3);

        foreach (var evt in new[] { event1, event2, event3 })
        {
            var tt = new TicketType
            {
                Id = Guid.NewGuid(),
                EventId = evt.Id,
                Name = "General",
                Price = 50,
                Quantity = 100,
                CreatedAt = DateTime.UtcNow
            };
            _context.TicketTypes.Add(tt);

            // APR-017: revenue is money-based. A confirmed purchase with an Approved
            // transaction of 5 × 50 supplies the charged amount for this event.
            var confirmedReservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = organizerId,
                EventId = evt.Id,
                TicketTypeId = tt.Id,
                Quantity = 5,
                PurchaserDNI = "CONFIRMED",
                PurchaserEmail = "confirmed@test.com",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _context.Reservations.Add(confirmedReservation);
            _context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                ReservationId = confirmedReservation.Id,
                MercadoPagoId = $"mp-{Guid.NewGuid():N}",
                Amount = 250m,
                Status = TransactionStatus.Approved,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            });

            // Seed 5 tickets per event, 2 used per event
            for (int i = 0; i < 5; i++)
            {
                _context.Tickets.Add(new Ticket
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.Id,
                    TicketTypeId = tt.Id,
                    ReservationId = confirmedReservation.Id,
                    PurchaserEmail = $"buyer{i}@test.com",
                    PurchaserDNI = $"DNI{i}",
                    QRCodeData = $"QR-{Guid.NewGuid()}",
                    IsUsed = i < 2,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Seed 2 active reservations per event, quantity 1 each
            for (int i = 0; i < 2; i++)
            {
                _context.Reservations.Add(new Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = organizerId,
                    EventId = evt.Id,
                    TicketTypeId = tt.Id,
                    Quantity = 1,
                    PurchaserDNI = $"RES{i}",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Status = ReservationStatus.Active,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        // Act
        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert: 3 events returned, each with correct metrics
        Assert.Equal(3, metrics.Count);

        foreach (var metric in metrics)
        {
            Assert.Equal(5, metric.TicketsSold);
            Assert.Equal(250, metric.TotalRevenue); // 5 tickets × 50 price
            Assert.Equal(2, metric.TicketsScanned);
            // 100 total inventory - 5 sold - 2 active reservations = 93 remaining
            Assert.Equal(93, metric.RemainingInventory);
        }

        // Each event has a distinct ID
        var eventIds = metrics.Select(m => m.EventId).Distinct().ToList();
        Assert.Equal(3, eventIds.Count);
    }

    /// <summary>
    /// B7.2: Verifies organizer with no events returns empty.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_NoEvents_ReturnsEmpty()
    {
        var organizerId = Guid.NewGuid();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = await metricsService.GetOrganizerMetricsAsync(organizerId);

        Assert.NotNull(metrics);
        Assert.Empty(metrics);
    }

    /// <summary>
    /// B7.2: Verifies only organizer's events are returned, not other organizers' events.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_ExcludesOtherOrganizersEvents()
    {
        var organizerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = organizerId, Email = "org@test.com", PasswordHash = "hash", Role = UserRole.Organizador, CreatedAt = DateTime.UtcNow },
            new User { Id = otherOrganizerId, Email = "other@test.com", PasswordHash = "hash", Role = UserRole.Organizador, CreatedAt = DateTime.UtcNow }
        );

        var myEvent = CreateEvent(organizerId, "My Event");
        var otherEvent = CreateEvent(otherOrganizerId, "Other Event");
        _context.Events.AddRange(myEvent, otherEvent);

        var tt = new TicketType { Id = Guid.NewGuid(), EventId = myEvent.Id, Name = "General", Price = 10, Quantity = 10, CreatedAt = DateTime.UtcNow };
        _context.TicketTypes.Add(tt);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        Assert.Single(metrics);
        Assert.Equal(myEvent.Id, metrics[0].EventId);
    }

    private static Event CreateEvent(Guid organizerId, string name)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            Date = now.AddDays(30),
            Location = "Test Location",
            ImageUrl = "https://example.com/test.jpg",
            OrganizerId = organizerId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    #region APR-017 — money-based organizer revenue (charged − recorded refunds)

    /// <summary>
    /// APR-005 + APR-017: refunded tickets stop counting as sold, and revenue is
    /// money-based — Σ charged transaction amounts minus Σ recorded refunds — not
    /// the list price of the non-refunded tickets. Full-price refunds keep the
    /// value organizers saw before this change (5 × 50 charged, 50 refunded → 200).
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_RefundedTickets_ExcludedFromSoldAndRevenue()
    {
        // Arrange — one confirmed purchase of 5 tickets (250 charged), 2 used,
        // 1 refunded with a 50 Refunds ledger row.
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Refunded Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 50m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 5, unitPrice: 50m,
            usedTickets: 2, refundedTickets: 1, refundAmount: 50m);
        await _context.SaveChangesAsync();

        // Act
        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        // Assert — sold = 4 (refunded excluded), revenue = 250 charged − 50 refunded,
        // scanned = 2, remaining = 100 inventory − 4 sold − 0 active reservations
        var metric = Assert.Single(metrics);
        Assert.Equal(4, metric.TicketsSold);
        Assert.Equal(200m, metric.TotalRevenue);
        Assert.Equal(2, metric.TicketsScanned);
        Assert.Equal(96, metric.RemainingInventory);
    }

    /// <summary>
    /// APR-017: a percentage refund retains the proportional money — 1 × 100 with a
    /// 50 refund yields revenue 50 (was 0 under the old price-based formula), sold 0.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_PercentageRefund_RetainsProportionalMoney()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Percentage Refund Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 1, unitPrice: 100m,
            refundedTickets: 1, refundAmount: 50m);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metric = Assert.Single(await metricsService.GetOrganizerMetricsAsync(organizerId));

        Assert.Equal(0, metric.TicketsSold);
        Assert.Equal(50m, metric.TotalRevenue);
    }

    /// <summary>
    /// APR-017: the single-event path (CalculateMetricsAsync via GetEventMetricsAsync)
    /// uses the same money-based formula as the organizer path.
    /// </summary>
    [Fact]
    public async Task GetEventMetricsAsync_PercentageRefund_RetainsProportionalMoney()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Single Percentage Refund Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 1, unitPrice: 100m,
            refundedTickets: 1, refundAmount: 50m);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = await metricsService.GetEventMetricsAsync(eventEntity.Id);

        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TicketsSold);
        Assert.Equal(50m, metrics.TotalRevenue);
    }

    /// <summary>
    /// APR-017: partial-quantity full-price refund — 3 × 100 with one ticket refunded
    /// at 100 → revenue 200, sold 2 (unchanged from the old full-price behaviour).
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_PartialQuantityFullPriceRefund_MatchesRemainingTickets()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Partial Quantity Refund Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 3, unitPrice: 100m,
            refundedTickets: 1, refundAmount: 100m);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metric = Assert.Single(await metricsService.GetOrganizerMetricsAsync(organizerId));

        Assert.Equal(2, metric.TicketsSold);
        Assert.Equal(200m, metric.TotalRevenue);
    }

    /// <summary>
    /// APR-017: full refund yields zero — 2 × 100 refunded in full (200) → revenue 0,
    /// sold 0. The transaction is flipped to Refunded (D2) and must be counted exactly
    /// once in charged (Approved ∪ Refunded), never double-subtracted.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_FullRefund_YieldsZeroAndCountsFlippedTransactionOnce()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Full Refund Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 2, unitPrice: 100m,
            refundedTickets: 2, refundAmount: 200m);
        await _context.SaveChangesAsync();

        // Precondition: the fully refunded purchase flipped its transaction (D2).
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId != Guid.Empty);
        Assert.Equal(TransactionStatus.Refunded, tx.Status);

        var metricsService = new MetricsService(_context, _logger);
        var metric = Assert.Single(await metricsService.GetOrganizerMetricsAsync(organizerId));

        Assert.Equal(0, metric.TicketsSold);
        Assert.Equal(0m, metric.TotalRevenue);
    }

    /// <summary>
    /// APR-017: with no recorded refunds, revenue equals the sum of the charged
    /// transaction amounts (never the TicketType.Price of the tickets).
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_NoRefunds_RevenueEqualsSumOfTransactionAmounts()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "No Refunds Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 1, unitPrice: 100m);
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 2, unitPrice: 150m);
        await _context.SaveChangesAsync();

        var expected = await _context.Transactions
            .Where(t => t.Status == TransactionStatus.Approved || t.Status == TransactionStatus.Refunded)
            .SumAsync(t => t.Amount);

        var metricsService = new MetricsService(_context, _logger);
        var metric = Assert.Single(await metricsService.GetOrganizerMetricsAsync(organizerId));

        Assert.Equal(400m, expected);
        Assert.Equal(expected, metric.TotalRevenue);
    }

    /// <summary>
    /// APR-017: per-event isolation — each event's revenue reflects only its own
    /// charged and refunded amounts.
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_TwoEvents_PerEventIsolation()
    {
        var organizerId = Guid.NewGuid();

        var eventA = SeedEvent(organizerId, "Isolated Event A");
        var ttA = SeedTicketType(eventA.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventA.Id, ttA.Id, quantity: 1, unitPrice: 100m,
            refundedTickets: 1, refundAmount: 40m);   // charged 100 − refunded 40 = 60

        var eventB = SeedEvent(organizerId, "Isolated Event B");
        var ttB = SeedTicketType(eventB.Id, unitPrice: 100m);
        SeedConfirmedPurchase(eventB.Id, ttB.Id, quantity: 2, unitPrice: 100m,
            refundedTickets: 2, refundAmount: 200m);  // charged 200 − refunded 200 = 0

        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var metrics = (await metricsService.GetOrganizerMetricsAsync(organizerId)).ToList();

        Assert.Equal(2, metrics.Count);
        Assert.Equal(60m, metrics.Single(m => m.EventId == eventA.Id).TotalRevenue);
        Assert.Equal(0m, metrics.Single(m => m.EventId == eventB.Id).TotalRevenue);
    }

    /// <summary>
    /// APR-016/APR-017 parity: the same fixture through MetricsService and
    /// AdminPurchaseService.GetPurchasesAsync MUST agree —
    /// organizer revenue == Σ purchase.amount − TotalRefunded (admin "Neto").
    /// </summary>
    [Fact]
    public async Task GetOrganizerMetricsAsync_MatchesAdminPurchasesNeto_Parity()
    {
        var organizerId = Guid.NewGuid();
        var eventEntity = SeedEvent(organizerId, "Parity Event");
        var tt = SeedTicketType(eventEntity.Id, unitPrice: 100m);

        // P1: 3 × 100, partial refund K=1 of 50 (percentage) → tx stays Approved.
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 3, unitPrice: 100m,
            refundedTickets: 1, refundAmount: 50m);
        // P2: 2 × 100, full refund 200 → tx flipped to Refunded (D2).
        SeedConfirmedPurchase(eventEntity.Id, tt.Id, quantity: 2, unitPrice: 100m,
            refundedTickets: 2, refundAmount: 200m);
        await _context.SaveChangesAsync();

        var metricsService = new MetricsService(_context, _logger);
        var adminService = new AdminPurchaseService(_context, Mock.Of<ILogger<AdminPurchaseService>>());

        var metric = Assert.Single(await metricsService.GetOrganizerMetricsAsync(organizerId));
        var admin = await adminService.GetPurchasesAsync(eventEntity.Id);

        var adminNeto = admin.Purchases.Sum(p => p.Amount) - admin.TotalRefunded;

        Assert.Equal(250m, adminNeto);            // (300 + 200) − (50 + 200)
        Assert.Equal(adminNeto, metric.TotalRevenue);
        Assert.Equal(2, metric.TicketsSold);      // 2 of 3 tickets remain in P1
    }

    #endregion

    #region APR-017 fixture helpers

    private Event SeedEvent(Guid organizerId, string name)
    {
        var eventEntity = CreateEvent(organizerId, name);
        _context.Events.Add(eventEntity);
        return eventEntity;
    }

    private TicketType SeedTicketType(Guid eventId, decimal unitPrice, int quantity = 100)
    {
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "General",
            Price = unitPrice,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow
        };
        _context.TicketTypes.Add(ticketType);
        return ticketType;
    }

    /// <summary>
    /// Seeds one Confirmed reservation with its tickets and a single transaction
    /// (APR-017 fixture). The transaction flips to Refunded only when every ticket
    /// is refunded (D2), mirroring AdminPurchaseService. Callers persist with
    /// SaveChangesAsync once the whole fixture is staged.
    /// </summary>
    private Guid SeedConfirmedPurchase(
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

        _context.Reservations.Add(new Reservation
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

            _context.Tickets.Add(new Ticket
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

        _context.Transactions.Add(new Transaction
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
            _context.Refunds.Add(new Refund
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

        return reservationId;
    }

    #endregion
}
