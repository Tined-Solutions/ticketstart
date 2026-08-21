using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services.Guards;

/// <summary>
/// Past-event immutability guard (PEM-001, ADR-6/ADR-7): throws
/// <see cref="EventFinalizedException"/> when <paramref name="eventEntity"/> is
/// expired as of <c>clock.GetUtcNow().UtcDateTime</c>. The rule is HARD — it applies
/// regardless of the HideExpiredEvents flag (which scopes only to read filters and
/// purchase guards). Evaluate on a MATERIALIZED entity only — never inside an
/// IQueryable predicate (EF cannot translate Event.IsExpired; ADR-2). Pure static
/// helper, zero DI; the caller injects its TimeProvider (D-3).
/// </summary>
internal static class EventFinalizedGuard
{
    public static void EnsureMutable(Event eventEntity, TimeProvider clock)
    {
        if (eventEntity.IsExpired(clock.GetUtcNow().UtcDateTime))
            throw new EventFinalizedException();
    }
}