```yaml
status: success
artifacts:
  engram_topic_key: sdd/hide-expired-events/design
  engram_observation_id: 81
  openspec_path: openspec/changes/hide-expired-events/design.md
next_recommended: sdd-tasks
risks:
  - severity: high
    title: Catch-order in ReservationController/PaymentController
    detail: New `catch (EventExpiredException)` MUST precede the generic `catch (Exception)→500`; tasks phase must verify the order post-edit.
  - severity: medium
    title: EF Core translation of inline `e.Date > asOf`
    detail: Inline predicate must stay in `.Where(...)`; do not call `e.IsExpired(now)` inside IQueryable (client-eval)._regression test required.
  - severity: medium
    title: Test fixtures with hardcoded dates flipping to "expired" as real time advances
    detail: Audit fixtures; use `FakeTimeProvider` or future-dated seeds.
  - severity: low
    title: Route precedence `[HttpGet("manage")]` vs `[HttpGet("{id:guid}")]`
    detail: GUID constraint disambiguates; add explicit route test.
skill_resolution:
  status: loaded
  skills_loaded:
    - aspnet-api-design
    - efcore-data
    - backend-security
    - dotnet-testing
    - cognitive-doc-design
correction_rerun:
  prior_attempt: failed gatekeeper review
  findings_resolved:
    - "[CRITICAL] UploadEventImage past-event 404 → added includeExpired:true remediation"
    - "[WARNING] ReservationService missing Event fetch → .Include(t => t.Event) added"
    - "[WARNING] GlobalExceptionHandler ProblemDetails shape → option (a) Type='event-expired' added"
    - "[WARNING] CreateEventAsync/UpdateEventAsync DateTime.UtcNow ambiguity → migrated to _clock.GetUtcNow()"
    - "[SUGGESTION] Payment route create-preference → fixed in all diagrams"
    - "[SUGGESTION] catalog-filtering spec scenario title misleading → noted in Open Questions (spec not edited)"
    - "[SUGGESTION] Result Contract front-matter → added at top"
```

# Design: Hide expired events from buyers + block purchase

> **Corrective re-run.** This design amends the prior attempt that failed gatekeeper review. Five findings were resolved in place (CRITICAL: `UploadEventImage`; WARNING: `ReservationService` Event fetch, `GlobalExceptionHandler` shape, `CreateEventAsync`/`UpdateEventAsync` clock; SUGGESTION: route, spec note, Result Contract front-matter). ADRs, file structure, and sequence diagrams retained; only the named defects fixed. No wholesale rewrite.

## Technical Approach

Defense-in-depth using `Event.Date < asOf` as the single rule, applied at three independent gates so any one failure does not restore purchasability: (1) DB-level `WHERE` on public catalog and detail queries (EHE-002/003), (2) a service-level guard in `ReservationService` (EHE-004), (3) a service-level guard in `PaymentService.CreatePaymentPreferenceAsync` (EHE-005). Confirmed-payment ticket issuance (`ProcessApprovedPaymentAsync`) and existing accesses (TicketLookup, My Tickets, organizer dashboard via `/metrics/organizer`, admin endpoints, HMAC-signed QR) are untouched (EHE-006/007/008/011). All gates are gated by a single typed `IOptions<HideExpiredEventsOptions>` flag (EHE-009); the backend is the sole authority (EHE-010). No schema change, no migration. Time is read through `TimeProvider` so the 13:59→14:00 race scenario (EHE-004/005) is deterministically freezable in tests. Every organizer/staff `EventOwnership` / staff-role action has been audited (see "Exhaustive caller analysis" in ADR-1) so the filter never breaks a management workflow.

## Architecture Decisions

### ADR-1 — Management endpoint shape (incl. exhaustive caller analysis)

| Context | Decision | Consequences | Rejected |
|---|---|---|---|
| `EventController.GetAllEvents` `[AllowAnonymous]` (line 24) and `GetEvent` `[AllowAnonymous]` (line 32) become filtered. `OrganizerEventDetail.jsx:17` uses `useEvent(id)` → `GET /events/:id`; `StaffScan.jsx:113-135` uses `GET /events` for the scan chooser. Both need an unfiltered variant. | Add two role-gated actions to `EventController` (no new controller): `GET /api/events/manage` `[Authorize(Policy="RequireStaffRole")]` (list, for StaffScan) and `GET /api/events/{id}/manage` `[Authorize(Policy="EventOwnership")]` (detail, owner-or-admin, for OrganizerEventDetail edit). The GUID route constraint disambiguates the `manage` literal from `{id:guid}`. Service gets `bool includeExpired = false` overloads; authz lives entirely in the policy layer (per `aspnet-api-design` hard rule). | Two new hooks (`useManagementEvents`, `useManagementEvent`) + 2 page-level swaps. `OrganizerDashboard` already uses `/metrics/organizer`, not `/events`, so unaffected. | (b) `?includeExpired=true` on the `[AllowAnonymous]` endpoints — forces a manual role check inside the action body, violating "reuse `[Authorize(Policy=...)]`" and mixing public/role-gated semantics on one route. (c) A parallel `ManagementEventsController` + `/api/management/events` — doubles controller surface; overkill. |

**Exhaustive caller analysis** (re-grep of `backend/Controllers/*.cs` for `[Authorize(Policy=...)]` + `GetEventByIdAsync` / `GetAllPublishedEventsAsync`):

