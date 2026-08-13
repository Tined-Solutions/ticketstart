# Proposal: Hide expired events from buyers + block purchase

## Intent

Buyers must never see or buy tickets for events whose start time has passed. Today an event stays listed and purchasable forever after creation. This change makes `Event.Date < DateTime.UtcNow` the single rule that hides past events from the public catalog and blocks purchase at reservation and payment, while preserving every existing access (buyer ticket lookup, organizer edit, staff scan).

## Motivation

`EventService.CreateEventAsync` rejects past dates at creation, but no runtime filter exists. `GetAllPublishedEventsAsync` and `GetEventByIdAsync` return every event regardless of date, and `ReservationService` / `PaymentService` never check the event date. An event that started at 14:00 is still on the catalog and buyable at 23:00, days, or months later.

## Scope

### In scope
- Domain predicate `IsExpired(asOf)` on `Event` (no new column, no migration). #[EHE-001](#requirements)
- DB-level filter excluding expired events from buyer-facing queries (`GetAllPublishedEventsAsync`, `GetEventByIdAsync`). #[EHE-002](#requirements), #[EHE-003](#requirements)
- Purchase guards in `ReservationService.CreateReservationTransactionalAsync` and `PaymentService.CreatePaymentPreferenceAsync` (defense-in-depth). #[EHE-004](#requirements), #[EHE-005](#requirements)
- Role-gated path so organizer and staff keep seeing/scanning expired events. #[EHE-006](#requirements), #[EHE-007](#requirements)
- Buyer access to already-purchased tickets for past events via TicketLookup and "My Tickets" stays intact. #[EHE-008](#requirements)
- Runtime feature flag `HideExpiredEvents` to disable without redeploy. #[EHE-009](#requirements)
- Backend is the single source of truth; frontend gets optional "event expired" UX. #[EHE-010](#requirements)

### Out of scope (non-goals)
- No "past events" archive section in the catalog.
- No sales cutoff before `Date` (no N-minutes lead time; cutoff is exactly at `Date`).
- No `EndDate` column or duration modeling (events stay point-in-time).
- No admin/organizer dashboard expiry filter (admin already sees all).
- No frontend test runner change (frontend is visual-only; backend-authoritative).
- No changes to `ReservationExpirationService`, `EventNotificationDispatchService`, or `ProcessApprovedPaymentAsync` (confirmed payments still produce tickets).

## Assumptions

1. "Past" = `Event.Date < DateTime.UtcNow` strictly; duration is irrelevant. An event starting at 20:00 is expired at 20:01.
2. An in-progress event TODAY is not purchasable; once the start instant passes, buyer purchase is blocked.
3. Purchase cutoff is exactly `Date`; no pre-cutoff lead time.
4. Past events are fully hidden from the public catalog: not listed, not returned by public endpoints. No archive section.
5. Existing accesses are inviolable: buyers keep seeing/QR-ing their purchased tickets for past events; organizers keep seeing/editing past events in their dashboard; staff keep scanning QR of past events. The technical form (management endpoint vs `?includeExpired=true` role-gated flag) is a DESIGN decision.
6. Hybrid approach: domain predicate + DB-level filter on buyer queries + purchase guards in `ReservationService` and `PaymentService` + role-gated path for organizer/staff. No DB migration.

## Requirements

IDs reference specs that will be created as `openspec/changes/hide-expired-events/specs/EHE-xxx/spec.md` in the spec phase.

