# Proposal: Past Events Read-Only (Event Immutability)

## Intent (Problem)

Today the codebase *explicitly supports* mutating past events: `UpdateEventAsync`, `DeleteEventAsync`, `ReplaceEventImageAsync`, `AddTicketStockAsync`, `AddTicketTypeAsync`, `ApproveEventAsync`, and `RejectEventAsync` perform **no date guard**. The `includeExpired: true` comments at `EventController.cs:135-137` (PUT) and `:216-218` (image) exist precisely so an organizer editing a past event gets a result instead of a 404. EHE-006 (`role-access`) even *mandates* that organizers "see and edit their past events exactly as before". There is also no admin/organizer read-only consultation view (EventDetail is public-only). This change freezes event mutation once an event's `Date` has passed, while keeping consultation fully available.

## Goals

- Make a past event **immutable** for both Admin and Organizer — backend is authoritative.
- Keep consultation working: view detail + view purchases.
- Preserve payments-domain operations on past-event tickets (refunds, purchase views).
- Add a read-only "Ver" consultation view for admin/organizer (none today).
- Surface a clear, consistent error (409 Conflict) when a mutation targets a past event.

## Non-Goals

- **Purchases/refunds are OUT** — they live in the payments domain and MUST keep working (carve-out).
- **Frontend redesign is OUT** — no reskin of AdminPanel/OrganizerDashboard/EventForm.
- **Public EventDetail buyer flow is NOT touched** beyond what the consultation view reuses.
- Decreasing ticket stock / editing existing ticket-type price/name (unchanged from `admin-ticket-stock`).
- Notification/email on past events (no new buyer comms).
- **Flag-gating the immutability rule** — the rule is HARD, independent of `HideExpiredEvents`.

## Business Rules

- **Immutability rule (HARD):** An event whose `Date < server UTC now` is read-only for Admin AND Organizer. Forbidden: editing content (PUT), delete, image upload/replace, add ticket stock, add ticket type, approve, reject. Independent of `HideExpiredEvents` (which only gates read filters + purchase guards per EHE-009).
- **Consultation carve-out:** GET on past events (management detail, purchases) MUST stay working. Green tests `GetEventById_ManagementIncludeExpired_200`, `Organizer_ManagementEvent_Expired_200` must not break.
- **Payments carve-out:** `AdminController.GetPurchases` and `RefundPurchase` → `AdminPurchaseService` (separate payments-domain service) are untouched.
- **Guard ordering:** The guard MUST throw BEFORE any save, audit, or notification side-effect (esp. `UpdateEventAsync` date-change buyer emails, EventService.cs:509-575; EDC-001 path becomes unreachable for past events).
- **Authoritative source:** Backend is authoritative; client `isPast` is cosmetic defense-in-depth.

## Scope

### In Scope

- **Backend mutation guard** on every listed endpoint, throwing 409 before any save/audit/notification:
  - `PUT /api/events/{id}` → `UpdateEventAsync` (EventService.cs:458)
  - `DELETE /api/events/{id}` → `DeleteEventAsync` (EventService.cs:585)
  - `POST /api/events/{id}/image` → `ReplaceEventImageAsync` (EventService.cs:727)
  - `POST /admin/events/{eventId}/ticket-types/{ticketTypeId}/stock` → `AddTicketStockAsync` (EventService.cs:290)
  - `POST /admin/events/{eventId}/ticket-types` → `AddTicketTypeAsync` (EventService.cs:368)
  - `POST /admin/events/{eventId}/approve` → `AdminService.ApproveEventAsync` (AdminService.cs:104)
  - `POST /admin/events/{eventId}/reject` → `AdminService.RejectEventAsync` (AdminService.cs:121)
- Shared, testable guard helper: evaluate `eventEntity.IsExpired(_clock.GetUtcNow().UtcDateTime)` on the **materialized entity** (never inside `IQueryable` — EF cannot translate). Inject `TimeProvider` into `AdminService` (currently has no clock, AdminService.cs:13-20).
- **AdminPanel.jsx:** add a read-only "Ver" action per event; for past events disable Aprobar, Rechazar, Agregar entradas, Editar, Eliminar with a "Finalizado" badge / "Evento finalizado — solo lectura" tooltip. Do NOT gray out the whole row. Keep "Compras" working.
- **OrganizerDashboard.jsx:** past events read-only — disable Editar/Eliminar; keep Metricas.
- **Read-only consultation view** for admin/organizer (new; EventDetail is public-only today).
- Backend negative tests (past-event mutation rejected with 409); existing green tests preserved.

### Out of Scope

- See Non-Goals above.

## Capabilities

> Contract for sdd-spec. Researched `openspec/specs/` for existing capability names.

### New Capabilities

- `past-event-mutation-guard`: Cross-cutting backend immutability rule. A past event (`Date < server UTC now`) is immutable. Shared guard helper evaluating `eventEntity.IsExpired(clock)` on the materialized entity, throwing 409 Conflict (`ProblemDetails` type `"event-finalized"`, title "Event has already finished") before any save/audit/notification. Applied to event content mutation endpoints not otherwise spec'd: `PUT /events/{id}`, `DELETE /events/{id}`, `POST /events/{id}/image`. Flag-independent (not gated by `HideExpiredEvents`). Carve-outs: consultation GET stays working; payments-domain operations untouched.
- `past-event-consultation`: Read-only "Ver" consultation view for admin and organizer (none today; EventDetail is public-only). Surfaces event detail + purchases without mutation affordances; reuses management fetch (`GET /events/{id}/manage`, `includeExpired`).