| Controller action | Policy | Service call | Remediation |
|---|---|---|---|
| `EventController.GetAllEvents` (line 24) | `[AllowAnonymous]` | `GetAllPublishedEventsAsync()` default | KEEP default `false` (public catalog) |
| `EventController.GetEvent` (line 32) | `[AllowAnonymous]` | `GetEventByIdAsync(id)` default | KEEP default `false` (public detail) |
| `EventController.CreateEvent` (line 46) | `RequireOrganizadorRole` | `GetEventByIdAsync(createdEvent.Id)` default | KEEP default `false` — newly-created events are validated as future-dated (`request.Date > now`), never expired; response payload is safe |
| `EventController.UpdateEvent` (line 79) | `EventOwnership` | `GetEventByIdAsync(updatedEvent.Id)` default | **CHANGE to `GetEventByIdAsync(updatedEvent.Id, includeExpired: true)`** — an organizer editing a past event would otherwise get null → 500 *after* a successful update (EHE-006 violation) |
| `EventController.UploadEventImage` (line 161) | `EventOwnership` | `GetEventByIdAsync(id)` default | **CHANGE to `GetEventByIdAsync(id, includeExpired: true)`** (CRITICAL fix) — past-event image upload/replacement is an existing organizer workflow; default filter would 404 the existence check at line 176 |
| `MetricsController` line 30 (EventOwnership) | `EventOwnership` | (no call to GetEventByIdAsync/GetAllPublishedEventsAsync) | No change |
| `MetricsController` line 62 | `RequireOrganizadorRole` | (uses `MetricsService`, no call to affected methods) | No change |
| `AdminController` line 14 | `RequireAdminRole` | (uses `AdminService`, no call to affected methods) | No change |
| `TicketController` line 96 | `RequireStaffRole` | (no call to affected methods) | No change |
| `PaymentController` line 168 | `RequireAdminRole` | (no call to affected methods) | No change |

Result: **two** EventOwnership actions need `includeExpired:true` (`UpdateEvent`, `UploadEventImage`); one creator action is safe to keep default; the two new `/manage` actions already pass `includeExpired:true`; no other controller calls the affected methods under a role policy. EHE-006 is preserved.

### ADR-2 — `IsExpired` placement

| Context | Decision | Consequences | Rejected |
|---|---|---|---|
| EF Core 9 cannot translate a custom method call (`e.IsExpired(now)`) inside `IQueryable.Where`; it would client-evaluate or throw. The domain predicate must also be unit-tested in isolation (EHE-001) and reused by the two purchase guards (EHE-004/005). | Add `public bool IsExpired(DateTime asOf) => Date < asOf;` as an **instance method on `Event`** for domain use on already-materialized entities. Use the **inline** comparison `e.Date > asOf` directly in `.Where(...)` for DB-level queries. The two definitions are logically identical. | Domain intent is readable in the guards; the DB query stays translatable to a single PostgreSQL predicate. One truth, two call sites. | Static helper on `EventService` — same EF translation problem and less discoverable. A separate `IEventExpiryService` domain service — overengineered for one boolean. |

### ADR-3 — Clock source for testability (CREATE/UPDATE date validation migrated)

| Context | Decision | Consequences | Rejected |
|---|---|---|---|
| Strict TDD; `EventService`, `ReservationService`, `PaymentService` all hardcode `DateTime.UtcNow` today. The EHE-004/005 race scenario (reservation OK at 13:59 → payment preference at 14:01 → guard rejects) requires freezing and advancing the clock deterministically, AND `PaymentService`'s existing reservation-token-expiry check (line 77) must read the same frozen clock or the race test goes flaky. AND `EventService.CreateEventAsync` line 61 (`request.Date <= DateTime.UtcNow` past-date validation) and line 80 (`var now = DateTime.UtcNow` for `CreatedAt`/`UpdatedAt`) and `UpdateEventAsync` would still read real time when tests freeze the clock — causing flaky creation/update of events near the fake clock. | Inject BCL `TimeProvider` (System, .NET 8+) into all three services; register `TimeProvider.System` as a singleton in `Program.cs`; replace `DateTime.UtcNow` with `_clock.GetUtcNow()` at: (a) EventService filter sites, (b) the new ReservationService/PaymentService expiry guards, (c) `PaymentService`'s existing reservation-token-expiry check (line 77) AND reservation-expiry check (line 102), **AND (d) `EventService.CreateEventAsync` line 61 (past-date validation) and line 80 (`var now`), AND `UpdateEventAsync`'s equivalent `now`/validation reads.** One clock for all of EventService. Tests use `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing` (test project only). | Update service constructors (signatures below); one new test-package dependency. No production behavior change — `TimeProvider.System.GetUtcNow()` is `DateTime.UtcNow` semantically. Test fixtures creating events near the frozen clock now behave deterministically. | Custom `IClock` interface — reinvents a BCL abstraction. `Func<DateTime>` seam — no DI lifecycle, no built-in fake. Leaving CreateEventAsync/UpdateEventAsync on `DateTime.UtcNow` — earlier design's choice; rejected here because the resulting test friction (real-time clock vs. fake-time services) outweighs the smaller diff, and consistency ("one clock per service") is cheaper to reason about. |

