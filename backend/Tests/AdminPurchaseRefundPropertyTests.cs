using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using ArbStatic = FsCheck.Fluent.Arb;
using GenStatic = FsCheck.Fluent.Gen;
using PropStatic = FsCheck.Fluent.Prop;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// FsCheck property suite for the admin refund flow (APR-011/APR-012): for arbitrary
/// VALID (K, amount) — 0 &lt; A ≤ unit price × K, at most 2 decimal places — the
/// refund operation MUST (1) store the ledger Amount == amount verbatim, (2) mark
/// exactly K tickets IsRefunded, (3) keep Σ Refunds.Amount ≤ tx.Amount after EVERY
/// operation, and (4) flip the Approved Transaction to Refunded IFF the operation
/// leaves zero active tickets. A fifth property proves invalid amounts are rejected
/// with NO state change (D3 guards).
/// Amounts are generated in integer cents (Gen.Choose over cents → cents/100m) so
/// every generated decimal carries at most 2 decimal places — never rounded.
/// </summary>
public class AdminPurchaseRefundPropertyTests
{
    private const decimal Price = 100m;
    private const int PriceCents = 10000;   // Price × 100

    #region Generators

    /// <summary>Fresh seed per ForAll invocation — no cross-iteration state.</summary>
    private static (ApplicationDbContext Context, Guid ReservationId, Guid TransactionId) SeedConfirmedPurchase(
        int quantity)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new ApplicationDbContext(options);

        var purchasedAt = DateTime.UtcNow.AddDays(-5);
        var eventId = Guid.NewGuid();
        var ticketTypeId = Guid.NewGuid();

        context.Events.Add(new Event
        {
            Id = eventId,
            Name = "Property Event",
            Description = "Property",
            Date = DateTime.UtcNow.AddDays(30),
            Location = "Test Location",
            OrganizerId = Guid.NewGuid(),
            CreatedAt = purchasedAt,
            UpdatedAt = purchasedAt
        });
        context.TicketTypes.Add(new TicketType
        {
            Id = ticketTypeId,
            EventId = eventId,
            Name = "General",
            Price = Price,
            Quantity = 100,
            CreatedAt = purchasedAt
        });

        var reservationId = Guid.NewGuid();
        context.Reservations.Add(new Reservation
        {
            Id = reservationId,
            EventId = eventId,
            TicketTypeId = ticketTypeId,
            Quantity = quantity,
            PurchaserDNI = "31234561",
            PurchaserEmail = "juan.perez@gmail.com",
            ExpiresAt = purchasedAt.AddMinutes(10),
            Status = ReservationStatus.Confirmed,
            CreatedAt = purchasedAt
        });

