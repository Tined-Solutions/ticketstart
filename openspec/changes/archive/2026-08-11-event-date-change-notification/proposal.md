# Proposal: Event Date Change Email Notification

## Intent
When an admin edits an event via `PUT api/events/{id}` and the `Date` field changes, all ticket buyers MUST automatically receive an email announcing the new date plus a "you can request a refund at `<contact>`" section. Today `EventService.UpdateEventAsync` blindly overwrites fields with no change detection and no side effects, so buyers learn of date moves late or never.

## Scope

### In Scope
- Detect `Date` change in `EventService.UpdateEventAsync` before overwrite; capture `oldDate`
- After `SaveChangesAsync`, query distinct non-refunded buyer emails and send a date-change notification
- New `IEmailService.SendEventDateChangeNotificationAsync` + `Services/Templates/EventDateChangeTemplate`
- Reuse `PendingEmailSend` retry pattern (PaymentService); email failures logged, update still succeeds
- Zero buyers = silent no-op; every date change re-notifies (no dedup, no tracking)
- Sender name + refund contact reuse existing global `Resend` config (see question round)

### Out of Scope
- Location/time change notifications (extensible hook only, not built now)
- Deduplication / per-buyer notification tracking table
- Admin confirmation dialog (automatic, no confirmation)
- Any frontend change

## Capabilities

### New Capabilities
- `event-date-change-notification`: Automatic email to all non-refunded buyers when an admin changes an event's date via the existing PUT endpoint — date-change detection, distinct-buyer query, send-after-commit, retry, sender/refund-contact from config, zero-buyer no-op.

### Modified Capabilities
- None. `admin-purchase-refunds` APR-008 forbids a refund-action email; this is a distinct date-move notification, so no existing spec requirement changes.

## Approach
Inject `IEmailService` into `EventService` (mirrors `PaymentService`). Compare `eventEntity.Date != request.Date` before overwriting; capture `oldDate`. After `SaveChangesAsync`, if changed: query distinct buyers, loop and `await SendEventDateChangeNotificationAsync` in try/catch. A single `if (dateChanged)` block keeps it trivially extensible to location/time later without a new abstraction today.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Services/EventService.cs` | Modified | Date-change detection + notify buyers after commit |
| `backend/Services/IEmailService.cs`, `EmailService.cs` | Modified | New `SendEventDateChangeNotificationAsync` |
| `backend/Services/Templates/EventDateChangeTemplate.cs` | New | Renders new date + refund-request section |
| `backend/Tests/EventServiceTests.cs`, `EmailPropertyTests.cs` | Modified | Date-change triggers; non-date silent; email error tolerated |
| `backend/appsettings*.json` | Possibly Modified | Only if a new `RefundContactEmail` key is chosen (see question round) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Slow PUT response for events with many buyers | Low | Existing retry; tolerate moderate counts, log+queue on failure |
| Resend rate limits on bulk send | Med | Reuse `PendingEmailSend` retry queue |
| Event update fails because email send throws | Low | try/catch isolates; commit precedes any send |

## Rollback Plan
Remove the `IEmailService` injection and `if (dateChanged)` block from `UpdateEventAsync`; delete the new template + send method and their tests. Pure code revert — no DB migration, no data concerns.

## Dependencies
- Existing `IEmailService` / `EmailService` / `PendingEmailSend` retry infra (no new external deps)

## Success Criteria
- [ ] Date change via PUT → all non-refunded buyers receive one email with new date + refund contact
- [ ] Non-date edits send nothing
- [ ] Zero buyers = no send, no error
- [ ] Email send failure does not fail the event update
- [ ] `dotnet test` green; existing tests unaffected

## Proposal question round
Two config assumptions need user confirmation before specs (defaults chosen to match existing patterns):
1. **Refund contact**: No `reembolsos@` key exists in `appsettings.json` / `ResendOptions` — only `Resend:FromEmail` (`tickets@resend.dev` dev / `tickets@yourdomain.com` prod). **Default**: reuse `Resend:FromEmail`. Alternative: add a new `Resend:RefundContactEmail` key.
2. **Sender name**: Existing `Resend:FromName` = `"Ticketera"` (not "Ticketera Online"). **Default**: reuse `Resend:FromName` (= "Ticketera", matching actual existing emails). Alternative: override date-change emails as "Ticketera Online".