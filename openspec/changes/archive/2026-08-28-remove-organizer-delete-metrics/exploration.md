# Exploration: remove-organizer-delete-metrics

> Read-only investigation of the Ticketstart worktree on branch `feat/frontend-brand-polish`
> (uncommitted Panel redesign IS the current state; explored as-is, not against HEAD).

## Current State

### Organizer delete surface (frontend)

`frontend/src/pages/OrganizerDashboard.jsx`:

- The **Eliminar** action is a `variant: 'danger'` item inside each row's `DropdownMenu`
  ("Acciones" kebab), lines 270–277. It is `disabled: isPast` (PEM-002 cosmetic guard).
- Flow: `handleDeleteClick(event)` → sets `deleteTarget` → renders
  `DeleteConfirmationDialog` → `handleDeleteConfirm` → `apiClient.delete('/events/${deleteTarget.eventId}')`
  (line 85) → optimistic row removal + success feedback.
- Related state: `deleteTarget`, `deleting`, `feedback` (feedback is also used by load/retry errors,
  so only the delete-driven usage is removable, not the state itself).

`frontend/src/components/DeleteConfirmationDialog.jsx` (new, uncommitted) is a **shared**
glass dialog used by BOTH `AdminPanel` and `OrganizerDashboard` (only variable is the event name;
focus trap via `useDialog`). It must survive the change — Admin keeps using it.

### Admin delete surface (frontend) — must remain unaffected

`frontend/src/pages/AdminPanel.jsx`:

- Identical pattern: kebab "Eliminar" (lines 491–497, `disabled: isPast`) →
  `DeleteConfirmationDialog` → `apiClient.delete('/events/${deleteTarget.id}')` (line 205).
- No surface differences to worry about: both call the SAME endpoint `DELETE /api/events/{id}`;
  role distinction happens entirely server-side today.

### Backend delete path

`backend/Controllers/EventController.cs` → `DeleteEvent` (lines 176–219):

- `[HttpDelete("{id:guid}")]` + `[Authorize(Policy = "EventOwnership")]`.
- Calls `_eventService.DeleteEventAsync(id, userId, userRole)`; writes an audit log
  (`AuditActionType.DeleteEvent`) only when `userRole == UserRole.Admin`.
- Exception mapping: `KeyNotFoundException` → 404, `UnauthorizedAccessException` → 403,
  `EventFinalizedException` → 409 RFC 7807 (`event-finalized`).

`backend/Authorization/EventOwnershipHandler.cs`:

- Admin role ⇒ succeed immediately; otherwise succeeds only if `OrganizerId == userId`.
- So today BOTH an owner-organizer and any Admin pass the policy gate.

`backend/Services/EventService.cs` → `DeleteEventAsync` (lines 602–644):

- Second ownership check: `OrganizerId != userId && userRole != UserRole.Admin` →
  `UnauthorizedAccessException` (403).
- `EventFinalizedGuard.EnsureMutable` (PEM-001): past events (`Date < now`) are immutable for
  EVERYONE — including Admin — → 409. Then `Remove` + `SaveChanges` + R2 image cleanup.
- Therefore "regardless of status" for the organizer requires removing organizer delete at the
  gate/service level, not just for a specific status. Note: Admin deletion of past events is
  currently blocked by the 409 guard — that existing behavior is out of scope and unchanged.

### Metrics surface — redundancy verified field-by-field

- The kebab "Metricas" item (OrganizerDashboard lines 252–256) navigates to
  `/organizer/events/:id/metrics` (route in `App.jsx` line 75) →
  `frontend/src/pages/OrganizerEventMetrics.jsx` → `GET /api/metrics/events/{id}`
  (`MetricsController.GetEventMetrics`, `[Authorize(Policy = "EventOwnership")]`).
- The dashboard list itself is fed by `GET /api/metrics/organizer`
  (`MetricsController.GetOrganizerMetrics`, `[Authorize(Policy = "RequireOrganizadorRole")]`).
- Both endpoints return the SAME `EventMetrics` DTO
  (`backend/Services/IMetricsService.cs`): `Id, EventId, EventName, EventDate, TicketsSold,
  TotalRevenue, RemainingInventory, TicketsScanned, Status`. Computation is identical
  (APR-005 refund exclusion; `RemainingInventory = inventory − sold − activeReservations`).

