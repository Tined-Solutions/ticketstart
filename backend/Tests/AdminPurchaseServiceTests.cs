using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for IAdminPurchaseService (APR-002/003/004/007/008).
/// Covers the atomic full-purchase refund (tickets marked IsRefunded, the Approved
/// Transaction FLIPPED to Refunded — never a second row), the IsUsed guard with
/// re-check under lock, and the listing with masked buyer data + totalRefunded.
/// </summary>
public class AdminPurchaseServiceTests : IDisposable
{
    private const string TestDbName = "AdminPurchaseServiceTests";

    private ApplicationDbContext _context = null!;
    private AdminPurchaseService _service = null!;

    public AdminPurchaseServiceTests()
    {
        SetupContext();
    }

    private void SetupContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"{TestDbName}-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new AdminPurchaseService(_context, new TestLogger<AdminPurchaseService>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(Guid EventId, Guid ReservationId, Guid TicketTypeId, string MpId)> SeedConfirmedPurchase(
        int quantity = 2,
        TransactionStatus txStatus = TransactionStatus.Approved,
        bool anyTicketUsed = false,
        bool anyTicketRefunded = false,
        Guid? existingEventId = null,
        Guid? existingTicketTypeId = null)
    {
        var purchasedAt = DateTime.UtcNow.AddDays(-5);
        var eventId = existingEventId ?? Guid.NewGuid();
        var ticketTypeId = existingTicketTypeId ?? Guid.NewGuid();

        Event? eventEntity = existingEventId != null
            ? await _context.Events.FindAsync(eventId)
            : null;
        if (eventEntity == null)
        {
            eventEntity = new Event
            {
                Id = eventId,
                Name = "Test Event",
                Description = "Test Description",
                Date = DateTime.UtcNow.AddDays(30),
                Location = "Test Location",
                OrganizerId = Guid.NewGuid(),
                CreatedAt = purchasedAt,
                UpdatedAt = purchasedAt
            };
            _context.Events.Add(eventEntity);
        }

        TicketType? type = existingTicketTypeId != null
            ? await _context.TicketTypes.FindAsync(ticketTypeId)
            : null;
        if (type == null)
        {
            type = new TicketType
            {
                Id = ticketTypeId,
                EventId = eventId,
                Name = "General",
                Price = 100m,
                Quantity = 100,
                CreatedAt = purchasedAt
            };
            _context.TicketTypes.Add(type);
        }

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = quantity,
            PurchaserDNI = "31234561",
            PurchaserEmail = "juan.perez@gmail.com",
            ExpiresAt = purchasedAt.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = purchasedAt
        };
        _context.Reservations.Add(reservation);

        for (var i = 0; i < quantity; i++)
        {
            _context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                ReservationId = reservation.Id,
                PurchaserEmail = reservation.PurchaserEmail!,
                PurchaserDNI = reservation.PurchaserDNI,
                QRCodeData = $"qr-{Guid.NewGuid():N}",
                IsUsed = anyTicketUsed && i == 0,
                IsRefunded = anyTicketRefunded && i == 0,
                CreatedAt = purchasedAt.AddSeconds(i)
            });
        }

        var mpId = $"mp-{Guid.NewGuid():N}";
        _context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            MercadoPagoId = mpId,
            Amount = quantity * 100m,
            Status = txStatus,
            CreatedAt = purchasedAt,
            UpdatedAt = purchasedAt
        });

        await _context.SaveChangesAsync();
        return (eventId, reservation.Id, ticketTypeId, mpId);
    }

    #region RefundPurchaseAsync — APR-003/004/007/008

    [Fact]
    public async Task RefundPurchaseAsync_HappyPath_MarksTicketsRefundedAndFlipsTransaction()
    {
        // Arrange (APR-003 happy path)
        var (eventId, reservationId, _, mpId) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act
        await _service.RefundPurchaseAsync(reservationId, adminId);

        // Assert — all tickets marked refunded with RefundedAt set
        var tickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();
        Assert.Equal(2, tickets.Count);
        Assert.All(tickets, t => Assert.True(t.IsRefunded));
        Assert.All(tickets, t => Assert.NotNull(t.RefundedAt));

        // Assert — the Approved transaction was FLIPPED to Refunded and exactly ONE
        // row remains for the MercadoPagoId (unique IX_Transactions_MercadoPagoId respected)
        var transactions = await _context.Transactions.Where(t => t.MercadoPagoId == mpId).ToListAsync();
        var tx = Assert.Single(transactions);
        Assert.Equal(TransactionStatus.Refunded, tx.Status);
        Assert.NotNull(tx.UpdatedAt);
    }

    [Fact]
    public async Task RefundPurchaseAsync_NoApprovedTransaction_ThrowsAndChangesNothing()
    {
        // Arrange (APR-003: no Approved transaction)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(
            txStatus: TransactionStatus.Pending);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, adminId));

        var tickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).AsNoTracking().ToListAsync();
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.All(tickets, t => Assert.Null(t.RefundedAt));
    }

    [Fact]
    public async Task RefundPurchaseAsync_UsedTicket_ThrowsAndChangesNothing()
    {
        // Arrange (APR-004: a used ticket blocks the refund)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2, anyTicketUsed: true);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, adminId));

        var tickets = await _context.Tickets.Where(t => t.ReservationId == reservationId).AsNoTracking().ToListAsync();
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_ScanWinsRace_ReCheckObservesUsedAndRollsBack()
    {
        // Arrange (APR-004 race arm): the staff scan committed IsUsed between the
        // initial load and the in-lock re-check; the refund must observe it and abort.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 1, anyTicketUsed: true);
        var adminId = Guid.NewGuid();

        // Act & Assert — the re-check sees IsUsed under the lock and refuses
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, adminId));

        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_AlreadyRefunded_ThrowsAndChangesNothing()
    {
        // Arrange — the purchase was already refunded (tx flipped + ticket flagged)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(
            txStatus: TransactionStatus.Refunded, anyTicketRefunded: true);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, adminId));

        // Exactly one tx row remains (no second insert attempted)
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Refunded, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_UnknownReservation_ThrowsKeyNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RefundPurchaseAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    #endregion

    #region GetPurchasesAsync — APR-002

    [Fact]
    public async Task GetPurchasesAsync_HappyPath_ReturnsRawBuyerDataAndFlagsRefunded()
    {
        // Arrange — two confirmed purchases on the SAME event: one refunded, one approved
        var (eventId, refundedReservationId, ticketTypeId, _) = await SeedConfirmedPurchase(quantity: 2);
        var (_, secondReservationId, _, _) = await SeedConfirmedPurchase(
            quantity: 1, existingEventId: eventId, existingTicketTypeId: ticketTypeId);

        var refundedTickets = await _context.Tickets.Where(t => t.ReservationId == refundedReservationId).ToListAsync();
        foreach (var t in refundedTickets) { t.IsRefunded = true; t.RefundedAt = DateTime.UtcNow; }
        var refundedTx = await _context.Transactions.SingleAsync(t => t.ReservationId == refundedReservationId);
        refundedTx.Status = TransactionStatus.Refunded;
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert
        Assert.Equal(eventId, response.EventId);
        Assert.Equal("Test Event", response.EventName);
        Assert.Equal(2, response.Purchases.Count);

        var refundedRow = response.Purchases.Single(p => p.ReservationId == refundedReservationId);
        Assert.True(refundedRow.Refunded);
        Assert.Equal("juan.perez@gmail.com", refundedRow.PurchaserEmail);
        Assert.Equal("31234561", refundedRow.PurchaserDni);
        Assert.Equal("General", refundedRow.TicketType);
        Assert.Equal(2, refundedRow.Quantity);
        Assert.Equal(200m, refundedRow.Amount);

        var approvedRow = response.Purchases.Single(p => p.ReservationId == secondReservationId);
        Assert.False(approvedRow.Refunded);
        Assert.Equal(100m, approvedRow.Amount);

        // totalRefunded = Σ Refunded tx amounts only (never includes approved)
        Assert.Equal(200m, response.TotalRefunded);
    }

    [Fact]
    public async Task GetPurchasesAsync_TotalRefunded_SumOfRefundedTransactionAmounts()
    {
        // Arrange — one refunded purchase of 200 + one approved of 100 on the same event
        var (eventId, ticketTypeId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var (_, _, _, _) = await SeedConfirmedPurchase(quantity: 1, existingEventId: eventId, existingTicketTypeId: ticketTypeId);
        var refundedReservationId = (await _context.Reservations.ToListAsync()).First(r => r.Quantity == 2).Id;
        var refundedTx = await _context.Transactions.SingleAsync(t => t.ReservationId == refundedReservationId);
        refundedTx.Status = TransactionStatus.Refunded;
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert — only the refunded transaction amount counts
        Assert.Equal(200m, response.TotalRefunded);
    }

    [Fact]
    public async Task GetPurchasesAsync_EventNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert (APR-002: missing event → 404)
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetPurchasesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPurchasesAsync_NoConfirmedPurchases_ReturnsEmptyList()
    {
        // Arrange — event exists but no confirmed purchases (APR-002 empty list)
        var eventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Name = "Empty Event",
            Description = "desc",
            Date = DateTime.UtcNow.AddDays(10),
            Location = "loc",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventEntity.Id);

        // Assert
        Assert.Empty(response.Purchases);
        Assert.Equal(0m, response.TotalRefunded);
    }

    [Fact]
    public async Task GetPurchasesAsync_LegacyUnlinkedTickets_MarksLinkUnverified()
    {
        // Arrange — a confirmed purchase whose tickets have NULL ReservationId (APR-009
        // ambiguous legacy backfill) must be flagged "link unverified" in the listing.
        var (eventId, _, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var tickets = await _context.Tickets.ToListAsync();
        foreach (var t in tickets) { t.ReservationId = null; }
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert
        var row = Assert.Single(response.Purchases);
        Assert.True(row.LinkUnverified);
    }

    [Fact]
    public async Task GetPurchasesAsync_LinkedTickets_NotUnverified()
    {
        // Arrange — normal purchase fully linked
        var (eventId, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert
        var row = response.Purchases.Single(p => p.ReservationId == reservationId);
        Assert.False(row.LinkUnverified);
    }

    #endregion
}