- **EHE-001** — `Event` SHALL expose `bool IsExpired(DateTime asOf)` returning `Date < asOf`, unit-testable in isolation.
- **EHE-002** — `GetAllPublishedEventsAsync` MUST exclude events where `Date < DateTime.UtcNow` at the DB query level (single `Where` clause), so expired events never appear in `GET /api/events`.
- **EHE-003** — `GetEventByIdAsync` invoked from the public endpoint (`GET /api/events/{id}`) MUST return null/404 for expired events. A role-gated management variant (separate method or `includeExpired` parameter) MUST exist for organizer/admin use.
- **EHE-004** — `ReservationService.CreateReservationTransactionalAsync` MUST reject reservation creation for an expired event with a `ProblemDetails` error (e.g., 409 Conflict / "event expired").
- **EHE-005** — `PaymentService.CreatePaymentPreferenceAsync` MUST reject payment preference creation for an expired event (defense-in-depth for the race where a reservation was created just before expiry).
- **EHE-006** — Organizer endpoints (`OrganizerDashboard`, `OrganizerEventDetail`, `MetricsService.GetOrganizerMetricsAsync`) MUST continue to return past events so organizers can view and edit them.
- **EHE-007** — Staff scan endpoint(s) used by `StaffScan.jsx` MUST continue to allow scanning QR of past events; staff role-gated path MUST include expired events.
- **EHE-008** — `TicketLookup` and "My Tickets" buyer endpoints MUST remain unaffected: a buyer who already purchased tickets to a now-expired event SHALL still retrieve their tickets and QR.
- **EHE-009** — A runtime feature flag `HideExpiredEvents` (typed `IOptions`, `appsettings.json`) SHALL gate all filtering and purchase guards. When disabled, the system behaves as today; toggling does not require redeploy.
- **EHE-010** — The backend is the authority for expiry; the frontend MAY add optional client-side "event expired" UX but MUST NOT be the enforcement point.
- **EHE-011** — `ProcessApprovedPaymentAsync` MUST remain unchanged: a payment already confirmed for a (now-expired) event still produces tickets.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Models/Event.cs` | New — adds `IsExpired(DateTime asOf)` method |
| `backend/Services/EventService.cs` | Modified — filter in `GetAllPublishedEventsAsync`; `GetEventByIdAsync` dual-use split for buyer vs management |
| `backend/Services/ReservationService.cs` | Modified — purchase guard in `CreateReservationTransactionalAsync` |
| `backend/Services/PaymentService.cs` | Modified — purchase guard in `CreatePaymentPreferenceAsync` |
| `backend/Controllers/EventController.cs` | Modified — public detail returns 404 for expired; management variant gated by role policy |
| `backend/Controllers/MetricsController.cs`, `AdminService.cs` | Unaffected — already separate role-gated paths |
| `backend/Tests/*` | Modified — expired-event tests across EventService, EventController, ReservationController, ReservationStock, PaymentService |
| `frontend/src/pages/EventDetail.jsx`, `EventList.jsx` | Optional — client-side expired UX (backend-authoritative) |
| `frontend/src/pages/OrganizerEventDetail.jsx`, `StaffScan.jsx` | Modified — switch to management/staff path so expired events remain visible |
| `backend/Program.cs`, `appsettings.json` | New — `HideExpiredEvents` feature flag binding |

## Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Race condition at event start: checkout started at 13:59, event expires at 14:00 mid-form | Medium | `ReservationService` guard is the final authority; frontend may show countdown; rejected reservation returns clear 409 |
| `GetEventByIdAsync` dual-use breaks organizer edit / staff scan of past events | Medium | Introduce management variant or `includeExpired` role-gated parameter; spec phase locks the contract |
| Feature flag left disabled in production by mistake | Medium | Default `true` in `appsettings.json`; config guard fail-fast if key missing; smoke test verifies filtering on deploy |
| Webhook `ProcessApprovedPaymentAsync` tied to a just-expired event gets rejected | Low | EHE-011 keeps webhook unchanged; only pre-payment reservation/preference creation is guarded |
| Existing tests break if they pre-create events with past dates | Low | `CreateEventAsync` already rejects past dates; tests use future dates, so unaffected. Verify in spec phase. |
| Frontend treats backend as decorative; organizer pages silently empty past events | Low | Backend is authority (EHE-010); frontend changes only switch to management endpoints |

## Rollback Plan

1. Set `HideExpiredEvents=false` in `appsettings.json` (runtime; no redeploy). All filters and purchase guards become no-ops; system behaves as pre-change. Confirms rollback without code change.
2. If a code rollback is required: revert the commits introducing `IsExpired`, the `Where` clauses in `EventService`, the guards in `ReservationService`/`PaymentService`, the management endpoint split, and the flag binding. No DB migration to revert (no schema change).
3. Confirm post-rollback: expired events reappear in `GET /api/events`, public detail returns 200, reservation/payment succeed for expired events; organizer and staff paths unchanged.
4. Communicate to organizers that past-event purchase is possible again until a corrected release ships.

## Dependencies

- Existing authorization policies `EventOwnership`, `RequireOrganizadorRole`, `RequireStaffRole`, `RequireAdminRole` (reused, no new policy).
- `DateTime.UtcNow` as the clock source (injectable `IClock` deferred to design for testability).

## Success Criteria

- [ ] `GET /api/events` and `GET /api/events/{id}` (public) return no expired events; existing buyer ticket lookups still work.
- [ ] `POST /api/reservations` and `POST /api/payments/preference` for an expired event return a clear expiry error.
- [ ] Organizer dashboard, organizer event detail, and staff scan continue to show and operate on past events.
- [ ] `HideExpiredEvents=false` disables all filtering and guards at runtime with no redeploy.
- [ ] Backend test suite (`dotnet test`) green with new expired-event tests covering listing, detail, reservation, and payment guards.

## Open questions for design

- **Management endpoint shape**: separate `GET /api/events/{id}/manage` (role-gated) vs. `GET /api/events/{id}?includeExpired=true` with role check? Affects controller surface and frontend client.
- **`IsExpired` placement**: instance method on `Event` vs. static helper on `EventService` vs. domain service. Affects unit-test shape and reuse.
- **Clock source**: introduce `IClock`/`TimeProvider` abstraction for testability, or keep `DateTime.UtcNow` and wrap in a thin internal `Func<DateTime>`? Affects how strict-TDD tests freeze time.
- **Flag scope granularity**: one global `HideExpiredEvents`, or per-surface (`HideExpiredEvents:Catalog`, `HideExpiredEvents:Purchase`)? Affects operational rollback precision.
- **Purchase error code/HTTP status**: 409 Conflict vs. 422 Unprocessable Entity vs. 400 Bad Request for "event expired" — must align with existing `ProblemDetails` conventions in `GlobalExceptionHandler`.