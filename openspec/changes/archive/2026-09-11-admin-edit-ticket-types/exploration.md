## Exploration: Admin full edit of ticket availability for pre-approval events

**Change**: `admin-edit-ticket-types`
**Artifact store**: hybrid (OpenSpec + Engram) — topic key `sdd/admin-edit-ticket-types/explore`
**Session preflight**: mode `auto`, store `both`, delivery strategy `single-pr`, review budget `2000` lines
**Status**: explored (read-only; no code changed)
**Canonical intent**: Engram observation #705 "PLAN APROBADO: Edición de disponibilidad de entradas pre-aprobación (admin)".

### Current State

**Approval lifecycle (EA-001/EA-005).** `EventStatus` is `Pending | Approved | Rejected` (`backend/Models/EventStatus.cs`). New events always start `Pending` (`EventService.CreateEventAsync` L96-98). `AdminService.ApproveEventAsync` / `RejectEventAsync` (L127 / L148) flip status with **no state machine**: any→any is allowed. Critically, `event-approval/spec.md:121-125` explicitly requires **reject-after-approve**, so a `Rejected` event can be a formerly-`Approved` event that already sold tickets.

**Public visibility (EA-002, catalog-filtering).** `GetAllPublishedEventsAsync` filters `Status == Approved` (`EventService.cs` L187). The public detail endpoint `GET /events/{id}` returns **404 for any non-Approved event** (`EventController.cs` L46-49). The management variant `GET /events/{id}/manage` (`EventController.cs` L71-83) is `EventOwnership`-gated, passes `includeExpired: true`, and returns the event regardless of status — this is the only endpoint an Admin can use to read ticket types for a Pending/Rejected event.

**Current admin ticket mutations (add-only).**
- `EventService.AddTicketStockAsync` (L293-371): `SELECT ... FOR UPDATE` (Postgres) / no-op-UPDATE (SQLite) / plain read (InMemory), inside an execution-strategy + transaction with rollback; validates `additionalQuantity` `(0, 1000]`; throws `KeyNotFoundException` on missing event/type; runs `EventFinalizedGuard.EnsureMutable` **inside** the lock; `Quantity += additionalQuantity`.
- `EventService.AddTicketTypeAsync` (L378-444): transaction-only (no row lock); validates name non-empty and ≤100 chars, `price >= 0`, `quantity > 0` and `<= 1000`; runs `EventFinalizedGuard.EnsureMutable` inside the transaction.
- Neither method checks event `Status` today — so both currently accept `Pending`, `Rejected`, and `Approved` (and reject only past/finalized events via PEM-001).
- Caps are two distinct consts: `MaxAdditionalStock = 1000` (an *increment*) and `MaxTicketQuantityPerOperation = 1000` (a *total* for a new type) (`EventService.cs` L36-37).

**Controller & audit.** `AdminController` is `[Authorize(Policy = "RequireAdminRole")]` at class level (L13-16). `AddTicketStock` (L187) and `AddTicketType` (L220) map `KeyNotFoundException`→404, `ArgumentException`→400, `EventFinalizedException`→409 RFC 7807 (`type: "event-finalized"`), else 500; each calls `TryLogAuditAsync` (private, L525) *after* service success with `AuditActionType.AddTicketStock` / `AddTicketType`. `AuditActionType` is enum `backend/Models/AuditLog.cs` L73-89, stored as varchar → adding a member is migration-free. Audit `Details` is truncated to 1000 via the local `Truncate` helper (L542).

**Frontend.** `AdminPanel.jsx` builds one `rowItems` array per event reused by the desktop inline buttons and the mobile kebab (L558-615); "Agregar entradas" (L584-590) opens `AddTicketsModal` via `addTicketsTarget` state (L102, mount L1011-1024). `AddTicketsModal.jsx` has `increase` / `newType` modes, validations mirroring `EventForm` create mode, `useDialog` a11y (role=dialog/aria-modal/labelledby, focus first error, `role="alert"` form errors), and on success invalidates `['event', id]` + `['events']` (ATS-007) then `onSuccess()` re-runs the panel's manual `loadData`. `EventForm` edit mode **omits ticket types entirely** (ATS-008 mitigation, `EventForm.jsx` L71-73), and `EventReadOnlyView` renders `EventForm mode="edit" readOnly`.

### Affected Areas

