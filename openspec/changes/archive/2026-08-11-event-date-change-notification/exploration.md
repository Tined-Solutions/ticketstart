## Exploration: Event Date Change Email Notification

### Current State

**Event editing** is a PUT `api/events/{id}` endpoint (`EventController.UpdateEvent`, line 79). It delegates to `EventService.UpdateEventAsync` (line 374), which performs ownership validation and then **blindly overwrites all fields** — including `Date` — with no change detection and no side effects after save.

**Email infrastructure** uses Resend via `IEmailService`/`EmailService`. Three methods exist today:
- `SendTicketEmailAsync(recipientEmail, tickets, event)` — ticket confirmation with QR codes
- `SendResendEmailAsync(recipientEmail, tickets, event)` — ticket resend
- `SendRefundNotificationAsync(recipientEmail, amount, reason)` — refund notification

Emails are sent **synchronously** after DB commit with try/catch + `PendingEmailSend` retry queue on failure (see `PaymentService.ProcessApprovedPaymentAsync` line 288–307). Templates are static classes (`TicketConfirmationTemplate`, `RefundNotificationTemplate`) in `Services/Templates/` with a `Render()` method. `ResendOptions` provides `FromEmail` (`tickets@resend.dev` in dev) — this is the contact address that should appear in the "request refund" section.

**No broadcast notification pattern exists** anywhere in the codebase. This is net-new functionality.

**Buyer querying**: Tickets link to events via `Ticket.EventId` FK. Each ticket has `PurchaserEmail` and `IsRefunded`. Active buyers for an event = `_context.Tickets.Where(t => t.EventId == eventId && !t.IsRefunded).Select(t => t.PurchaserEmail).Distinct()`.

### Affected Areas

| File | Why |
|------|-----|
| `backend/Services/EventService.cs` — `UpdateEventAsync` (line 374) | Must detect date change and trigger notification **after** save. Currently no change detection exists. |
| `backend/Services/IEventService.cs` — `UpdateEventAsync` signature | If we inject `IEmailService` into `EventService`, no signature change. If we use Mediator or event pattern, no change either. |
| `backend/Controllers/EventController.cs` — `UpdateEvent` (line 79) | Unchanged — date change detection lives in the service, not the controller. |
| `backend/Services/IEmailService.cs` + `EmailService.cs` | New method needed: `SendEventDateChangeNotificationAsync(recipientEmail, event, oldDate, newDate)`. |
| `backend/Services/Templates/` | New template `EventDateChangeTemplate` — static class with `Render(eventName, oldDate, newDate, contactEmail)`. |
| `backend/Models/Event.cs` | Unchanged — `Date` field exists, no modifications needed. |
| `backend/Models/Ticket.cs` | Unchanged — used only for querying buyers. |
| `backend/appsettings*.json` | May add `ContactEmail` or reuse `Resend:FromEmail` for the refund contact. |
| `backend/Tests/EventServiceTests.cs` | New tests needed: date change triggers notification, non-date change does not, email errors log but don't fail the update. |
| `backend/Tests/EmailPropertyTests.cs` | New property tests for the date-change template. |
| `backend/Program.cs` | Unchanged — `IEmailService` already registered as scoped. |

### Approaches

1. **Inject IEmailService into EventService + detect in UpdateEventAsync** (Recommended)
   - Add `IEmailService` to `EventService` constructor
   - In `UpdateEventAsync`, compare `eventEntity.Date != request.Date` before overwriting
   - After `SaveChangesAsync`, if date changed: query distinct buyer emails, loop and send
   - Pros: Simple, follows existing `PaymentService` pattern, no new abstractions, minimal changes
   - Cons: `EventService` takes on an additional concern (notifications); synchronous email sends within the HTTP request could be slow for events with many buyers
   - Effort: **Low**

2. **Extract notification to a separate service with an IDateChangeNotifier interface**
   - Create `IEventDateChangeNotifier` → `EmailEventDateChangeNotifier`
   - `EventService` calls `_dateChangeNotifier.NotifyDateChangeAsync(event, oldDate)` after save
   - Pros: Clean separation of concerns; easily extensible to location/time changes; mockable in tests
   - Cons: More files, more DI wiring; abstraction may be premature for only one notification type
   - Effort: **Medium**

3. **Background job via a "pending notification" table + hosted service**
   - After date change save, insert rows into `PendingDateChangeNotification` table
   - A `BackgroundService` polls and sends emails asynchronously
   - Pros: HTTP request never waits for email sends; resilient to high buyer counts
   - Cons: Significant complexity; new table + migration + background service; delayed delivery; overengineered for MVP
   - Effort: **High**

### Recommendation

**Approach 1 — Inject IEmailService into EventService**

Rationale:
- This is the **simple, direct pattern** already used by `PaymentService` (line 294): after DB commit, call `_emailService.SendTicketEmailAsync()` in try/catch
- The user explicitly said "no confirmation dialog — it's automatic" — synchronous inline notification is the simplest way to achieve this with the least architectural change
- For extension (future location/time changes): the single `if (dateChanged)` check is trivial to extend to `if (dateChanged || locationChanged || timeChanged)` later. When that day comes, **then** extract to an `IDateChangeNotifier` interface — not before
- **Architecture designed for extension** without overbuilding: the change detection is encapsulated in one `if` block; the "what to do" call is a single method; when location/time come, we refactor that block into a notifier

The synchronous email send is acceptable for MVP because:
- Typical event has limited distinct buyers (10s-100s), not thousands
- `EmailService.SendWithRetryAsync` already has exponential backoff
- Each send is `await`ed serially, but the HTTP request can handle moderate wait times

### Risks

- **Slow HTTP response for events with many buyers**: Serial email sends could exceed the HTTP timeout if an event has 500+ distinct buyers. Mitigation: cap the loop at a reasonable limit and log a warning for remaining.
- **Email sending failure blocks the update response**: Same risk as `PaymentService` already handles — email failures are caught and logged, not propagated. The date change itself succeeds regardless.
- **Rate limiting**: Resend has rate limits. Sending many emails rapidly could hit them. `SendWithRetryAsync` handles this via retry — but if the rate limit persists across retries, emails fail silently. Mitigation: `PendingEmailSend` retry queue could be reused.
- **Duplicate emails**: If the admin changes date → changes back → changes again, buyers get notified each time (per requirement). No deduplication risk needed.

### Ready for Proposal

**Yes.** All technical questions are answered. The approach is clear, the integration points are known, and the risk assessment is complete. Ready to move to the Proposal phase.
