# Design: Event Date Change Email Notification

## Technical Approach

`EventService.UpdateEventAsync` detects a `Date` change before overwriting and, after commit, enqueues one `EventNotification` row per distinct non-refunded `PurchaserEmail` via a new `IEventNotificationQueue`. It then returns — no email concerns in the HTTP path. A new `EventNotificationDispatchService` (`BackgroundService`/`IHostedService`, mirroring the existing `ReservationExpirationService` pattern) polls `EventNotification`, renders + sends through `IEmailService.SendEventDateChangeNotificationAsync`, and updates row state. The per-row retry state machine (Attempts/Status/LastError/MaxAttempts) — currently inlined in `PaymentService.RetryPendingEmailsAsync` — is extracted into a shared `IRetryableEmailSender` used by BOTH the new background service AND a refactored `PaymentService.RetryPendingEmailsAsync`. `PendingEmailSend` is left untouched.

## Architecture Decisions

### Decision: Dedicated `EventNotification` table (no PendingEmailSend changes)

| Option | Tradeoff | Decision |
|--------|----------|---------|
| Extend `PendingEmailSend` with `EmailType` + nullable payload | Couples unrelated domains, breaks SRP, mutates a stable schema | Rejected |
| New `EventNotification` with `NotificationType` discriminator | Additive migration; OCP for future Location/Time types | Chosen |
| Generic `Notifications` table | Over-generalized for MVP | Rejected |

**Rationale**: User mandate + OCP. `NotificationType = "EventDateChange"` today; `"EventLocationChange"` / `"EventTimeChange"` later require no schema change — the dispatcher switches on `NotificationType` to pick the render+send path.

### Decision: Async from day 1 via BackgroundService + shared retry abstraction

| Option | Tradeoff | Decision |
|--------|----------|---------|
| Serial inline send inside PUT request | Blocks PUT on Resend latency; EDC-004 says async | Rejected |
| `BackgroundService` polling `EventNotification` + shared `IRetryableEmailSender` | Async + no duplication of the Attempt/Status state machine | Chosen |
| `IHostedService` + in-memory `Channel<T>` | Loses durability across restarts | Rejected |

**Rationale**: `ReservationExpirationService` already establishes the `IHostedService` + `PeriodicTimer` + scoped-`DbContext` pattern; we mirror it. `IRetryableEmailSender.ProcessAsync(rows, sendFunc, ct)` is the single owner of the "iterate → send → set sent/exhausted" loop; both dispatchers call it — eliminating duplication without coupling.

### Decision: `EventService` depends only on `IEventNotificationQueue` (not `IEmailService`)

**Choice**: `IEventNotificationQueue.EnqueueDateChangeNotificationsAsync(eventId, eventName, oldDate, newDate, recipientEmails)` records intent.
**Alternatives**: inject `IEmailService` into `EventService` (couples domain service to Resend; rejected).
**Rationale**: DIP + ISP — EventService records what happened; the dispatcher decides how to deliver. Generalizes the `IPaymentService.QueueEmailRetryAsync` seam but at a clean domain boundary.

## Data Flow

```
PUT api/events/{id} → EventService.UpdateEventAsync
  ├─ FindAsync; capture oldDate; validate; overwrite; SaveChangesAsync (commit)
  └─ if (dateChanged) {                                  ◄── EDC-007 single block
       buyers = DISTINCT PurchaserEmail WHERE EventId==id AND !IsRefunded
       if (buyers.Any()) _queue.EnqueueDateChangeNotificationsAsync(...)
     }  → 200 OK (no email logic in EventService; EDC-004)

EventNotificationDispatchService.ExecuteAsync (PeriodicTimer 30s, scoped DbContext, batch 50)
  pending = EventNotifications WHERE Status=="pending" AND Attempts<MaxAttempts
  _retrySender.ProcessAsync(pending, row => _emailService.SendEventDateChangeNotificationAsync(...), ct)

IRetryableEmailSender.ProcessAsync (shared state machine — single owner)
  per row: try send → RecordSuccess (Status="sent", Attempts++)
           catch → RecordFailure (Attempts++, LastError, LastAttemptAt;
                    if Attempts>=MaxAttempts → Status="exhausted")
  SaveChangesAsync once per batch

PaymentService.RetryPendingEmailsAsync (refactored, identical observable behavior)
  _retrySender.ProcessAsync(pending, row => _emailService.SendTicketEmailAsync(...), ct)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Models/EventNotification.cs` | Create | `IRetryableEmailRow`: Id, EventId, NotificationType, RecipientEmail, EventName, OldDate?, NewDate?, Attempts, MaxAttempts, Status, LastError?, LastAttemptAt?, CreatedAt, UpdatedAt |