Constructor signatures:
```csharp
public EventService(ApplicationDbContext context, ILogger<EventService> logger,
    IConfiguration configuration, IAmazonS3 s3Client,
    IEventNotificationQueue notificationQueue,
    TimeProvider timeProvider,
    IOptions<HideExpiredEventsOptions> hideExpiredOptions)

public ReservationService(ApplicationDbContext context, ILogger<ReservationService> logger,
    IOptions<ReservationTokenOptions> tokenOptions,
    TimeProvider timeProvider,
    IOptions<HideExpiredEventsOptions> hideExpiredOptions)

public PaymentService(ApplicationDbContext context, IMercadoPagoClient mercadoPagoClient,
    IOptions<MercadoPagoOptions> options, IOptions<ReservationTokenOptions> tokenOptions,
    ITicketService ticketService, IEmailService emailService,
    ILogger<PaymentService> logger,
    TimeProvider timeProvider,
    IOptions<HideExpiredEventsOptions> hideExpiredOptions)
```

Race test seam:
```csharp
var fake = new FakeTimeProvider();
fake.SetUtcNow(new DateTime(2026,8,12,13,59,0,DateTimeKind.Utc));
// create reservation for event at 14:00 (EventService.IsExpired(13:59) → false) → 201
fake.Advance(TimeSpan.FromMinutes(2));   // now 14:01
// CreatePaymentPreferenceAsync → event.IsExpired(14:01) → throw EventExpiredException → 409
```

### ADR-4 — Feature flag scope granularity

| Context | Decision | Consequences | Rejected |
|---|---|---|---|
| EHE-009 mandates one runtime flag. Disabling purchase guard while keeping catalog filter on (or vice versa) produces a confusing partial state ("you can see it but can't buy it" or "you can't see it but reservation still works"), which has no operational use case. Rollback is all-or-nothing. | One global flag `HideExpiredEvents:Enabled` (bool, default `true`) bound to a typed `IOptions<HideExpiredEventsOptions>`. When `false`, every filter and guard becomes a no-op and the system reverts to pre-change behavior. Startup guard (`Program.cs`): the section MUST exist (fail-fast) — missing section throws `InvalidOperationException("HideExpiredEvents is not configured")`. Within the section, the property defaults to `true` (section present without `Enabled` → active). | Single mental model. No migration. Runtime toggle (no redeploy). Deployment smoke test only needs to verify one knob. | Per-surface `Catalog`/`Purchase` sub-flags — combinatorial ambiguity, partial states, violates YAGNI; no operator has asked for it. |

### ADR-5 — Purchase error code / HTTP status (GlobalExceptionHandler Option (a))

| Context | Decision | Consequences | Rejected |
|---|---|---|---|
| `GlobalExceptionHandler.MapException` (line 93-105) maps `ArgumentException`→400, `KeyNotFoundException`→404, `DbUpdateConcurrencyException`→409, `InvalidOperationException`→500 fallback. `ReservationController.CreateReservation` (line 80-104) catches `InvalidOperationException`→`Conflict(new {error})` (409) and "Insufficient tickets" `ArgumentException`→`Conflict(...)`. The existing 409 convention is for business-state conflicts; an "expired event" is a state conflict, not malformed input. Spec EHE-004 mandates RFC 7807 `ProblemDetails` with `type: "event-expired"`, not the bare `{ error }` shape. The handler's generic builder at lines 67-73 sets `Status`/`Title`/`Detail`/`Instance` but NEVER sets `Type`, so without action the fallback payload for `EventExpiredException` would lack the spec-required `type` field while the controller `Problem(...)` path produces it. | **409 Conflict** with RFC 7807 `ProblemDetails` produced via `ControllerBase.Problem(...)` inline in `ReservationController` and `PaymentController`. A new `EventExpiredException` is thrown by both guards. Each relevant controller adds `catch (EventExpiredException) { return Problem(...) }` BEFORE the generic `catch (Exception)`. **Belt-and-suspenders fallback path chosen: Option (a) — make the fallback spec-compliant.** In `GlobalExceptionHandler`, add a `MapException` case `EventExpiredException => (409, "EVENT_EXPIRED", "This event has already started and is no longer purchasable.")` AND special-case the `ProblemDetails` construction in `TryHandleAsync` so that for `EventExpiredException` it also sets `Type = "event-expired"` and overrides `Title = "Event has already started"` (matching the controller path). This guarantees the same JSON shape reaches the client whether the exception is caught at the controller or escapes to the middleware. | `Problem(...)` and the explicit fallback both produce `application/problem+json` with `type:"event-expired"`. One canonical exception type reused by both guards. Belt-and-suspenders now spec-compliant, not degraded. | Option (b) leaving the fallback without `type` — rejected. The fallback is reachable any time a future code path throws `EventExpiredException` outside a controller catch (e.g., a new endpoint, a refactor that drops the catch block, background-hosted code). "Currently unreachable in practice" is too brittle a justification for a spec-mandated field. 422 — not used anywhere in the project. 400 — reserved for `ArgumentException` input validation. 500 — incorrect: this is an expected business error. |

`Problem(...)` (controller path) and the `GlobalExceptionHandler` special-case (fallback path) both produce:
```
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type": "event-expired",
  "title": "Event has already started",
  "status": 409,
  "detail": "This event has already started and is no longer purchasable.",
  "instance": "/api/reservations"
}
```

## Data Flow

### Buyer catalog (flag ON)

