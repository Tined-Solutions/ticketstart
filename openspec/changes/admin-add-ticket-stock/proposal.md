# Proposal: Admin Add Ticket Stock

## Intent

Event organizers (salon/theater owners) sometimes need to add capacity to an existing event — a few extra chairs for the same zone, or a whole new zone with a different price. Today there is NO supported path: `EventService.UpdateEventAsync` only touches scalar event fields, and `EventForm` edit mode silently ignores ticket-type edits (a pre-existing trap). This change gives Admins a supported, concurrency-safe way to add stock at any point in the event lifecycle.

## Scope

### In Scope
- Admin endpoint to **increment** `Quantity` of an existing `TicketType`.
- Admin endpoint to **create a NEW `TicketType`** on an existing event (different zone/price — user-confirmed business case).
- `SELECT ... FOR UPDATE` row lock mirroring `ReservationService.CreateReservationTransactionalAsync`.
- Admin-only auth (`RequireAdminRole`); audit logging with new `AuditActionType` members.
- Per-operation high anti-error cap (≈1000) — reasonable validation, not a restrictive limit.
- Allowed at ANY lifecycle stage, even after sales started (availability recalculates automatically).
- AdminPanel modal UI + TanStack invalidations.
- Backend tests (strict TDD); frontend Vitest where feasible.

### Out of Scope
- Seat entity / seat maps / assigned seating / per-seat QR.
- Buyer "new seats" notification (quantity-based model, no seat identity).
- Decreasing `Quantity` (removing stock).
- Editing existing `TicketType` price/name.
- Organizer-triggered stock changes (admin-only per business requirement).

## Capabilities

### New Capabilities
- `admin-ticket-stock`: Admin operations to increment an existing `TicketType`'s `Quantity` and to add a new `TicketType` to an existing event, with concurrency-safe row locking, audit logging, and validation.

### Modified Capabilities
- None — no existing `openspec/specs/` capability receives a spec-level change (admin/event behavior was not previously spec-tracked).

## Approach

Two endpoints on `AdminController` (class-level `RequireAdminRole` already covers auth; follow `CreateUser` pattern: `TryGetUserId`, try/catch, `TryLogAuditAsync`):

- `POST /api/admin/events/{eventId}/ticket-types/{ticketTypeId}/stock` — body `{ additionalQuantity }` (>0 int, ≤ cap). `CreateExecutionStrategy` → `BeginTransaction` → `SELECT ... FOR UPDATE` (`FromSqlInterpolated`; SQLite no-op-UPDATE fallback; InMemory plain-query fallback) → validate event/TT match → `Quantity += N` → save → commit. Serializes against concurrent reservations on the same row lock.
- `POST /api/admin/events/{eventId}/ticket-types` — body `{ name, price, quantity }` (insert new row; event-existence validation; transaction-only, no shared row lock needed).

Audit with new `AuditActionType.AddTicketStock` + `AddTicketType` (string column → NO migration). Frontend: AdminPanel per-event "Agregar entradas" → modal fetching ticket types/availability via existing `useEvent(id)` (EventSummary has no ticket types). On success `queryClient.invalidateQueries(['event', id])` + `['events']` so buyer catalog/EventDetail pick up new availability. EventForm trap mitigation decided in **design** (disable/hide the silent ticket-type quantity fields in edit mode, or explicit warning).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Controllers/AdminController.cs` | Modified | 2 new endpoints |
| `backend/Services/IEventService.cs`, `EventService.cs` | Modified | `AddTicketStockAsync`, `AddTicketTypeAsync` |
| `backend/Models/AuditLog.cs` | Modified | New `AuditActionType` members (no migration) |
| `frontend/src/pages/AdminPanel.jsx` | Modified | "Agregar entradas" modal, `useQueryClient` |
| `frontend/src/components/EventForm.jsx` | Modified | Disable silent ticket-type edit fields (design-decided) |
| `backend/Tests/*`, `frontend/src/**/*.test.jsx` | New | Controller/service/UI tests |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Increment lost vs concurrent reservation | Med | `FOR UPDATE` row lock identical to `ReservationService` pattern |
| EventForm edit trap (silent no-op stock edits) | High | Disable/hide ticket-type quantity fields in edit mode; new modal is the only stock path |
| AdminPanel not TanStack-driven → stale buyer catalog | Med | Explicit `invalidateQueries` on `['event',id]` + `['events']` |
| `FOR UPDATE` untestable on InMemory/SQLite | Med | Provider-branching fallback; rely on concurrency-style coverage where feasible |
| `AuditLog.Details` column overflow | Low | Truncate details to <1000 chars (column max) |

## Rollback Plan

Purely additive — no schema migration, no seeded data. `git revert <sha>` removes the two endpoints, service methods, enum members, modal, and EventForm change; existing reservation/event flows untouched. No DB cleanup needed (no `Quantity` deltas to undo beyond what already sold).

## Dependencies

- None (no migration, no new external deps).

## Success Criteria

- [ ] Admin can increment an existing `TicketType`'s `Quantity`; buyer `EventDetail` shows updated `X disponibles de Y`.
- [ ] Admin can add a new `TicketType` to an existing event; it appears in the buyer catalog.
- [ ] Non-admin/organizer receives 403 on both endpoints.
- [ ] Concurrent increment + reservation serializes (no lost update, no oversell).
- [ ] Both operations audited with new `AuditActionType` members.
- [ ] `dotnet test` green; existing tests unaffected.

## Product Scope (pre-confirmed)

Pre-confirmed in exploration session (engram `sdd/admin-add-ticket-stock/scope`): two-operations scope (increment + new type, not increment-only), any-lifecycle timing, high per-op cap, admin-only — no re-opened product questions.