| `backend/Models/IRetryableEmailRow.cs` | Create | Shared retry-state interface |
| `backend/Services/IRetryableEmailSender.cs` + `RetryableEmailSender.cs` | Create | Owns per-row state machine |
| `backend/Services/IEventNotificationQueue.cs` + `EventNotificationQueue.cs` | Create | Enqueue-side seam for EventService |
| `backend/Services/EventNotificationDispatchService.cs` | Create | `IHostedService`, polls + dispatches via `IRetryableEmailSender` |
| `backend/Services/IEmailService.cs` | Modify | Add `SendEventDateChangeNotificationAsync(recipientEmail, Event, oldDate, newDate)` |
| `backend/Services/EmailService.cs` | Modify | Implement via `EventDateChangeTemplate.Render` + `ResolvedFrom` (EDC-003) |
| `backend/Services/Templates/EventDateChangeTemplate.cs` | Create | Static `Render(eventName, oldDate, newDate, refundContactEmail)` |
| `backend/Services/EventService.cs` | Modify | Inject `IEventNotificationQueue`; capture `oldDate`; `if (dateChanged)` enqueue block after commit |
| `backend/Services/PaymentService.cs` | Modify | `RetryPendingEmailsAsync` delegates the per-row loop to `IRetryableEmailSender` (dedup; behavior unchanged) |
| `backend/Data/ApplicationDbContext.cs` | Modify | `DbSet<EventNotification>` + config (`event_notifications`, FK Event cascade, index Status+CreatedAt) |
| `backend/Program.cs` | Modify | Register `IEventNotificationQueue`, `IRetryableEmailSender` (Scoped), `AddHostedService<EventNotificationDispatchService>()` |
| Test files | 4 new / 2 modified | See Testing Strategy |

No `PendingEmailSend.cs` changes. Optional `appsettings` keys `EventNotification:PollIntervalSeconds` (default 30), `EventNotification:BatchSize` (default 50) with hardcoded fallbacks.

## Interfaces

```csharp
public interface IRetryableEmailRow {
    string RecipientEmail { get; }
    int Attempts { get; set; }
    int MaxAttempts { get; }
    string Status { get; set; }
    string? LastError { get; set; }
    DateTime? LastAttemptAt { get; set; }
}
public interface IRetryableEmailSender {
    Task<RetryBatchResult> ProcessAsync(IReadOnlyList<IRetryableEmailRow> rows,
        Func<IRetryableEmailRow, Task> sendAsync, CancellationToken ct);
}
public interface IEventNotificationQueue {
    Task EnqueueDateChangeNotificationsAsync(Guid eventId, string eventName,
        DateTime oldDate, DateTime newDate, IEnumerable<string> recipientEmails);
}
// IEmailService.SendEventDateChangeNotificationAsync(string, Event, DateTime, DateTime) — added
```

## Testing Strategy

Strict TDD order (each layer green before the next compiles):

| # | Layer | Drives | Verifies |
|---|-------|--------|----------|
| 1 | Template | `EventDateChangeTemplate` | Event name, old/new date, refund contact, HTML-escapes (EDC-003) |
| 2 | EmailService | `SendEventDateChangeNotificationAsync` | Uses `ResolvedFrom` ("Ticketera" `<email>`), calls Resend, returns `EmailResult` |
| 3 | Queue + Entity | `EventNotification` + `IEventNotificationQueue` | Rows inserted with snapshot, Status="pending", NotificationType="EventDateChange" |
| 4 | Retry state machine | `IRetryableEmailSender` | Success→sent; failure<max→pending/Attempts++; failure==max→exhausted (pure unit, no DB) |
| 5 | EventService | `UpdateEventAsync` | Date change→enqueue per distinct buyer (EDC-001/002); non-date silent; zero buyers no-op (EDC-005); repeat re-enqueue (EDC-006); PUT 200 regardless (EDC-004) |
| 6 | BackgroundService | `EventNotificationDispatchService` | Dispatches pending rows; increments/exhausts; honors `CancellationToken`; poll cycle |
| 7 | Refactor regression | `PendingEmailRetryTests` (unchanged) | PaymentService.RetryPendingEmailsAsync still green after delegating to `IRetryableEmailSender` |

## Migration / Rollout

EF migration adds the `event_notifications` table (additive only); `PendingEmailSend` untouched. Defaults: `Status="pending"`, `Attempts=0`, `MaxAttempts=5`, `NotificationType="EventDateChange"`. No data backfill. Rollback drops the new table (only newly-recorded notifications lost — regenerable on next date change). Single-instance deployment assumed; no distributed lock (consistent with `ReservationExpirationService`).

## Open Questions

- [ ] Confirm 30s poll + batch 50 are acceptable for MVP given buyer volume; or start stricter (e.g., 10s/25)?
- [ ] Is the `PaymentService.RetryPendingEmailsAsync` refactor (delegate to `IRetryableEmailSender`) in-scope here? It dedups the loop but mutates a previously-shipped method; existing tests must stay green.
- [ ] Future (out of scope): should the admin `RetryPendingEmails` endpoint eventually retire in favor of the background-dispatch path? Pending a separate decision.