Field comparison — dashboard row vs OrganizerEventMetrics page:

| Field                        | Dashboard row (below list) | Metrics page |
|------------------------------|:--------------------------:|:------------:|
| Event name                   | ✅ (h3 heading)            | ✅ (page h1) |
| Date (`eventDate`)           | ✅ (formatEventDate)       | ✅ (formatDate) |
| Tickets sold                 | ✅                         | ✅           |
| Total revenue (currency)     | ✅ (formatCurrency)        | ✅ (formatCurrency) |
| Remaining inventory          | ✅                         | ✅           |
| Tickets scanned              | ✅                         | ✅           |
| Status badge (Aprobado/…)    | ✅                         | ❌           |
| "Finalizado" past badge      | ✅                         | ❌           |
| Location                     | ✅                         | ❌           |
| Row actions (Ver/Acciones)   | ✅                         | ❌           |

**Verdict: the redundancy claim is CONFIRMED.** The metrics page renders zero unique data —
it is a large-card re-render of the four mini-stats plus date already present in every row,
and it even drops status/location. The dashboard row is a strict superset.

## Affected Areas

- `frontend/src/pages/OrganizerDashboard.jsx` — remove "Eliminar" menu item + delete-flow state/handlers + `DeleteConfirmationDialog` usage; remove "Metricas" item if the metrics page is retired.
- `frontend/src/components/DeleteConfirmationDialog.jsx` — SHARED; keep (Admin still uses it). Do not delete.
- `frontend/src/pages/OrganizerEventMetrics.jsx` (+ `.test.jsx`) — candidate for removal (or keep the route; product decision).
- `frontend/src/App.jsx` — route `/organizer/events/:id/metrics` if the page is retired.
- `frontend/src/pages/AdminPanel.jsx` — NO functional change expected; only touched if shared dialog usage is refactored.
- `backend/Services/EventService.cs` → `DeleteEventAsync` — replace owner-or-admin check with Admin-only (and update `IEventService` doc comment).
- `backend/Controllers/EventController.cs` — endpoint policy decision (see Approaches); audit-log branch already Admin-only.
- Tests listed below.

## Approaches

### Change 1 — block organizer deletion (admin keeps it)

1. **Service-level role check** — in `EventService.DeleteEventAsync`, require
   `userRole == UserRole.Admin` (throw `UnauthorizedAccessException` otherwise); keep the
   endpoint's `EventOwnership` policy.
   - Pros: smallest blast radius; `EventOwnership` policy stays untouched for the 5 other
     endpoints that use it (`GET/PUT manage`, image upload, metrics per-event); the
     controller's Admin-only audit branch and 409 mapping remain coherent; organizer-owner
     gets a clean 403 from the service.
   - Cons: policy name no longer fully describes the delete semantics (doc comment mitigates).
   - Effort: Low.

2. **Endpoint policy swap** — change the `[HttpDelete]` attribute to
   `[Authorize(Policy = "RequireAdminRole")]`.
   - Pros: authorization intent explicit at the gate; no service change.
   - Cons: changes the endpoint's whole auth identity; if anyone later wants owner-scoped
     delete again the service check must be rebuilt; slightly larger conceptual change to an
     endpoint shared by both panels.
   - Effort: Low.

**Recommendation: Approach 1** (service-level Admin-only check). It matches Ticketera's
convention (service owns authorization validation, e.g. `UpdateEventAsync`) and keeps the
shared `EventOwnership` policy semantics intact for every other endpoint.

### Change 2 — remove the redundant metrics button

1. **Remove UI entry + page** — delete the "Metricas" kebab item, the
   `/organizer/events/:id/metrics` route, `OrganizerEventMetrics.jsx` + its test; optionally
   retire `GET /metrics/events/{id}`, `GetEventMetricsAsync`, `CalculateMetricsAsync`.
   - Pros: no dead UI; the organizer list already shows 100% of the data.
   - Cons: per-event endpoint removal is a public-ish API decision (Admin never used it, but
     it is an authorized surface).
   - Effort: Low–Medium (Medium only if the backend endpoint is also retired).

2. **Remove UI entry only** — keep the backend endpoint.
   - Pros: reversible, no API surface change.
   - Cons: dead endpoint + dead page code; `GetEventMetricsAsync` has no controller test
     coverage today (silent rot).
   - Effort: Low.