        for (var i = 0; i < quantity; i++)
        {
            context.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                TicketTypeId = ticketTypeId,
                ReservationId = reservationId,
                PurchaserEmail = "juan.perez@gmail.com",
                PurchaserDNI = "31234561",
                QRCodeData = $"qr-{Guid.NewGuid():N}",
                CreatedAt = purchasedAt.AddSeconds(i)
            });
        }

        var transactionId = Guid.NewGuid();
        context.Transactions.Add(new Transaction
        {
            Id = transactionId,
            ReservationId = reservationId,
            MercadoPagoId = $"mp-{Guid.NewGuid():N}",
            Amount = quantity * Price,
            Status = TransactionStatus.Approved,
            CreatedAt = purchasedAt,
            UpdatedAt = purchasedAt
        });

        context.SaveChanges();
        return (context, reservationId, transactionId);
    }

    private static AdminPurchaseService NewService(ApplicationDbContext context) =>
        new(context, Mock.Of<ILogger<AdminPurchaseService>>());

    /// <summary>
    /// Arbitrary VALID single-op refund: N ∈ [1,4] tickets, K ∈ [1,N], amount in
    /// integer cents ∈ [1, priceCents × K] → cents/100m (≤ 2 decimals, ≤ cap, > 0).
    /// </summary>
    private static Gen<(int N, int K, decimal Amount)> ValidSingleOpGen() =>
        from n in GenStatic.Choose(1, 4)
        from k in GenStatic.Choose(1, n)
        from amountCents in GenStatic.Choose(1, PriceCents * k)
        select (n, k, amountCents / 100m);

    /// <summary>
    /// Arbitrary VALID cumulative pair: two ops refunding all N tickets (K1 + K2 = N),
    /// each with its own custom amount ≤ its per-op cap (unit price × K).
    /// </summary>
    private static Gen<(int N, int K1, decimal Amount1, int K2, decimal Amount2)> ValidCumulativeGen() =>
        from n in GenStatic.Choose(2, 4)
        from k1 in GenStatic.Choose(1, n - 1)
        let k2 = n - k1
        from amount1Cents in GenStatic.Choose(1, PriceCents * k1)
        from amount2Cents in GenStatic.Choose(1, PriceCents * k2)
        select (n, k1, amount1Cents / 100m, k2, amount2Cents / 100m);

    /// <summary>
    /// Arbitrary INVALID amount for a valid K: either ≤ 0 (cents ∈ [-2·cap, 0]) or
    /// above the cap (cents ∈ [cap+1, cap+5000]). Both keep ≤ 2 decimals.
    /// </summary>
    private static Gen<(int N, int K, decimal Amount)> InvalidAmountGen() =>
        from n in GenStatic.Choose(1, 4)
        from k in GenStatic.Choose(1, n)
        from amountCents in GenStatic.Frequency(
            (1, GenStatic.Choose(-2 * PriceCents * k, 0)),                  // A ≤ 0
            (1, GenStatic.Choose(PriceCents * k + 1, PriceCents * k + 5000))) // A > cap
        select (n, k, amountCents / 100m);

    #endregion

    #region Property 1: ledger Amount == amount (verbatim, APR-012)

    [Property]
    public Property RefundLedger_AmountStoredVerbatim_ForArbitraryValidKAndAmount()
    {
        return PropStatic.ForAll(ArbStatic.From(ValidSingleOpGen()), scenario =>
        {
            var (n, k, amount) = scenario;
            var (context, reservationId, _) = SeedConfirmedPurchase(n);
            try
            {
                NewService(context).RefundPurchaseAsync(reservationId, k, amount, Guid.NewGuid())
                    .GetAwaiter().GetResult();

                var refund = context.Refunds.AsNoTracking().Single(r => r.ReservationId == reservationId);
                return refund.Amount == amount;
            }
            finally
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
        });
    }

    #endregion

    #region Property 2: exactly K tickets marked IsRefunded

    [Property]
    public Property RefundLedger_ExactlyKTicketsMarkedRefunded()
    {
        return PropStatic.ForAll(ArbStatic.From(ValidSingleOpGen()), scenario =>
        {
            var (n, k, amount) = scenario;
            var (context, reservationId, _) = SeedConfirmedPurchase(n);
            try
            {
                NewService(context).RefundPurchaseAsync(reservationId, k, amount, Guid.NewGuid())
                    .GetAwaiter().GetResult();

                var tickets = context.Tickets.AsNoTracking().Where(t => t.ReservationId == reservationId).ToList();
                return tickets.Count == n
                    && tickets.Count(t => t.IsRefunded) == k
                    && tickets.Where(t => t.IsRefunded).All(t => t.RefundedAt != null);
            }
            finally
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
        });
    }

    #endregion

    #region Property 3: Σ Refunds ≤ tx.Amount after every operation (APR-012 cumulative)

    [Property]
    public Property RefundLedger_CumulativeSumNeverExceedsTransactionAmount()
    {
        return PropStatic.ForAll(ArbStatic.From(ValidCumulativeGen()), scenario =>
        {
            var (n, k1, amount1, k2, amount2) = scenario;
            var (context, reservationId, transactionId) = SeedConfirmedPurchase(n);
            try
            {
                var service = NewService(context);
                var txAmount = context.Transactions.AsNoTracking().Single(t => t.Id == transactionId).Amount;

                service.RefundPurchaseAsync(reservationId, k1, amount1, Guid.NewGuid())
                    .GetAwaiter().GetResult();
                var sumAfterFirst = context.Refunds.AsNoTracking()
                    .Where(r => r.ReservationId == reservationId).ToList().Sum(r => r.Amount);
                if (sumAfterFirst > txAmount) return false;

                service.RefundPurchaseAsync(reservationId, k2, amount2, Guid.NewGuid())
                    .GetAwaiter().GetResult();
                var sumAfterSecond = context.Refunds.AsNoTracking()
                    .Where(r => r.ReservationId == reservationId).ToList().Sum(r => r.Amount);

                return sumAfterSecond <= txAmount;
            }
            finally
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
        });
    }

    #endregion

    #region Property 4: flip iff 0 active tickets remain (D2)

    [Property]
    public Property RefundLedger_TransactionFlipsIffZeroActiveTicketsRemain()
    {
        return PropStatic.ForAll(ArbStatic.From(ValidSingleOpGen()), scenario =>
        {
            var (n, k, amount) = scenario;
            var (context, reservationId, transactionId) = SeedConfirmedPurchase(n);
            try
            {
                NewService(context).RefundPurchaseAsync(reservationId, k, amount, Guid.NewGuid())
                    .GetAwaiter().GetResult();

                var tx = context.Transactions.AsNoTracking().Single(t => t.Id == transactionId);
                var activeRemaining = context.Tickets.Count(t => t.ReservationId == reservationId && !t.IsRefunded && !t.IsUsed);

                // Flip IFF the operation leaves zero active tickets (K == N).
                return (tx.Status == TransactionStatus.Refunded) == (activeRemaining == 0)
                    && (tx.Status == TransactionStatus.Refunded) == (k == n);
            }
            finally
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
        });
    }

    #endregion

    #region Property 5: invalid amounts rejected with NO state change (D3)

    [Property]
    public Property RefundGuard_RejectsInvalidAmountsWithoutStateChange()
    {
        return PropStatic.ForAll(ArbStatic.From(InvalidAmountGen()), scenario =>
        {
            var (n, k, amount) = scenario;
            var (context, reservationId, transactionId) = SeedConfirmedPurchase(n);
            try
            {
                var threw = false;
                try
                {
                    NewService(context).RefundPurchaseAsync(reservationId, k, amount, Guid.NewGuid())
                        .GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                if (!threw) return false;   // invalid amounts MUST be rejected

                var ticketsUnchanged = context.Tickets.AsNoTracking().All(t => !t.IsRefunded);
                var noLedgerRows = !context.Refunds.AsNoTracking().Any(r => r.ReservationId == reservationId);
                var txStillApproved = context.Transactions.AsNoTracking().Single(t => t.Id == transactionId).Status
                    == TransactionStatus.Approved;

                return ticketsUnchanged && noLedgerRows && txStillApproved;
            }
            finally
            {
                context.Database.EnsureDeleted();
                context.Dispose();
            }
        });
    }

    #endregion
}
