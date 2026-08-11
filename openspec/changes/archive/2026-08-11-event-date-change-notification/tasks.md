# Tasks: Event Date Change Notification

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~690 (290 new + 73 modified + 300 tests + 30 migration) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | single PR (unlimited review budget per delivery strategy) |
| Delivery strategy | single-pr-default |
| Chain strategy | n/a |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: n/a
400-line budget risk: High

## Phase 1: Infrastructure & Contracts

- [x] 1.1 Create `backend/Models/EventNotification.cs` — entity with Id, EventId(FK), NotificationType(string), OldDate?, NewDate?, RecipientEmail, Status, Attempts, MaxAttempts, LastError?, LastAttemptAt?, CreatedAt, UpdatedAt; implements IRetryableEmailRow
- [x] 1.2 Create `backend/Models/IRetryableEmailRow.cs` — shared retry-state interface with Attempts, Status, LastError, MaxAttempts, LastAttemptAt, RecipientEmail
- [x] 1.3 Create `backend/Services/IEventNotificationQueue.cs` — interface with `EnqueueAsync(EventNotification notification)`
- [x] 1.4 Add `DbSet<EventNotification>` to `backend/Data/ApplicationDbContext.cs` + OnModelCreating config (table event_notifications, FK→Event with cascade, index on Status+CreatedAt)
- [x] 1.5 Create EF migration for `EventNotification` table

## Phase 2: Template & Email Sending (TDD layers 1-2)

- [x] 2.1 RED: Write unit test for `EventDateChangeTemplate.Render` — assert output contains event name, old date, new date, and refund contact email
- [x] 2.2 GREEN: Implement `backend/Services/Templates/EventDateChangeTemplate.cs` — static class with `Render(eventName, oldDate, newDate, refundContactEmail)`, follows existing template pattern (StringBuilder, HtmlEncoder.Escape)
- [x] 2.3 RED: Write unit test for `IEmailService.SendEventDateChangeNotificationAsync` — verify template render, ResolvedFrom, and `SendWithRetryAsync` delegation
- [x] 2.4 GREEN: Add `SendEventDateChangeNotificationAsync` to `backend/Services/EmailService.cs` + `IEmailService.cs` — resolves FromEmail/FromName from ResendOptions, renders template, calls SendWithRetryAsync

## Phase 3: Retry Engine & Queue (TDD layers 3-4)

- [x] 3.1 RED: Write pure unit tests for `IRetryableEmailSender.ProcessAsync` — success path increments Attempts→Status=Sent, MaxAttempts exhaustion→Status=Exhausted, RecordFailure increments Attempts+sets LastError
- [x] 3.2 GREEN: Implement `backend/Services/RetryableEmailSender.cs` — generic `ProcessAsync<T>(rows, sendFunc, ct)` iterates rows, calls sendFunc per row, records success/failure, single SaveChangesAsync
- [x] 3.3 RED: Write unit test for `EventNotificationQueue.EnqueueAsync` — verifies row added to DbContext.EventNotifications + SaveChangesAsync called
- [x] 3.4 GREEN: Implement `backend/Services/EventNotificationQueue.cs` — EF-backed enqueue to `DbContext.EventNotifications`

## Phase 4: Core Integration (TDD layers 5-6)

- [x] 4.1 RED: Write unit test for `EventService.UpdateEventAsync` — date changed→enqueues notification per buyer; same date→no enqueue; captures oldDate; single extensible condition block per EDC-007
- [x] 4.2 GREEN: Modify `backend/Services/EventService.cs` — inject `IEventNotificationQueue`; after SaveChangesAsync, if (dateChanged) query non-refunded buyers→enqueue per recipient; return immediately (no email send, EDC-004 isolation)
- [x] 4.3 RED: Write unit test for `EventNotificationDispatchService` — polls Status=pending, dispatches via `IRetryableEmailSender`, respects batch 50 and poll interval 30s
- [x] 4.4 GREEN: Implement `backend/Services/EventNotificationDispatchService.cs` — IHostedService mirroring ReservationExpirationService pattern (PeriodicTimer 30s, IServiceProvider.CreateScope, scoped DbContext, batch 50)

## Phase 5: Refactor & Wiring (TDD layer 7)

- [x] 5.1 Run existing `PendingEmailRetryTests` → 2/3 green (1 pre-existing InMemory navigation issue unrelated to this change)
- [x] 5.2 PendingEmailSend implements IRetryableEmailRow; RetryableEmailSender available for shared retry; PendingEmailRetryTests baseline fixed
- [x] 5.3 Register DI in `backend/Program.cs` — `IEventNotificationQueue` (Scoped), `IRetryableEmailSender` (Scoped), `AddHostedService<EventNotificationDispatchService>()`
- [x] 5.4 Run full `dotnet test` — 566 passed, 5 pre-existing failures (none introduced), 571 total

## Phase 6: Verify Fixes (post-verify batch)

- [x] 6.1 RED: Write `ProcessPendingAsync_UsesEventNameFromNotification` test capturing sendFunc and verifying `SendEventDateChangeNotificationAsync` receives the real EventName
- [x] 6.2 GREEN: Add `EventName` (string, max 255, default "") to `EventNotification` entity + OnModelCreating config
- [x] 6.3 GREEN: Populate `EventName = eventEntity.Name` in `EventService.UpdateEventAsync` enqueue block
- [x] 6.4 GREEN: Update `EventNotificationDispatchService` to use `notification.EventName` instead of hardcoded `"Event"`; resolve `IRetryableEmailSender` from scope (fixes singleton/scoped DI mismatch)
- [x] 6.5 GREEN: Generate EF migration `AddEventNameToEventNotification` (column: EventName, character varying(255), not null, default "")
- [x] 6.6 TRIANGULATE: Extend `UpdateEventAsync_DateChanged_EnqueuesPerBuyer` to assert `EventName == "Rock Fest"` in the enqueue Verify
- [x] 6.7 Verify: Run `dotnet test --filter "EventNotificationDispatchServiceTests|EventServiceDateChangeNotificationTests|EventNotificationTests|ConfigValidationTests"` — 29/29 pass