```
Client ──GET /api/events──▶ EventController.GetAllEvents [AllowAnonymous]
                                    │
                                    ▼
                       EventService.GetAllPublishedEventsAsync(includeExpired:false)
                                    │
                       if (options.Enabled) append  .Where(e => e.Date > _clock.GetUtcNow())
                                    │
                                    ▼
                            ApplicationDbContext.Events  (SQL: WHERE "Date" > now)
                                    │
                                    ▼
                       MapToEventWithAvailabilityAsync (existing N+1-safe aggregation)
                                    │
                                    ◀── 200 OK (expired events absent)
```

### Buyer reservation on expired event → 409 (with new Event fetch)

```
Client ──POST /api/reservations──▶ ReservationController.CreateReservation
                                            │
                                            ▼
                  ReservationService.CreateReservationTransactionalAsync (tx + FOR UPDATE)
                                            │
                                  [NEW] Load TicketType WITH Event navigation:
                                        var tt = await _context.TicketTypes
                                            .Include(t => t.Event)            // [NEW] single round-trip
                                            .Where(t => t.Id == ticketTypeId && t.EventId == eventId)
                                            .FirstOrDefaultAsync();           // (or FromSqlInterpolated + .Include for Npgsql)
                                            │
                                            ▼
                                  if (tt == null) throw KeyNotFoundException
                                            │
                                  [NEW] var ev = tt.Event;                  // already loaded, no extra query
                                  [NEW] if (ev == null) throw KeyNotFoundException
                                            │
                                  [NEW] if (options.Enabled && ev.IsExpired(_clock.GetUtcNow()))
                                            │              throw new EventExpiredException()
                                            ▼ (rollback tx, no row inserted)
                              ReservationController catch (EventExpiredException)
                                            │
                                            ▼  return Problem(...)
                                            ◀── 409 application/problem+json
                                                  { type:"event-expired", title:"Event has already started", ... }
```

EF note: `.Include(t => t.Event)` on the existing TicketType query keeps the guard on a **single round-trip** (the TicketType row is already fetched; the Event navigation is the only new data, joined by EF Core in the same SQL). The alternative `_context.Events.FindAsync(eventId)` would add a **second** round-trip and a second lock-relevant query inside the same transaction. Choose `.Include`. Adjust the Npgsql `FromSqlInterpolated` branch to compose with `.Include` (EF Core supports `.Include` after `FromSqlInterpolated` via query continuation; if the provider refuses, fall back to a second `FindAsync` for Npgsql only — Tasks phase must smoke-test the SQL shape).

### Race 13:59 → 14:01 (`FakeTimeProvider`)

```
[TimeProvider = 2026-08-12 13:59:00 UTC]
Client ─POST /api/reservations─▶ ReservationService: ev.Date=14:00, asOf=13:59 → IsExpired=false
                                          │ reservation inserted (ExpiresAt = 13:59+10m = 14:09)
                                          ◀── 201 + reservation token

[TimeProvider = 2026-08-12 14:01:00 UTC]
Client ─POST /api/payments/create-preference─▶ PaymentService.CreatePaymentPreferenceAsync
                                          │ reservation still Active, ExpiresAt(14:09) > now(14:01) ✓
                                          │ token age (2 min) ≤ 10 min ✓
                                          │ GUARD: options.Enabled && ev.IsExpired(14:01) → true
                                          │   throw new EventExpiredException()
                                          ▼  PaymentController catch (EventExpiredException) → Problem(...)
                                          ◀── 409 { type:"event-expired", ... }
```

### Organizer / Staff management path

```
OrganizerEventDetail.jsx ──useManagementEvent(id)──▶ GET /api/events/{id}/manage
                                              [Authorize(Policy="EventOwnership")]  (owner-or-admin)
                                                            │
                                                            ▼
                              EventService.GetEventByIdAsync(id, includeExpired:true)
                                                            │  (no Date filter applied)
                                                            ◀── 200 OK (full event detail, ticket availability)

StaffScan.jsx ──useManagementEvents()──▶ GET /api/events/manage
                       [Authorize(Policy="RequireStaffRole")]  (staff + admin)
                                     │
                                     ▼
                  EventService.GetAllPublishedEventsAsync(includeExpired:true)
                                     │  (no Date filter applied)
                                     ◀── 200 OK (past + active events; staff picks one, scans QR)
                                     │
                                     │  scan itself uses the existing TicketLookup/QR endpoint (EHE-008 untouched)
```

### Organizer image upload on a past event (regression preserved)

```
Organizer (owner-or-admin) ─POST /api/events/{id}/image─▶ EventController.UploadEventImage
                                       [Authorize(Policy="EventOwnership")]
                                                  │
                                                  ▼
                            EventService.GetEventByIdAsync(id, includeExpired: true)   // [FIX] was default false
                                                  │  returns the past Event (no filter)
                                                  ▼
                            ReplaceEventImageAsync(id, userId, role, stream, ...)   // ownership re-checked here
                                                  ◀── 200 OK { imageUrl }  (EHE-006 preserved)
```

## File Changes

