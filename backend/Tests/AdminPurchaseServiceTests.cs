using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// RED tests for IAdminPurchaseService (APR-002/003/004/007/008/012/013/014).
/// Covers the atomic QUANTITY-BASED partial refund: the K oldest non-refunded,
/// non-used tickets marked IsRefunded (APR-013), one immutable Refunds ledger row
/// per operation (APR-012), the Approved Transaction flipped to Refunded ONLY when
/// zero active tickets remain (D2 — never a second row), and the listing projecting
/// RefundedQuantity/RefundedAmount with TotalRefunded = Σ Refunds.Amount (APR-002).
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

    // TRACKED query: listing tests mutate these entities (IsRefunded / ReservationId)
    // as seeding, so they must be tracked for SaveChangesAsync to persist the change.
    private async Task<List<Ticket>> TicketsOf(Guid reservationId) =>
        await _context.Tickets.Where(t => t.ReservationId == reservationId).ToListAsync();

    #region RefundPurchaseAsync — APR-003/004/012/013

    [Fact]
    public async Task RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved()
    {
        // Arrange (APR-003 partial happy path): 4 active tickets, refund K=2.
        var (_, reservationId, _, mpId) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();

        // Act
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);

        // Assert — exactly 2 of the 4 tickets marked refunded with RefundedAt set
        var tickets = await TicketsOf(reservationId);
        Assert.Equal(4, tickets.Count);
        Assert.Equal(2, tickets.Count(t => t.IsRefunded));
        Assert.All(tickets.Where(t => t.IsRefunded), t => Assert.NotNull(t.RefundedAt));
        Assert.All(tickets.Where(t => !t.IsRefunded), t => Assert.False(t.IsRefunded));

        // Assert — the Approved transaction is NOT flipped on a partial refund (D2)
        var tx = await _context.Transactions.SingleAsync(t => t.MercadoPagoId == mpId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_FullAtZeroActive_FlipsTransaction()
    {
        // Arrange (APR-003 full-at-zero): 2 active tickets, refund K=2 → active == K.
        var (_, reservationId, _, mpId) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);

        // Assert — all tickets refunded
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.True(t.IsRefunded));

        // Assert — the Approved transaction FLIPPED to Refunded and exactly ONE row
        // remains for the MercadoPagoId (unique IX_Transactions_MercadoPagoId respected)
        var transactions = await _context.Transactions.Where(t => t.MercadoPagoId == mpId).ToListAsync();
        var tx = Assert.Single(transactions);
        Assert.Equal(TransactionStatus.Refunded, tx.Status);
        Assert.NotNull(tx.UpdatedAt);
    }

    [Fact]
    public async Task RefundPurchaseAsync_InsertsRefundRow_WithTicketIdsQuantityUnitPriceAmountAdminId()
    {
        // Arrange (APR-012): 4 active tickets, refund K=2. TicketType.Price = 100m (D7).
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();
        var tickets = await TicketsOf(reservationId);
        var selectedIds = tickets.OrderBy(t => t.CreatedAt).Take(2).Select(t => t.Id).ToArray();

        // Act
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);

        // Assert — exactly one Refunds row with the operation snapshot
        var refund = Assert.Single(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        Assert.Equal(reservationId, refund.ReservationId);
        Assert.Equal(selectedIds, refund.TicketIds);          // TicketIds = the 2 selected
        Assert.Equal(2, refund.Quantity);                      // Quantity = K
        Assert.Equal(200m, refund.Amount);                     // Amount = Price × K (100 × 2)
        Assert.Equal(adminId, refund.AdminId);
        Assert.NotEqual(default, refund.CreatedAt);
    }

    [Fact]
    public async Task RefundPurchaseAsync_Cumulative_SecondRefundAppendsAndFlipsAtZero()
    {
        // Arrange (APR-012 cumulative): 4 active tickets; refund K=2 twice with CUSTOM
        // amounts. Σ Refunds must stay ≤ tx.Amount (400 = 4 × 100) after EVERY op.
        var (_, reservationId, _, mpId) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();

        // Act — first partial refund (2 of 4) with a custom amount
        await _service.RefundPurchaseAsync(reservationId, 2, 150.25m, adminId);

        // Assert after op 1 — one row, verbatim amount, Σ ≤ tx.Amount
        var refundsAfterFirst = await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync();
        var sumAfterFirst = refundsAfterFirst.Sum(r => r.Amount);
        Assert.Single(refundsAfterFirst);
        Assert.Equal(150.25m, sumAfterFirst);
        Assert.True(sumAfterFirst <= 400m);

        // Act — second refund (the last 2) with another custom amount (still ≤ the
        // per-operation cap of unit price × K = 200)
        await _service.RefundPurchaseAsync(reservationId, 2, 199.5m, adminId);

        // Assert — two Refunds rows appended; TotalRefunded = Σ Amounts ≤ tx.Amount
        var refunds = await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync();
        Assert.Equal(2, refunds.Count);
        Assert.Equal(150.25m, refunds[0].Amount);
        Assert.Equal(199.5m, refunds[1].Amount);
        var totalRefunded = refunds.Sum(r => r.Amount);
        Assert.Equal(349.75m, totalRefunded);
        Assert.True(totalRefunded <= 400m);

        // Assert — all 4 tickets refunded, and the tx flipped only at zero active
        var tickets = await TicketsOf(reservationId);
        Assert.Equal(4, tickets.Count(t => t.IsRefunded));
        var tx = await _context.Transactions.SingleAsync(t => t.MercadoPagoId == mpId);
        Assert.Equal(TransactionStatus.Refunded, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_QuantityAboveActiveRemaining_ThrowsNoChange()
    {
        // Arrange (APR-003: K > active): 2 active tickets, request K=3.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 3, 300m, adminId));

        // Assert — nothing mutated: no ticket, no Refunds row, tx still Approved
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RefundPurchaseAsync_QuantityZeroOrNegative_ThrowsNoChange(int quantity)
    {
        // Arrange (APR-003: K ≤ 0 blocked)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, quantity, 200m, adminId));

        // Assert — no state change
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_AmountZeroOrNegative_ThrowsNoChange()
    {
        // Arrange (APR-003: A ≤ 0 blocked). Quantity is valid so the AMOUNT guard fires.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act & Assert — 0 and negative amounts are rejected with the exact message
        foreach (var amount in new[] { 0m, -1m })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.RefundPurchaseAsync(reservationId, 1, amount, adminId));
            Assert.Equal("Refund amount must be greater than zero", ex.Message);
        }

        // Assert — no state change
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_AmountAboveCap_ThrowsNoChange()
    {
        // Arrange (APR-003: A > unit price × K blocked): 4 active tickets, refund K=2 →
        // cap = 100 × 2 = 200; amount 200.01 exceeds it by one cent.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 2, 200.01m, adminId));
        Assert.Equal("Cannot refund 200.01 for 2 tickets; maximum is 200", ex.Message);

        // Assert — no ticket, no Refunds row, tx still Approved
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_AmountMoreThanTwoDecimals_RejectedNotRounded()
    {
        // Arrange (APR-003/D3: > 2 decimal places rejected, NEVER rounded to 33.33).
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 1, 33.333m, adminId));
        Assert.Equal("Refund amount cannot have more than 2 decimal places", ex.Message);

        // Assert — no Refunds row exists and nothing was rounded/persisted
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_QuantityGuardFiresBeforeAmountGuard()
    {
        // Arrange (D3 guard ordering): K=3 > 2 active with a VALID amount — the failure
        // must report the QUANTITY violation; amount validation must never run.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var adminId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 3, 100m, adminId));
        Assert.Contains("Cannot refund 3 tickets", ex.Message);
        Assert.Contains("active remaining", ex.Message);
        Assert.DoesNotContain("Refund amount", ex.Message);
    }

    [Fact]
    public async Task RefundPurchaseAsync_CustomAmountStoredVerbatim()
    {
        // Arrange (APR-012/D3): partial refund K=2 of 4 with a custom amount of 50.5 —
        // the ledger must store 50.5 EXACTLY (verbatim), not 50.50-rounded price math.
        var (_, reservationId, _, mpId) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();

        // Act
        await _service.RefundPurchaseAsync(reservationId, 2, 50.5m, adminId);

        // Assert — one Refunds row with Amount == 50.5 verbatim
        var refund = Assert.Single(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
        Assert.Equal(50.5m, refund.Amount);
        Assert.Equal(2, refund.Quantity);
        Assert.Equal(adminId, refund.AdminId);

        // Assert — 2 tickets marked refunded, tx stays Approved (partial op, D2)
        var tickets = await TicketsOf(reservationId);
        Assert.Equal(2, tickets.Count(t => t.IsRefunded));
        var tx = await _context.Transactions.SingleAsync(t => t.MercadoPagoId == mpId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
    }

    [Fact]
    public async Task RefundPurchaseAsync_SelectsOldestTickets_ByCreatedAt()
    {
        // Arrange (APR-013): 4 tickets with DISTINCT CreatedAt values; refund K=2.
        // Ticket CreatedAt = purchasedAt.AddSeconds(i) → i=0 and i=1 are the oldest.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();
        var tickets = await TicketsOf(reservationId);
        var oldestTwo = tickets.OrderBy(t => t.CreatedAt).Take(2).Select(t => t.Id).ToHashSet();

        // Act
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);

        // Assert — exactly the two earliest-CreatedAt tickets are marked refunded
        var after = await TicketsOf(reservationId);
        var refundedIds = after.Where(t => t.IsRefunded).Select(t => t.Id).ToHashSet();
        Assert.Equal(oldestTwo, refundedIds);
    }

    [Fact]
    public async Task RefundPurchaseAsync_ConcurrentQuantityGuard_SecondSeesFirstCommittedState()
    {
        // Arrange (APR-003 concurrent serialize): 4 active tickets. Simulates two
        // sequential refunds where the second re-reads the FIRST's committed state
        // under lock (InMemory path) — no ticket may be refunded twice.
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 4);
        var adminId = Guid.NewGuid();

        // Act — "request 1" refunds 2, "request 2" refunds 2: both observe committed state
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);
        await _service.RefundPurchaseAsync(reservationId, 2, 200m, adminId);

        // Assert — each ticket refunded exactly once; total refunded == 4
        var tickets = await TicketsOf(reservationId);
        Assert.Equal(4, tickets.Count(t => t.IsRefunded));
        Assert.DoesNotContain(tickets, t => t.IsRefunded && t.RefundedAt == null);
        var refunds = await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync();
        Assert.Equal(2, refunds.Count);
        Assert.Equal(4, refunds.Sum(r => r.Quantity));
    }

    [Fact]
    public async Task RefundPurchaseAsync_NoApprovedTransaction_ThrowsNoChange()
    {
        // Arrange (APR-003: no Approved transaction)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(
            txStatus: TransactionStatus.Pending);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 1, 100m, adminId));

        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        Assert.All(tickets, t => Assert.Null(t.RefundedAt));
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RefundPurchaseAsync_UsedTicket_ThrowsNoChange()
    {
        // Arrange (APR-004: a used ticket blocks the WHOLE refund, any K)
        var (_, reservationId, _, _) = await SeedConfirmedPurchase(quantity: 2, anyTicketUsed: true);
        var adminId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RefundPurchaseAsync(reservationId, 1, 100m, adminId));

        var tickets = await TicketsOf(reservationId);
        Assert.All(tickets, t => Assert.False(t.IsRefunded));
        var tx = await _context.Transactions.SingleAsync(t => t.ReservationId == reservationId);
        Assert.Equal(TransactionStatus.Approved, tx.Status);
        Assert.Empty(await _context.Refunds.Where(r => r.ReservationId == reservationId).AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RefundPurchaseAsync_UnknownReservation_ThrowsKeyNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RefundPurchaseAsync(Guid.NewGuid(), 1, 100m, Guid.NewGuid()));
    }

    #endregion

    #region GetPurchasesAsync — APR-002/012/014

    [Fact]
    public async Task GetPurchasesAsync_PartialAndFullRefunded_ReturnsRefundedQuantityRefundedAmountAndDerivedFlag()
    {
        // Arrange — two confirmed purchases on the SAME event: one partially refunded
        // (1 of 2 tickets), one fully refunded. Refunds ledger rows back each state.
        var (eventId, partialReservationId, ticketTypeId, _) = await SeedConfirmedPurchase(quantity: 2);
        var (_, fullReservationId, _, _) = await SeedConfirmedPurchase(
            quantity: 1, existingEventId: eventId, existingTicketTypeId: ticketTypeId);

        // Partial: 1 of 2 tickets refunded, 1 Refunds row (1 × 100), tx stays Approved.
        var partialTickets = await TicketsOf(partialReservationId);
        partialTickets[0].IsRefunded = true;
        partialTickets[0].RefundedAt = DateTime.UtcNow;
        _context.Refunds.Add(new Refund
        {
            Id = Guid.NewGuid(),
            ReservationId = partialReservationId,
            TicketIds = new[] { partialTickets[0].Id },
            Quantity = 1,
            Amount = 100m,
            AdminId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });

        // Full: 1 of 1 tickets refunded, 1 Refunds row (1 × 100), tx Refunded.
        var fullTickets = await TicketsOf(fullReservationId);
        fullTickets[0].IsRefunded = true;
        fullTickets[0].RefundedAt = DateTime.UtcNow;
        _context.Refunds.Add(new Refund
        {
            Id = Guid.NewGuid(),
            ReservationId = fullReservationId,
            TicketIds = new[] { fullTickets[0].Id },
            Quantity = 1,
            Amount = 100m,
            AdminId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        var fullTx = await _context.Transactions.SingleAsync(t => t.ReservationId == fullReservationId);
        fullTx.Status = TransactionStatus.Refunded;
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert
        Assert.Equal(eventId, response.EventId);
        Assert.Equal("Test Event", response.EventName);
        Assert.Equal(2, response.Purchases.Count);

        var partialRow = response.Purchases.Single(p => p.ReservationId == partialReservationId);
        Assert.Equal(1, partialRow.RefundedQuantity);   // APR-012
        Assert.Equal(100m, partialRow.RefundedAmount);   // APR-012
        Assert.False(partialRow.Refunded);               // 1 >= 2? NO → derived flag false
        Assert.Equal("juan.perez@gmail.com", partialRow.PurchaserEmail);
        Assert.Equal("31234561", partialRow.PurchaserDni);
        Assert.Equal("General", partialRow.TicketType);
        Assert.Equal(2, partialRow.Quantity);
        Assert.Equal(200m, partialRow.Amount);

        var fullRow = response.Purchases.Single(p => p.ReservationId == fullReservationId);
        Assert.Equal(1, fullRow.RefundedQuantity);
        Assert.Equal(100m, fullRow.RefundedAmount);
        Assert.True(fullRow.Refunded);                   // 1 >= 1 → derived flag true
        Assert.Equal(100m, fullRow.Amount);

        // totalRefunded = Σ Refunds.Amount (APR-012), both rows count
        Assert.Equal(200m, response.TotalRefunded);
    }

    [Fact]
    public async Task GetPurchasesAsync_TotalRefunded_SumOfRefundsAmount()
    {
        // Arrange — one refunded purchase of 200 (two Refunds rows of 100) + one
        // approved of 100 on the same event. totalRefunded = Σ Refunds.Amount.
        var (eventId, ticketTypeId, _, _) = await SeedConfirmedPurchase(quantity: 2);
        var (_, secondReservationId, _, _) = await SeedConfirmedPurchase(quantity: 1, existingEventId: eventId, existingTicketTypeId: ticketTypeId);
        var refundedReservationId = (await _context.Reservations.AsNoTracking().ToListAsync()).First(r => r.Quantity == 2).Id;

        var tickets = await TicketsOf(refundedReservationId);
        foreach (var t in tickets) { t.IsRefunded = true; t.RefundedAt = DateTime.UtcNow; }
        _context.Refunds.Add(new Refund { Id = Guid.NewGuid(), ReservationId = refundedReservationId, TicketIds = new[] { tickets[0].Id }, Quantity = 1, Amount = 100m, AdminId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        _context.Refunds.Add(new Refund { Id = Guid.NewGuid(), ReservationId = refundedReservationId, TicketIds = new[] { tickets[1].Id }, Quantity = 1, Amount = 100m, AdminId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        var refundedTx = await _context.Transactions.SingleAsync(t => t.ReservationId == refundedReservationId);
        refundedTx.Status = TransactionStatus.Refunded;
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert — Σ Refunds.Amount = 200, never includes the approved purchase
        Assert.Equal(200m, response.TotalRefunded);
        Assert.Equal(2, response.Purchases.Count);
        var approvedRow = response.Purchases.Single(p => p.ReservationId == secondReservationId);
        Assert.False(approvedRow.Refunded);
    }

    [Fact]
    public async Task GetPurchasesAsync_LegacyRefundWithBackfilledRow_KeepsCountingTotalRefunded()
    {
        // Arrange (APR-014): a Refunded transaction created BEFORE this change, with a
        // backfilled Refunds row whose AdminId is NULL (pure-SQL migration). The legacy
        // refund MUST keep counting toward totalRefunded.
        var (eventId, ticketTypeId, _, _) = await SeedConfirmedPurchase(quantity: 1);
        var (_, approvedReservationId, _, _) = await SeedConfirmedPurchase(quantity: 1, existingEventId: eventId, existingTicketTypeId: ticketTypeId);
        var legacyReservationId = (await _context.Reservations.AsNoTracking().ToListAsync()).First(r => r.Quantity == 1 && r.Id != approvedReservationId).Id;

        var ticket = Assert.Single(await TicketsOf(legacyReservationId));
        ticket.IsRefunded = true;
        ticket.RefundedAt = DateTime.UtcNow;
        _context.Refunds.Add(new Refund
        {
            Id = Guid.NewGuid(),
            ReservationId = legacyReservationId,
            TicketIds = new[] { ticket.Id },
            Quantity = 1,
            Amount = 100m,
            AdminId = null,                                  // backfilled legacy row (APR-014)
            CreatedAt = ticket.RefundedAt.Value
        });
        var legacyTx = await _context.Transactions.SingleAsync(t => t.ReservationId == legacyReservationId);
        legacyTx.Status = TransactionStatus.Refunded;
        await _context.SaveChangesAsync();

        // Act
        var response = await _service.GetPurchasesAsync(eventId);

        // Assert — the backfilled row keeps TotalRefunded at 100 (no regression to 0)
        Assert.Equal(100m, response.TotalRefunded);
        var legacyRow = response.Purchases.Single(p => p.ReservationId == legacyReservationId);
        Assert.True(legacyRow.Refunded);
        Assert.Equal(100m, legacyRow.RefundedAmount);
        var approvedRow = response.Purchases.Single(p => p.ReservationId == approvedReservationId);
        Assert.False(approvedRow.Refunded);
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
        var tickets = await _context.Tickets.Where(t => t.ReservationId != null).ToListAsync();
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
