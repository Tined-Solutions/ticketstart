using Microsoft.EntityFrameworkCore;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Data;

/// <summary>
/// EA-006: best-effort backfill of legacy events to <see cref="EventStatus.Approved"/>.
///
/// The migration's Up() adds the NOT NULL Status column (default 0 = Pending) and
/// then runs this backfill so pre-existing events keep their pre-approval public
/// visibility. Scope is ALL rows — expired included. A failure (e.g. the
/// design-time factory cannot resolve) must NOT abort the schema migration: the
/// caller wraps this in try/catch, logs, and continues.
///
/// InMemory-testable: load + set + SaveChanges mirrors TicketReservationBackfill.
/// </summary>
public static class EventApprovalBackfill
{
    public static async Task RunAsync(ApplicationDbContext context)
    {
        var events = await context.Events.ToListAsync();
        if (events.Count == 0)
        {
            return;
        }

        foreach (var eventEntity in events)
        {
            eventEntity.Status = EventStatus.Approved;
        }

        await context.SaveChangesAsync();
    }
}