| File | Action | Delta | EHE |
|------|--------|-------|-----|
| `backend/Models/Event.cs` | Modify | Add `public bool IsExpired(DateTime asOf) => Date < asOf;` | 001 |
| `backend/Models/EventExpiredException.cs` | Create | New `EventExpiredException : Exception` thrown by both purchase guards | 004, 005 |
| `backend/Services/HideExpiredEventsOptions.cs` | Create | Typed options `{ public const string SectionName="HideExpiredEvents"; public bool Enabled { get; set; } = true; }` | 009 |
| `backend/Services/IEventService.cs` | Modify | Overloads `GetEventByIdAsync(Guid, bool includeExpired=false)` and `GetAllPublishedEventsAsync(bool includeExpired=false)`; default = public (filtered). | 002, 003, 006 |
| `backend/Services/EventService.cs` | Modify | Apply `e.Date > asOf` when `Enabled && !includeExpired`; inject `TimeProvider` + `IOptions<HideExpiredEventsOptions>`. **Replace ALL `DateTime.UtcNow` reads with `_clock.GetUtcNow()`:** (a) filter sites, (b) `CreateEventAsync` line 61 (`request.Date <= DateTime.UtcNow` past-date validation) AND line 80 (`var now`), (c) `UpdateEventAsync`'s equivalent `now`/validation reads. Rationale: one clock for all of EventService; deterministic under `FakeTimeProvider`. The `CreateEventAsync`/`UpdateEventAsync` business rule (reject past dates) is **unchanged** — only the time source moves. | 002, 003, 009 |
| `backend/Services/ReservationService.cs` | Modify | **[NEW] Add an Event fetch** in `CreateReservationTransactionalAsync`: apply `.Include(t => t.Event)` on the existing TicketType query (all three provider branches: Npgsql `FromSqlInterpolated` + `.Include`, SQLite, InMemory). Add `if (tt.Event == null) throw new KeyNotFoundException(...)` for safety. Then `if (_hideExpiredOptions.Value.Enabled && tt.Event.IsExpired(_clock.GetUtcNow())) throw new EventExpiredException();` BEFORE the stock check. Inject `TimeProvider` + `IOptions<HideExpiredEventsOptions>`. **One round-trip** for TicketType+Event; do NOT add a separate `FindAsync` unless Npgsql refuses `.Include` on `FromSqlInterpolated` (Tasks phase smoke-test). | 004, 009 |
| `backend/Services/PaymentService.cs` | Modify | In `CreatePaymentPreferenceAsync`, after reservation is validated as active, add the same guard (`reservation.TicketType.Event` must be loaded; if PaymentService's existing query does not `.Include(t => t.Event).ThenInclude(...)` or equivalent, ADD the `.Include` per the same single-round-trip rationale). Inject `TimeProvider` + `IOptions<HideExpiredEventsOptions>`; replace `DateTime.UtcNow` (line 77 token check, line 102 reservation-expiry check) with `_clock.GetUtcNow()` so the race test is deterministic. Leave `ProcessApprovedPaymentAsync` unchanged. | 005, 009, 011 |
| `backend/Controllers/EventController.cs` | Modify | Public `GetAllEvents`/`GetEvent` call overloads with `includeExpired:false` (default). Add `[HttpGet("manage")] [Authorize(Policy="RequireStaffRole")] GetAllEventsForManagement()` → `GetAllPublishedEventsAsync(includeExpired:true)`. Add `[HttpGet("{id:guid}/manage")] [Authorize(Policy="EventOwnership")] GetEventForManagement(id)` → `GetEventByIdAsync(id, includeExpired:true)`. Place `manage` route alongside `{id:guid}` (constraint disambiguates). **[FIX] `UpdateEvent` line 96: change `GetEventByIdAsync(updatedEvent.Id)` → `GetEventByIdAsync(updatedEvent.Id, includeExpired: true)`** (EventOwnership-gated; organizer editing past event must see the result, not 404/500). **[FIX] `UploadEventImage` line 175: change `GetEventByIdAsync(id)` → `GetEventByIdAsync(id, includeExpired: true)`** (EventOwnership-gated; past-event image workflow must not 404). `CreateEvent` line 63 stays on default (newly-created events are validated future-dated → never expired). | 002, 003, 006, 007 |
| `backend/Controllers/ReservationController.cs` | Modify | Add `catch (EventExpiredException) { return Problem(detail:"This event has already started and is no longer purchasable.", statusCode:409, title:"Event has already started", type:"event-expired"); }` above the generic `catch (Exception)` (line 105). | 004 |
| `backend/Controllers/PaymentController.cs` | Modify | Same `catch (EventExpiredException) { return Problem(...) }` in the `CreatePaymentPreference` action. | 005 |
| `backend/Middleware/GlobalExceptionHandler.cs` | Modify | Add belt-and-suspenders `MapException` case `EventExpiredException => (409, "EVENT_EXPIRED", "This event has already started and is no longer purchasable.")`. **[FIX] Special-case the `ProblemDetails` construction in `TryHandleAsync` so that for `EventExpiredException` it sets `Type = "event-expired"` and `Title = "Event has already started"`** (overriding the `errorCode` title) — Option (a) keeps the fallback spec-compliant with `purchase-guards` (`type: "event-expired"`). | 004, 005 |
| `backend/Program.cs` | Modify | `var eheSection = builder.Configuration.GetSection("HideExpiredEvents"); if (!eheSection.Exists()) throw new InvalidOperationException("HideExpiredEvents configuration section is required"); builder.Services.Configure<HideExpiredEventsOptions>(eheSection); builder.Services.AddSingleton(TimeProvider.System);` | 009 |
| `backend/appsettings.json` | Modify | Add `"HideExpiredEvents": { "Enabled": true }`. | 009 |
| `frontend/src/hooks/useManagementEvent.js` | Create | Like `useEvent(id)` but `GET /events/:id/manage`; no `includeExpired` flag (authz is on the server). | 006 |
| `frontend/src/hooks/useManagementEvents.js` | Create | Like `useEvents()` but `GET /events/manage`. | 007 |
| `frontend/src/pages/OrganizerEventDetail.jsx` | Modify | Switch `useEvent(id)` → `useManagementEvent(id)`. | 006 |
| `frontend/src/pages/StaffScan.jsx` | Modify | Switch `useEvents()` → `useManagementEvents()` for the chooser; scan endpoint unchanged. | 007 |
| `frontend/src/pages/EventDetail.jsx` | Modify (optional) | Client-side "event expired" banner from `event.Date` / on 404; manual verification only. | 010 |
| `backend/Tests/EventServiceTests.cs` | Modify | New tests (see Test Plan). | 001, 002, 003, 009 |
| `backend/Tests/EventControllerTests.cs` | Modify | New tests incl. 404-for-expired, management variant role checks, **`UploadEventImage_PastEvent_Succeeds_ForOrganizer`**, **`UpdateEvent_PastEvent_IncludeExpired_200`**. | 002, 003, 006, 007 |
| `backend/Tests/ReservationControllerTests.cs` | Modify | 409 ProblemDetails for expired, race scenario, **no second DB round-trip** assert (assert EventLoaded via Include). | 004, 009 |
| `backend/Tests/ReservationStockTests.cs` | Modify | No reservation row persisted when guard throws. | 004 |
| `backend/Tests/ReservationServiceTests.cs` | Modify | `EventExpiredException` thrown; active event succeeds; **Event loaded via `.Include(t => t.Event)`** (single round-trip). | 004 |
| `backend/Tests/PaymentServiceWebhookTests.cs` | Modify | Expiry guard rejections, race-after-expiry, EHE-011 regression (`ProcessApprovedPaymentAsync` unchanged). | 005, 011 |

## Interfaces / Contracts

```csharp
// backend/Models/Event.cs (delta)
public bool IsExpired(DateTime asOf) => Date < asOf;

// backend/Models/EventExpiredException.cs (new)
public class EventExpiredException : Exception
{
    public EventExpiredException() : base("Event has already started") { }
}

// backend/Services/HideExpiredEventsOptions.cs (new)
public class HideExpiredEventsOptions
{
    public const string SectionName = "HideExpiredEvents";
    public bool Enabled { get; set; } = true;
}

// IEventService delta — new overloads (existing signatures keep the default false)
Task<EventWithAvailability?> GetEventByIdAsync(Guid eventId, bool includeExpired = false);
Task<IEnumerable<EventWithAvailability>> GetAllPublishedEventsAsync(bool includeExpired = false);

// GlobalExceptionHandler.TryHandleAsync delta — Option (a) spec-compliant fallback
// after `var problemDetails = new ProblemDetails { ... }`:
if (exception is EventExpiredException)
{
    problemDetails.Type = "event-expired";
    problemDetails.Title = "Event has already started";   // override errorCode title
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (domain) | `Event.IsExpired(asOf)` — past/future/exact instant | xUnit `[Theory]`, no DB, no Moq |
| Unit (service) | `EventService` filter behavior with flag ON/OFF; `GetEventByIdAsync` public vs `includeExpired:true`; **`CreateEventAsync` past-date rejection uses `_clock.GetUtcNow()`** (freeze clock at T, request.Date=T+1s → succeeds; T-1s → `ArgumentException`); `UpdateEventAsync` equivalent | xUnit + InMemory DB + `FakeTimeProvider` |
| Unit (service) | `ReservationService` guard throws `EventExpiredException`, no row persisted; flag OFF → no throw; **Event loaded via `.Include(t => t.Event)` (assert: no second `Select` query issued — `Assert.Single(contextTracker??)` or `IEventService` spy)**; TicketType null → `KeyNotFoundException`; Event navigation null → `KeyNotFoundException` | xUnit + InMemory + `FakeTimeProvider` + `IOptions<HideExpiredEventsOptions>` mock |
| Unit (service) | `PaymentService` guard rejects on expired event; race-after-expiry rejects; `ProcessApprovedPaymentAsync` unaffected (EHE-011 regression) | xUnit + InMemory + `FakeTimeProvider`, Mock `IMercadoPagoClient` |
| Integration (HTTP) | `GET /api/events`, `GET /api/events/{id}` public behavior; `/api/events/manage` and `/api/events/{id}/manage` role gating (401 anon, 403 wrong role, 200 owner/staff) | `WebApplicationFactory<Program>` with a `FakeTimeProvider` registered via a test host modifier |
| Integration (HTTP) | `POST /api/reservations` and `POST /api/payments/create-preference` return 409 `application/problem+json` with `type=event-expired` (assert JSON field explicitly); fallback path: also assert `GlobalExceptionHandler` payload for a deliberately-escaped `EventExpiredException` carries `type=event-expired` | `WebApplicationFactory<Program>` + assert ProblemDetails JSON |
| Integration (HTTP) | **[NEW] `POST /api/events/{id}/image` on a past event with an organizer/admin owner → 200 OK** (`UploadEventImage_PastEvent_Succeeds_ForOrganizer`); anon → 401; non-owner → 403 | `WebApplicationFactory<Program>` with `FakeTimeProvider` pinned past the seeded event's Date |
| Integration (HTTP) | **[NEW] `PUT /api/events/{id}` on a past event (organizer/admin owner) → 200 OK** (`UpdateEvent_PastEvent_IncludeExpired_200`); the post-update `GetEventByIdAsync(updatedEvent.Id, includeExpired:true)` must return the entity, not 404/500 | `WebApplicationFactory<Program>` |

New test method names mapped to spec scenarios (representative; full mapping in tasks phase):

| Test method | Spec scenarios |
|---|---|
| `Event_IsExpired_Future_False` / `_Past_True` / `_ExactInstant_False` | EHE-001 (3) |
| `GetAllPublished_FlagEnabled_ExcludesExpired` / `_AllExpired_Empty` / `_MixOrderIndependent` | EHE-002 (3) |
| `GetEventById_Public_Expired_Null` / `_Active_200` / `_SameDayAfterStart_404` / `_ManagementIncludeExpired_200` | EHE-003 (4) |
| `CreateReservation_Expired_Returns409ProblemDetails` / `_Active_201` / `_Race_13_59_to_14_01_409` / `_FlagDisabled_Succeeds` | EHE-004 (3) + EHE-009 |
| `CreateReservation_EventLoadedViaInclude_SingleRoundTrip` | EHE-004 (implementation invariant) |
| `CreatePaymentPreference_Expired_ThrowsEventExpiredException` / `_Active_Succeeds` / `_RaceAfterExpiry_Throws` / `ProcessApprovedPayment_ExpiredEvent_ProducesTickets` | EHE-005 (3) + EHE-011 |
| `UploadEventImage_PastEvent_Succeeds_ForOrganizer` / `UploadEventImage_PastEvent_Succeeds_ForAdmin` / `_Anon_401` / `_NonOwner_403` | EHE-006 (regression for the gatekeeper's CRITICAL finding) |
| `UpdateEvent_PastEvent_IncludeExpired_200` / `UpdateEvent_PastEvent_NonOwner_403` | EHE-006 (regression covers the second EventOwnership caller) |
| `Organizer_ManagementEvent_Expired_200` / `Staff_ManagementList_IncludesExpired` / `Staff_ManagementList_Anon_401` | EHE-006, EHE-007 |
| `Flag_MissingSection_FailsFast` / `Flag_DefaultTrue` / `Flag_Disabled_PurchaseOpen` | EHE-009 (6) |
| `CreateEvent_PastDate_FrozenClock_AnyException` / `CreateEvent_FutureDate_FrozenClock_Succeeds` / `UpdateEvent_PastDate_FrozenClock_AnyException` | ADR-3 (clock migration regression for CreateEventAsync/UpdateEventAsync) |
| `GlobalExceptionHandler_EventExpiredException_PayloadHasTypeEventExpired` | ADR-5 Option (a) — fallback path is spec-compliant |
| `EventController_PublicDetail_Expired_404` / `_Active_200` (regression) | EHE-010 backend authority |

### Race scenario freeze

Implemented via `FakeTimeProvider` (NuGet: `Microsoft.Extensions.Time.Testing` added to `backend/Tests`). The same instance is injected into `EventService`, `ReservationService`, and `PaymentService` in the test fixture. `SetUtcNow` / `Advance` move the single shared clock. Because `CreateEventAsync`/`UpdateEventAsync` now read `_clock.GetUtcNow()` (ADR-3), seeding an event at `T+10d` against a frozen clock at `T` is deterministic — no real-time bleed-through.

### NOT backend-testable (manual verification)

- `frontend-ux-optional` and `frontend-cannot-bypass` (EHE-010 scenarios 3-4): the optional client-side "Event expired" banner in `EventDetail.jsx` is manual smoke-test only. The **enforcement** is verified by backend tests (`backend-test-catalog`, `backend-test-purchase`); tampering with the frontend still yields 404/409 from the backend.
- Metadata: `OrganizerDashboard`, `AdminPanel`, `MetricsService` paths are already role-gated and unfiltered — confirmed via Explore; no new tests required for EHE-006/007/008 (regression-only).

## Migration / Rollout

No DB migration. Frontend ships new hooks + 2 page swaps atomically with the backend so management variants are available before the public endpoints start filtering.

Rollback (EHE-009 + Rollback Plan):
1. Set `HideExpiredEvents:Enabled=false` in `appsettings.json` (no redeploy). All filters and guards become no-ops; `Event.IsExpired` is still defined but unused; controllers still expose `/manage` variants and the `UploadEventImage`/`UpdateEvent` `includeExpired:true` calls harmlessly (the flag only governs the filter, not the new overloads).
2. Code revert: remove guard lines, remove `Where` clauses, remove `/manage` actions, remove the `includeExpired:true` calls on `UploadEventImage`/`UpdateEvent` (or leave them — harmless post-revert since the overload remains), remove new options/exception/TimeProvider registration, remove frontend hook swaps, drop the test package. No schema to revert.

## Cross-cutting Concerns

- **`Program.cs` flag binding**: typed `IOptions<HideExpiredEventsOptions>` bound via `Configure<HideExpiredEventsOptions>(Configuration.GetSection("HideExpiredEvents"))`. Startup guard mirrors the existing `GetRequiredValue` pattern: if `Configuration.GetSection("HideExpiredEvents").Exists()` is false → `throw new InvalidOperationException("HideExpiredEvents configuration section is required")`. Default `true` is satisfied by the property initializer on the options class (section present without `Enabled` → active).
- **`AdminService.GetAllEventsAsync` and `MetricsService.GetOrganizerMetricsAsync`**: confirmed NO new flag behavior — both already run role-gated on separate code paths (`AdminController` `[Authorize(Policy="RequireAdminRole")]`; `MetricsController` `[Authorize(Policy="RequireOrganizadorRole")]`), neither calls `GetAllPublishedEventsAsync` nor `GetEventByIdAsync`. They keep returning all events.
- **`ReservationExpirationService` and `EventNotificationDispatchService`**: confirmed unaffected (Explore artifact). No `TimeProvider` injection in this change.
- **Frontend hooks**: `useEvent` and `useEvents` stay role-agnostic and continue to hit the public (filtered) endpoints. New role-aware variants `useManagementEvent` and `useManagementEvents` are used ONLY inside `OrganizerEventDetail.jsx` and `StaffScan.jsx`. No shared client wrapper needs to know about roles — the role gating is fully server-side via the policies.
- **DataLoader / BFF safeguards**: no GraphQL/BFF layer in this codebase (verified during Explore). No additional catalog surface to update.

## Open Questions

- [ ] **Spec inconsistency, to be reconciled in `sdd-verify` or `sdd-tasks` (design phase must not edit specs):** `openspec/changes/hide-expired-events/specs/catalog-filtering/spec.md` line 28 scenario title reads "Event at exact start instant is expired (strict less-than)" but the scenario's THEN clause asserts the event is **not** expired (consistent with `Event.IsExpired(asOf) => Date < asOf` — at the exact instant `Date == asOf`, `Date < asOf` is false). The title's "is expired (strict less-than)" is misleading; a strict-`<` predicate makes the exact-start-instant result NOT expired. Recommend retitling the scenario to "Event at exact start instant is **not** expired (strict less-than: `Date == asOf` → `false`)" in a verify/tasks pass. **Design does not edit the spec file.**
- [ ] Npgsql: confirm `.Include(t => t.Event)` composes with `FromSqlInterpolated($"SELECT ... FOR UPDATE")` (EF Core 9/Npgsql). If it does not, fall back to a second `FindAsync` for the Npgsql branch ONLY and document the second round-trip. Tasks phase smoke-test the generated SQL.
- [ ] PaymentService: confirm whether its existing reservation-load query already `.Include`s the Event navigation. If not, add `.Include` per the same single-round-trip rationale (file-changes note above already assumes adding it; verify).

## Implementation Risks for Tasks Phase

| Risk | Severity | Note for tasks phase |
|---|---|---|
| EF translation gotcha: writing `e.IsExpired(now)` inside `Where` client-evaluates. | Medium | Enforce inline `e.Date > asOf`. Add a regression test asserting the query executes server-side (assert no client-evaluation warning; for Npgsql, just assert correct results on a real-like dataset). |
| Test fixtures need pre-seeded future dates so existing event tests don't accidentally flip into "expired" as real time advances. | Medium — **reduced** by ADR-3 migration of `CreateEventAsync`/`UpdateEventAsync` to `_clock.GetUtcNow()`. | Audit each fixture in `EventServiceTests`/`EventControllerTests`; use dates clearly in the future (e.g., `_clock.GetUtcNow().AddYears(1)`) or use the `FakeTimeProvider` to pin time. Now that CreateEvent reads the fake clock, seeding via `CreateEventAsync` is also deterministic. |
| `ReservationController.CreateReservation` has a generic `catch (Exception) → 500` (line 105) that would swallow `EventExpiredException`. | High | The new `catch (EventExpiredException)` MUST be placed BEFORE line 105's generic catch. Verify by inspecting the catch order after the edit. |
| `PaymentController` may have the same generic-catch pattern. | Medium | Inspect PaymentController's `CreatePaymentPreference` action and place the new catch before its generic fallback. |
| Route precedence between `[HttpGet("manage")]` and `[HttpGet("{id:guid}")]`. | Low | The `{id:guid}` constraint rejects `manage` (not a GUID), so ASP.NET Core does not ambiguously match. Add an explicit test requesting `/api/events/manage` to confirm it routes to the list action. |
| `Microsoft.Extensions.Time.Testing` package must be added to the test project only (not main project). | Low | Confirm via `dotnet test` after adding the test package reference. |
| `.Include(t => t.Event)` on `FromSqlInterpolated(... FOR UPDATE)` for Npgsql may not compose (provider limitation). | Medium | Tasks phase: smoke-test the generated SQL; if Npgsql refuses `.Include` after `FromSqlInterpolated`, fall back to `_context.Events.FindAsync(eventId)` inside the Npgsql branch (second round-trip, acceptable fallback). |
| `GlobalExceptionHandler` special-case for `EventExpiredException` diverges from the existing uniform `ProblemDetails` shape. | Low | Special-case is minimal (only sets `Type` and overrides `Title`); keep it confined to a single `if (exception is EventExpiredException)` block after the generic builder so the rest of the handler stays untouched. |
| Two `EventOwnership` actions (`UpdateEvent`, `UploadEventImage`) silently changed to `includeExpired:true`; a future refactor that adds a *third* `EventOwnership` action may forget to do the same. | Medium | Add a convention note in `aspnet-api-design` skill or as an inline code comment: "Any `[Authorize(Policy="EventOwnership")]` action that loads an event for editing/management MUST call `GetEventByIdAsync(id, includeExpired: true)`." Add a unit test asserting both currently-remediated actions return 200 for past events. |