### Modified Capabilities

- `role-access` (EHE-006): "Organizers SHALL see and edit their past events exactly as before" → organizers AND admins may CONSULT but NOT mutate past events. Mutation authority for past events is revoked; consultation preserved; dashboard listing of past events unchanged.
- `event-approval` (EA-003, EA-004): approve/reject MUST reject past events with 409 before any status/audit mutation.
- `admin-ticket-stock` (ATS-002, ATS-004): AddTicketStock/AddTicketType MUST reject past events with 409 before any capacity mutation.

## Approach

Single shared guard, evaluated on the materialized entity (the pure `Event.IsExpired(DateTime)` method, EHE-001, is unit-tested and EF-safe outside queries). Each mutating service method loads the event, then calls the guard before any write. `AdminService` gets `TimeProvider` injected via DI, mirroring `EventService`'s clock pattern (EventService.cs:22/40-48; singleton `TimeProvider.System` at Program.cs:73-75); tests use `FakeTimeProvider`. Backend returns 409 Conflict with `ProblemDetails` consistent with the `purchase-guards` 409 shape (EHE-004/005). UI computes `isPast = new Date(event.date) < new Date()` per row to disable actions and show the "Finalizado" badge; backend remains authoritative. New consultation view renders management-event data read-only.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Services/EventService.cs` | Modified | Guard in `UpdateEventAsync`(:458), `DeleteEventAsync`(:585), `ReplaceEventImageAsync`(:727), `AddTicketStockAsync`(:290), `AddTicketTypeAsync`(:368) |
| `backend/Services/AdminService.cs`, `IAdminService.cs` | Modified | Inject `TimeProvider`; guard in `ApproveEventAsync`(:104), `RejectEventAsync`(:121) |
| `backend/Program.cs` | Modified | `AdminService` registration resolves `TimeProvider` |
| `frontend/src/pages/AdminPanel.jsx` | Modified | "Ver" action; disable past-event actions; "Finalizado" badge/tooltip; keep Compras (:383-445) |
| `frontend/src/pages/OrganizerDashboard.jsx` | Modified | Disable Editar/Eliminar for past; keep Metricas (:228-259) |
| `frontend/src/pages/...` (consultation view) | New | Read-only consultation view for admin/organizer |
| `backend/Tests/*` | New | Negative tests: each mutation rejects past event with 409; existing green tests preserved |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Timezone boundary: client `isPast` vs server UTC | Med | Backend guard authoritative; UI disable is cosmetic defense-in-depth; compare in UTC (mind `DateTime.Kind` mixing) |
| `AddTicketType` not frozen (only stock frozen) | Med | Freeze BOTH — both modify capacity |
| Guard runs after save/notification (date-change buyer emails, EventService.cs:509-575) | High | Guard MUST throw BEFORE any save/side-effect; load → guard → mutate order |
| `AdminService` has no clock today | Med | Inject `TimeProvider` mirroring EventService; tests use `FakeTimeProvider` |
| GET on past events breaks (consultation carve-out) | Low | Guard applies to MUTATION only; green tests `GetEventById_ManagementIncludeExpired_200`/`Organizer_ManagementEvent_Expired_200` must stay green |
| Calling `IsExpired` inside `IQueryable` (EF can't translate) | Low | Evaluate on materialized entity only, never in query predicate |
| `HideExpiredEvents` flag coupling | Low | Immutability is HARD, independent of flag (flag scopes to filters + purchase guards, EHE-009) |
| PUT blocked → EventForm still POSTs image | Med | Read-only path must not reach image call; UI disables both |
| Reviewer load (guard touches 7 endpoints + 2 surfaces + new view) | Med | Forecast in sdd-tasks; consider chained PRs (backend guard slice, then UI slice) |

## Rollback Plan

Purely additive behavior guard — no schema migration, no seeded data, no enum changes. `git revert <sha>` removes the guard calls, the `TimeProvider` injection on `AdminService`, the consultation view, and the UI disable/badge; existing mutation/consultation flows return to today's behavior. Because the rule is HARD and independent of `HideExpiredEvents`, there is no flag to toggle — rollback is code revert only. If a softer rollback is ever desired before full revert, an interim feature gate (`ImmutabilityGuard.Enabled`, default `true`) could be introduced as a *later refinement*, but is explicitly NOT part of this change (hard rule decided).

## Dependencies

- None external. Reuses existing `Event.IsExpired(DateTime)` (Models/Event.cs:28), injected `TimeProvider` (Program.cs:73-75), and the 409 `ProblemDetails` shape from `purchase-guards` (EHE-004/005).

## Success Criteria (Acceptance)

- [ ] Every listed mutation endpoint rejects a past event with 409 Conflict (`type: "event-finalized"`) before any save/audit/notification.
- [ ] GET on past events (management detail, purchases) stays working; `GetEventById_ManagementIncludeExpired_200` and `Organizer_ManagementEvent_Expired_200` stay green.
- [ ] Payments carve-out intact: `AdminPurchaseService` refund/purchase paths untouched and green.
- [ ] Admin "Ver" consultation view renders event detail + purchases read-only for past events.
- [ ] AdminPanel past-event rows disable Aprobar/Rechazar/Agregar entradas/Editar/Eliminar with "Finalizado" badge/tooltip; "Compras" works; row not grayed out.
- [ ] OrganizerDashboard past events disable Editar/Eliminar; "Metricas" works.
- [ ] `dotnet test` green; existing tests unaffected; new negative tests pass.