**Recommendation: raise at propose phase** — UI entry + page removal is safe; whether to
retire the per-event backend endpoint should be an explicit spec decision (dead code with
zero test coverage vs. keeping an authorized API surface).

## Tests Requiring Updates

Frontend (Vitest + Testing Library):

- `frontend/src/pages/OrganizerDashboard.test.jsx` (currently modified, uncommitted — edit as-is):
  - Delete flow block (lines ~308–414): 4 tests (dialog opens / cancel / confirm DELETE / error feedback) → replace with "no Eliminar item for organizers, for any status" assertions.
  - `"hides Editar for organizers"` (lines ~259–275): expects "Metricas + Eliminar remain for organizers" → update.
  - Past-events read-only test (lines ~418–461): asserts Eliminar disabled with readonly title → must become "Eliminar absent".
  - `"Metricas" menu item navigates…` (lines ~245–257): delete if metrics entry is removed.
- `frontend/src/pages/OrganizerEventMetrics.test.jsx` — delete if the page goes.
- `frontend/src/pages/AdminPanel.test.jsx` — delete tests (lines ~237–330, ~955–958) MUST KEEP PASSING unchanged (regression guard for Change 1).
- `DeleteConfirmationDialog` has no dedicated test file; covered via AdminPanel integration — unchanged.

Backend (xUnit):

- `backend/Tests/EventServiceTests.cs` — `DeleteEventAsync` region (lines ~648–845):
  `ByOwner_DeletesEvent` (organizer) must invert to expect `UnauthorizedAccessException`;
  `ByAdmin_DeletesEvent` stays; `WithImageUrl_*` organizer-owner cases switch to Admin.
- `backend/Tests/EventServiceImmutabilityTests.cs` (~176–188): `DeleteEventAsync_PastEvent_…`
  uses an organizer → becomes Admin-only scenario (organizer can no longer reach the guard).
- `backend/Tests/ImageStoragePropertyTests.cs` — 5 organizer-role delete calls (lines 454, 535, 599, 650, 706) → switch to Admin.
- `backend/Tests/EventControllerTests.cs` — `DeleteEvent_AdminRole_LogsDeleteEventAudit` (163),
  `DeleteEvent_AdminRole_AuditLogFails…` (185) stay; `DeleteEvent_PastEvent_409_EventFinalized`
  (515) seeds with an organizer → 409 is now reachable only via Admin.
- `backend/Tests/EventManagementPropertyTests.cs` — non-owner-throws (802) still valid; admin delete (1026) stays.
- `backend/Tests/MetricsPropertyTests.cs` / `MetricsConsolidationTests.cs` — only if the per-event metrics endpoint/service methods are retired.

## Risks

- **Uncommitted overlap**: this change touches the SAME files as the uncommitted
  `feat/frontend-brand-polish` work (OrganizerDashboard, AdminPanel, its test, the new
  `DeleteConfirmationDialog.jsx`, pnpm-lock). Branch/commit strategy must be decided before
  `sdd-apply` (apply on top of the worktree as-is, or land brand-polish first).
- **Shared component**: `DeleteConfirmationDialog` must survive; only the organizer usage is removed. Accidentally deleting the component breaks AdminPanel.
- **Backend policy decision**: Approach 1 vs 2 above — service-level check recommended, but it is a spec-level choice.
- **Past-event asymmetry**: after the change, organizer has no delete at all (any status); Admin retains delete but past events still 409 via `EventFinalizedGuard` (existing behavior, untouched). Spec should state this explicitly to avoid "regardless of status" being misread as applying to Admin.
- **Dead code**: if UI is removed but the per-event metrics endpoint stays, it becomes an untested authorized surface (`GetEventMetricsAsync` has ⚠️ no covering controller tests today).
- **Feedback state coupling**: OrganizerDashboard's `feedback` state is shared by load errors and delete feedback; removing delete must not break the load/retry feedback path.

## Ready for Proposal

Yes — evidence is complete: exact UI flows, backend authorization chain, field-level metrics
overlap (redundancy confirmed), and the full test impact list. Next step: `sdd-propose`,
deciding (a) service-level vs policy-swap for delete, and (b) whether the per-event metrics
endpoint is retired together with the UI.