- `backend/Services/EventService.cs` — add `ReplaceTicketTypesAsync`; optionally harden `AddTicketStockAsync` (L293) / `AddTicketTypeAsync` (L378) with a status guard. Reuse the transaction/execution-strategy/rollback and `EventFinalizedGuard` patterns.
- `backend/Services/IEventService.cs` — new interface method + request/response DTOs (L97/L109 are the add-only signatures; DTOs at L112-120).
- `backend/Controllers/AdminController.cs` — new `PUT events/{id}/ticket-types` action; mirror the existing 404/400/409/500 mapping + `TryLogAuditAsync`; consider adding a 409 for the "has sales" case.
- `backend/Models/AuditLog.cs` — add `EditTicketAvailability` to `AuditActionType` (or reuse a name; decide).
- `backend/Data/ApplicationDbContext.cs` — no schema change, but FK behavior is load-bearing: `Ticket.TicketTypeId` and `Reservation.TicketTypeId` are both `OnDelete(DeleteBehavior.Restrict)` (L135-138, L110-113). Deleting a referenced `TicketType` will throw a DB FK error.
- `frontend/src/pages/AdminPanel.jsx` — branch the row action/icon/label on `event.status`.
- `frontend/src/components/EditTicketsModal.jsx` — new component (greenfield; no existing `AddTicketsModal` test to copy from).
- `frontend/src/lib/queryKeys.js` — reuse `managementEvent(id)` for invalidation (new usage).
- Spec deltas (OpenSpec): `admin-ticket-stock` (extend ATS), `past-event-mutation-guard` (PEM-002 "six endpoints" list must grow), possibly `catalog-filtering`/`event-approval` for the Rejected-with-sales carve-out.

### Approaches

**1. Single atomic `PUT /api/admin/events/{id}/ticket-types` replacing the whole list** (approved plan).
- Pros: one round-trip, atomic (all-or-nothing via transaction + rollback), matches the approved invariant, reuses the existing `CreateEventAsync` validation set, no migration.
- Cons: delete-of-referenced-type needs an explicit policy (see Risks), and cap semantics differ from add-stock.
- Effort: Medium.

**2. Reuse `AddTicketStockAsync` + `AddTicketTypeAsync` and add a new delete endpoint** (incremental CRUD).
- Pros: smaller new surface.
- Cons: NOT atomic — a partial failure leaves a half-edited list; contradicts the approved "one atomic replacement" decision; more endpoints to guard/audit.
- Effort: Medium-High.

