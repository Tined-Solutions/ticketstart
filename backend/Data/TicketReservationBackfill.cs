using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Data;

/// <summary>
/// APR-009: best-effort link of legacy tickets to their confirmed reservation.
///
/// Tickets with a NULL <see cref="Ticket.ReservationId"/> are grouped by
/// (EventId, TicketTypeId, PurchaserDNI, PurchaserEmail), ordered by CreatedAt and
/// chunked by each confirmed reservation's Quantity (reservations ordered by
/// CreatedAt). A chunk is assigned only when it is FULL (exactly the reservation
/// quantity): a partial final chunk cannot be proven to belong to that reservation
/// and stays NULL. Tickets beyond all confirmed reservation quantities also stay
/// NULL. Already-linked tickets are never touched.
///
/// Going forward, <c>TicketService.CreateTicketsAsync</c> sets the FK precisely, so
/// only legacy rows ever flow through here.
/// </summary>
public static class TicketReservationBackfill
{
    public static async Task RunAsync(ApplicationDbContext context)
    {
        var unlinkedTickets = await context.Tickets
            .Where(t => t.ReservationId == null)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToListAsync();

        if (unlinkedTickets.Count == 0)
        {
            return;
        }

        var groups = unlinkedTickets
            .GroupBy(t => new { t.EventId, t.TicketTypeId, t.PurchaserDNI, t.PurchaserEmail })
            .ToList();

        foreach (var group in groups)
        {
            var reservations = await context.Reservations
                .Where(r => r.EventId == group.Key.EventId &&
                            r.TicketTypeId == group.Key.TicketTypeId &&
                            r.PurchaserDNI == group.Key.PurchaserDNI &&
                            r.PurchaserEmail == group.Key.PurchaserEmail &&
                            r.Status == ReservationStatus.Confirmed)
                .OrderBy(r => r.CreatedAt)
                .ThenBy(r => r.Id)
                .ToListAsync();

            if (reservations.Count == 0)
            {
                continue;
            }

            var tickets = group.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).ToList();
            var offset = 0;

            foreach (var reservation in reservations)
            {
                var chunk = tickets.Skip(offset).Take(reservation.Quantity).ToList();
                if (chunk.Count < reservation.Quantity)
                {
                    // Partial chunk: cannot prove these tickets belong to this
                    // reservation → leave them NULL (ambiguous, APR-009).
                    break;
                }

                foreach (var ticket in chunk)
                {
                    ticket.ReservationId = reservation.Id;
                }

                offset += reservation.Quantity;
            }
        }

        await context.SaveChangesAsync();
    }
}
