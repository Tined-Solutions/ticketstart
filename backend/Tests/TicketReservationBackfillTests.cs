using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for the APR-009 legacy ticket backfill.
/// The backfill must link legacy tickets (ReservationId == NULL) to their confirmed
/// reservation best-effort: tickets are grouped by (EventId, TicketTypeId,
/// PurchaserDNI, PurchaserEmail), ordered by CreatedAt and chunked by reservation
/// quantity. Full chunks are assigned; ambiguous partial chunks stay NULL.
/// </summary>
public class TicketReservationBackfillTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public TicketReservationBackfillTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private (Event Event, TicketType Type, Reservation Reservation) SeedConfirmedPurchase(
        string dni = "12345678",
        string email = "buyer@test.com",
        int quantity = 3,
        int ticketCount = 3,
        DateTime? purchasedAt = null)
    {
        var purchased = purchasedAt ?? DateTime.UtcNow.AddDays(-10);

        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Test Event",
            Description = "Test Description",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = purchased,
            UpdatedAt = purchased
        };
        _context.Events.Add(eventEntity);

        var type = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            Name = "General",
            Price = 100m,
            Quantity = 100,
            CreatedAt = purchased
        };
        _context.TicketTypes.Add(type);

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = type.Id,
            Quantity = quantity,
            PurchaserDNI = dni,
            PurchaserEmail = email,
            ExpiresAt = purchased.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = purchased
        };
        _context.Reservations.Add(reservation);

        for (var i = 0; i < ticketCount; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventEntity.Id,
                TicketTypeId = type.Id,
                PurchaserEmail = email,
                PurchaserDNI = dni,
                QRCodeData = $"qr-{Guid.NewGuid():N}",
                IsUsed = false,
                CreatedAt = purchased.AddSeconds(i)
            });
        }

        _context.SaveChanges();
        return (eventEntity, type, reservation);
    }

    [Fact]
    public async Task RunAsync_FullChunk_AssignsAllTicketsToReservation()
    {
        // Arrange — 3 legacy tickets, 1 confirmed reservation of quantity 3 (APR-009)
        var (_, _, reservation) = SeedConfirmedPurchase(quantity: 3, ticketCount: 3);

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert — every ticket of the purchase is linked to the reservation
        var tickets = _context.Tickets.ToList();
        Assert.Equal(3, tickets.Count);
        Assert.All(tickets, t => Assert.Equal(reservation.Id, t.ReservationId));
    }

    [Fact]
    public async Task RunAsync_PartialChunk_LeavesAmbiguousTicketsNull()
    {
        // Arrange — 3 legacy tickets but the reservation is for 2: the 3rd ticket is
        // ambiguous (cannot be proven to belong to this reservation) → stays NULL
        var (_, _, reservation) = SeedConfirmedPurchase(quantity: 2, ticketCount: 3);

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert — first 2 (earliest CreatedAt) linked, ambiguous 3rd stays NULL
        var tickets = _context.Tickets.OrderBy(t => t.CreatedAt).ToList();
        Assert.Equal(3, tickets.Count);
        Assert.Equal(reservation.Id, tickets[0].ReservationId);
        Assert.Equal(reservation.Id, tickets[1].ReservationId);
        Assert.Null(tickets[2].ReservationId);
    }

    [Fact]
    public async Task RunAsync_MultipleReservations_ChunksInCreatedAtOrder()
    {
        // Arrange — 3 tickets, reservations of quantity 2 and 1 for the same buyer
        var (eventEntity, type, _) = SeedConfirmedPurchase(quantity: 2, ticketCount: 3);
        var secondReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = type.Id,
            Quantity = 1,
            PurchaserDNI = "12345678",
            PurchaserEmail = "buyer@test.com",
            ExpiresAt = DateTime.UtcNow,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-9)
        };
        _context.Reservations.Add(secondReservation);
        _context.SaveChanges();

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert — earliest 2 tickets → first reservation; last ticket → second reservation
        var tickets = _context.Tickets.OrderBy(t => t.CreatedAt).ToList();
        var firstReservation = _context.Reservations.OrderBy(r => r.CreatedAt).ToList()[0];
        var secondReservationPersisted = _context.Reservations.OrderBy(r => r.CreatedAt).ToList()[1];
        Assert.Equal(3, tickets.Count);
        Assert.Equal(firstReservation.Id, tickets[0].ReservationId);
        Assert.Equal(firstReservation.Id, tickets[1].ReservationId);
        Assert.Equal(secondReservationPersisted.Id, tickets[2].ReservationId);
    }

    [Fact]
    public async Task RunAsync_NoConfirmedReservation_KeepsTicketsNull()
    {
        // Arrange — tickets exist but no Confirmed reservation matches the buyer key
        var (_, _, _) = SeedConfirmedPurchase(ticketCount: 2);
        _context.Reservations.RemoveRange(_context.Reservations);
        _context.SaveChanges();

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert
        var tickets = _context.Tickets.ToList();
        Assert.Equal(2, tickets.Count);
        Assert.All(tickets, t => Assert.Null(t.ReservationId));
    }

    [Fact]
    public async Task RunAsync_AlreadyLinkedTickets_Untouched()
    {
        // Arrange — a ticket already linked must not be re-assigned or unlinked.
        // Reservation quantity 2 matches the 2 legacy (unlinked) tickets.
        var (_, _, reservation) = SeedConfirmedPurchase(quantity: 2, ticketCount: 2);
        var unlinked = new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = reservation.EventId,
            TicketTypeId = reservation.TicketTypeId,
            PurchaserEmail = reservation.PurchaserEmail!,
            PurchaserDNI = reservation.PurchaserDNI,
            QRCodeData = $"qr-{Guid.NewGuid():N}",
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ReservationId = reservation.Id
        };
        _context.Tickets.Add(unlinked);
        _context.SaveChanges();

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert — the already-linked ticket keeps its link, the 2 legacy get linked too
        var tickets = _context.Tickets.ToList();
        Assert.Equal(3, tickets.Count);
        Assert.All(tickets, t => Assert.Equal(reservation.Id, t.ReservationId));
    }

    [Fact]
    public async Task RunAsync_ExceedsAllReservationQuantities_LeavesOverflowNull()
    {
        // Arrange — 5 tickets, reservation quantity 2 → 2 linked, 3 overflow NULL
        var (_, _, reservation) = SeedConfirmedPurchase(quantity: 2, ticketCount: 5);

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert
        var tickets = _context.Tickets.OrderBy(t => t.CreatedAt).ToList();
        Assert.Equal(5, tickets.Count);
        Assert.Equal(reservation.Id, tickets[0].ReservationId);
        Assert.Equal(reservation.Id, tickets[1].ReservationId);
        Assert.Null(tickets[2].ReservationId);
        Assert.Null(tickets[3].ReservationId);
        Assert.Null(tickets[4].ReservationId);
    }

    [Fact]
    public async Task RunAsync_DifferentBuyerKeys_AreIndependent()
    {
        // Arrange — two buyers, each with a full chunk
        var (eventEntity, type, reservationA) = SeedConfirmedPurchase(dni: "11111111", email: "a@test.com", quantity: 2, ticketCount: 2);
        var reservationB = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = type.Id,
            Quantity = 1,
            PurchaserDNI = "22222222",
            PurchaserEmail = "b@test.com",
            ExpiresAt = DateTime.UtcNow,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-9)
        };
        _context.Reservations.Add(reservationB);
        _context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            EventId = eventEntity.Id,
            TicketTypeId = type.Id,
            PurchaserEmail = "b@test.com",
            PurchaserDNI = "22222222",
            QRCodeData = $"qr-{Guid.NewGuid():N}",
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddDays(-9).AddMinutes(1)
        });
        _context.SaveChanges();

        // Act
        await TicketReservationBackfill.RunAsync(_context);

        // Assert — buyer A tickets linked to A, buyer B ticket linked to B
        var aTickets = _context.Tickets.Where(t => t.PurchaserDNI == "11111111").ToList();
        Assert.All(aTickets, t => Assert.Equal(reservationA.Id, t.ReservationId));
        var bTicket = _context.Tickets.Single(t => t.PurchaserDNI == "22222222");
        Assert.Equal(reservationB.Id, bTicket.ReservationId);
    }
}