**3. Reject pre-approval full edit; keep add-only everywhere.**
- Pros: no new risk.
- Cons: directly contradicts the approved business decision (#705); rejected.
- Effort: Low.

### Recommendation

Proceed with **Approach 1** (already approved in #705), but resolve the Rejected-with-sales edge case explicitly in spec/design (do NOT silently ship full replacement as-is). Concretely:
- Replace list in one `CreateExecutionStrategy` + `BeginTransactionAsync` block with rollback, mirroring `AddTicketStockAsync`.
- Guards in this order, each before any mutation: (1) load event tracked; (2) `Status ∈ {Pending, Rejected}` else 409; (3) `EventFinalizedGuard.EnsureMutable`; (4) payload validation (`>=1` type, name non-empty/trim ≤100, `price >= 0`, integer `quantity > 0` and ≤ cap); (5) reference/ownership validation of supplied ids.
- Audit once after commit via `TryLogAuditAsync` with the admin id and a summary.
- Frontend: use `useManagementEvent` (NOT `useEvent`) in `EditTicketsModal`; branch AdminPanel label to "Editar entradas" for Pending/Rejected and keep "Agregar entradas" for Approved.

### Drift vs the Approved Plan (#705)

1. **Line refs are accurate.** `AddTicketStockAsync` L293, `AddTicketTypeAsync` L378, `AdminController` stock L187, approve L336, reject L381 — all verified against current HEAD `91ef552`. No code moved since the plan.
2. **The plan's "no oversell because Rejected has no sales" rationale is incomplete.** `Rejected` is reachable via reject-after-approve (EA-005, spec L121-125), so a Rejected event CAN already own `Ticket`/`Reservation` rows. Full replacement that deletes such a `TicketType` hits `OnDelete(Restrict)` → `DbUpdateException` → 500 today. **Blocker-level decision for spec/design.**
3. **"Cap 1000/type" is ambiguous.** `MaxAdditionalStock=1000` caps an *increment*; applying "quantity ≤ 1000" to full replacement reinterprets `Quantity` as a *total*. Add-stock can already accumulate a total >1000, so a full edit that re-submits the existing total would be rejected. Decide: cap total at 1000 (and accept the trap), or cap only *new* types / validate `quantity >= sold+reserved`.
4. **Price rule mismatch.** Backend `CreateEventAsync`/`AddTicketTypeAsync` allow `price >= 0`; `EventForm`/`AddTicketsModal` require `price > 0`. "Validates like EventForm" therefore means `>0`, not `>=0`. Pick one canonical rule and state it.
5. **`AddTicketsModal` cannot load types for Pending/Rejected.** It uses `useEvent` → `GET /events/{id}` → **404 for non-Approved**. So `increase` mode is effectively broken for exactly the events this change targets. `EditTicketsModal` MUST use `useManagementEvent`. (Latent bug in the existing modal; out of scope to fix but worth noting.)
6. **Invalidation drift.** Plan says invalidate `['event', id]`; but the management read path uses `queryKeys.managementEvent(id)`. `EditTicketsModal` should invalidate `['management-event', id]` (and `['event', id]`/`['events']` for public cache) so `EventReadOnlyView` refreshes.
7. **Spec bookkeeping.** PEM-002 (`past-event-mutation-guard/spec.md:33-35`) enumerates "all six mutation endpoints" by name. The new endpoint becomes a seventh and MUST be added to that requirement, not only to the `admin-ticket-stock` delta.
8. **Label collision.** `AdminPanel` already has an "Editar" action (navigate to organizer event edit, L599-605). Adding "Editar entradas" to the same row/kebab risks user confusion; the icon (`TicketPlus`) should likely change too (e.g. `Pencil`/`Ticket`).

### Risks

- **HIGH — referenced TicketType deletion (Rejected with sales).** `Ticket.TicketTypeId` / `Reservation.TicketTypeId` are `Restrict`. Need a specified outcome: e.g. return 409 when the event has any ticket/reservation, block deletion of referenced types only, or restrict full edit to `Pending` (drop `Rejected` from the invariant). The current plan does not decide this.
- **MED — atomicity test surface.** SQLite in-memory (`EventServiceTicketStockTests`) is the established service-test harness; a failing row mid-replace must roll back the whole list. Ensure the test proves no partial writes.
- **MED — cap semantics regression** (see Drift #3): re-submitting an existing >1000 total fails.
- **LOW — audit naming.** `EditTicketAvailability` must be added; enum is varchar-backed so no migration, but confirm no consumer switches exhaustively on `AuditActionType`.
- **LOW — `EditTicketsModal` has no prior modal test** for `AddTicketsModal`; mirror `RoleEditModal`/`ResetPasswordModal` test + a11y patterns instead.
- **LOW — response shape undefined** by the plan. Return the recomputed `TicketTypeWithAvailability[]` (reuse `MapTicketTypeWithAvailabilityAsync`) for consistency with the add endpoints.

### Open Questions for Spec / Design

1. **Rejected-with-sales**: forbid full edit, or allow edit but forbid deleting referenced types, or return a clear 409? (Blocker.)
2. Does full edit allow reducing `Quantity` below `sold + reserved` (which yields clamped availability 0 but kept sales)? For Pending this is moot; for the Rejected-with-sales path it is not.
3. Canonical price rule: `>= 0` (backend today) or `> 0` (frontend today)?
4. Canonical quantity cap: per-operation (current add-stock semantics) vs total-per-type (current new-type semantics) for a replacement?
5. Confirm `AddTicketStockAsync`/`AddTicketTypeAsync` hardening: should they now reject `Pending`/`Rejected` (forcing the new PUT), or remain status-agnostic? Hardening changes existing behavior and existing tests.
6. Response body contract: list of ticket types with availability, or the full event?
7. Does "delete absent type" apply when the type has zero sales but also when it has refunded/used tickets? (Related to Q1.)

### Ready for Proposal

**Yes**, with one caveat: the proposal MUST state a decision (or an explicit spec-level open item) for the **Rejected-with-sales** case, because the approved plan's safety argument ("Rejected events have no sales") does not hold. Everything else is a clean, migration-free extension of existing patterns.
