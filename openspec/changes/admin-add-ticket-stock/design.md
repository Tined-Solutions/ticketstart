# Design: Admin Add Ticket Stock

## Overview & Goals

Give Admins a supported, concurrency-safe way to add ticket capacity to an existing event at
any lifecycle stage, via two AdminController endpoints: increment an existing `TicketType.Quantity`
(ATS-002) and create a new `TicketType` (ATS-004). Both reuse the `ReservationService` row-lock
pattern (ATS-003), are audited (ATS-005), leave the data model untouched (ATS-006 is automatic
because availability is purely mathematical), expose an AdminPanel modal (ATS-007), and remove
the `EventForm` edit-mode silent no-op trap (ATS-008). Coverage is backend-stRICT TDD (ATS-009).

## Technical Approach

Two AdminController actions delegate to two new `IEventService` methods. The increment path
mirrors `ReservationService.CreateReservationTransactionalAsync` (ReservationService.cs:83-179)
exactly: `CreateExecutionStrategy` → `BeginTransactionAsync` → provider-branched `FOR UPDATE` →
validate event/TT match → `Quantity += N` → `SaveChanges` → `Commit` (catch → `Rollback`).
Availability is never stored, so the response recomputes `available` via the existing
`ComputeAvailabilityAggregatesAsync` math (EventService.cs:170) on a single id. The new-type path
is a transaction-wrapped insert. AdminPanel gets a modal that reads ticket types through the
existing `useEvent(id)` hook and `invalidateQueries` on `['event', id]` + `['events']` (ATS-006/007).

## Architecture Decisions

| # | Decision | Choice | Alternatives | Rationale |
|---|----------|--------|--------------|----------|
| D-1 | Increment row-lock strategy | Byte-for-byte mirror of `CreateReservationTransactionalAsync`: `CreateExecutionStrategy.ExecuteAsync` wrapping `BeginTransactionAsync`; Npgsql → `FromSqlInterpolated($"SELECT * FROM \"TicketTypes\" WHERE \"Id\" = {ticketTypeId} AND \"EventId\" = {eventId} FOR UPDATE")`; SQLite → `FirstOrDefaultAsync(...)` then no-op `ExecuteSqlInterpolatedAsync($"UPDATE \"TicketTypes\" SET \"CreatedAt\" = \"CreatedAt\" WHERE \"Id\" = {ticketTypeId}")`; InMemory → plain `FirstOrDefaultAsync(...)`. | Optimistic concurrency token; no lock + last-write-wins. | Same lock is the serialization point `ReservationService` already uses on the row; the spec (ATS-003) mandates identical lock, guaranteeing no lost update vs concurrent reservation. SQLite no-op-UPDATE is already proven in tests. |
| D-2 | EventForm trap mitigation | In edit mode hide the **entire** `fieldset.ticket-types-section` and replace it with a static notice: ticket-type edits are not editable here — manage stock from the AdminPanel "Agregar entradas" action. Create mode keeps the editable fieldset untouched. | Disable each field; show an inline warning but keep fields visible. | Hiding removes the silent no-op entirely and points the admin at the supported path. Disabling still tempts edits that do nothing; a warning alone keeps the dead UI. ATS-008 forbids any silent no-op — hiding is the simplest compliant mitigation. |
| D-3 | AdminPanel TanStack integration | Minimal: keep manual list state; import `useQueryClient` and call `queryClient.invalidateQueries({ queryKey: queryKeys.event(id) })` and `{ queryKey: queryKeys.events }` after modal success. Do NOT migrate the events table to `useQuery`. | Migrate the table to `useQuery(['admin-events'])`. | Migrating the table is a larger refactor with its own stale-state risks; ATS-007 only requires invalidation of the buyer-facing keys. Minimal change, least blast radius. The parent `['event']` prefix clear is not used because we always know the affected id. |
| D-4 | Response shapes | Both endpoints return `TicketTypeWithAvailability` (IEventService.cs:127): `{ id, name, price, quantity, available }`. Increment → `200 Ok`; new type → `CreatedAtAction(...)`. `available` recomputed by a new `private async Task<TicketTypeWithAvailability> MapTicketTypeWithAvailabilityAsync(TicketType tt)` that calls `ComputeAvailabilityAggregatesAsync(new(){ tt.Id })` and the same `Math.Max(0, Quantity - sold - reserved)` clamping used in `MapToEventWithAvailabilityAsync` (EventService.cs:485-519). | Reuse `GetEventByIdAsync` and project one TT; expose a recompute helper. | Single helper reuses the canonical availability math (ATS-006) and keeps one mapping source. Avoids loading the whole event graph for one ticket type. |
| D-5 | Error mapping | Follow `EventController.UpdateEvent` (EventController.cs:105-122): `KeyNotFoundException` → `NotFound({error="Event not found"})`; `ArgumentException` → `BadRequest({error=ex.Message})`; `UnauthorizedAccessException` → `Forbid()`; catch-all → `500`. The class-level `RequireAdminRole` (AdminController.cs:14) covers 403 — no per-action auth attribute needed (ATS-001 `Non-admin → 403` satisfied by the policy). | Add `Authorize(Policy="RequireAdminRole")` per action. | Matches the existing controller pattern exactly; the policy already denies non-admins at the pipeline so they reach 403 before the action body. `KeyNotFoundException` from mismatched EventId on a real ticket type maps to 404 per ATS-002 scenario "Unknown event or mismatched ticket type". |
| D-6 | Audit details | Add `AuditActionType.AddTicketStock` and `AuditActionType.AddTicketType` to the enum (AuditLog.cs:73). Details strings: increment → `$"Admin added {n} tickets to ticket type {ticketType.Name} (event {eventId})"`; new type → `$"Admin created ticket type {name} (price {price}, quantity {quantity}) for event {eventId}"`. Both built into a local `Truncate(string, 1000)` helper mirroring the `AuditLog.Details` varchar(1000) cap (migration snapshot). ResourceId = event id (`AuditResourceType.Event`). | Log raw payloads; skip truncation. | ATS-005 requires the new members and ≤1000-char Details. Deterministic, human-readable strings keep audit readable and bounded; the Guid/block explicitly avoids logging buyer data. |
| D-7 | Validation constants | Add two `private const int` in `EventService`: `MaxAdditionalStock = 1000` (increment) and `MaxTicketQuantityPerOperation = 1000` (new type). Inline the validations in each method (mirror `CreateEventAsync` EventService.cs:62-72: name non-empty & ≤100 chars; price ≥ 0; quantity > 0) for increment: `additionalQuantity > 0 && ≤ MaxAdditionalStock`. | Extract a shared `IValidator<TicketType>` used by Create + new-type paths. | `CreateEventAsync` cannot share a parameter object cleanly because the increment input is just an int. Extraction would touch create-event (out of scope) and risk regression. Duplicating three trivial guards follows the existing inline-guard style. |
| D-8 | Frontend modal | New `AddTicketsModal` component (default export) in `frontend/src/components/AddTicketsModal.jsx`, props `{ eventId, eventName, onClose, onSuccess }`. Two modes: `mode === 'increase'` (select existing ticket type → `additionalQuantity` int input) and `mode === 'newType'` (name/price/quantity inputs). Local state via `useEvent(eventId)` (ticket types + availability), `useState` for form fields + busy + error. POST via existing `apiClient.post(...)` (X-CSRF-PROTECT auto-set). On 200/201 → `onSuccess()`; on 400/404 → show `getErrorMessage(err)` inline, leave state untouched. Submit disabled while busy or when selected/new fields invalid (matches EventForm validation: price > 0, quantity int > 0, name non-empty). | Inline the modal in AdminPanel; use TanStack `useMutation`. | Separate component keeps AdminPanel readable and mirrors the existing `DeleteConfirmationDialog` pattern in AdminPanel.jsx:46-73. `useMutation` adds no value over direct `apiClient` here since AdminPanel is not query-driven; keeping the manual style is consistent. |
| D-9 | Frontend tests (ATS-009 SHOULD) | Do NOT add Vitest in this change. `openspec/config.yaml` records "Frontend has NO test runner configured". ATS-009 uses SHOULD, not MUST. Verify the modal manually + via the backend contract; record the gap as a follow-up. | Add a minimal vitest config + jsdom for AddTicketsModal. | Adding vitest is its own infrastructure change (deps, config, lint baseline) outside this change's scope; doing it inline risks scope creep and config regressions. SHOULD permits deferral. |

## Data Model Impact

**NONE.** No migration, no new table, no column change. `AuditActionType` is stored as a string
varchar(100) so adding enum members needs no migration (ATS-005). Availability remains computed
(ATS-006 is satisfied by the existing math; neither operation writes availability). Confirmed
against `TicketType.cs` (no `RowVersion`/`CurrentlyReserved`) and the `DropCurrentlyReserved`
migration.

## Sequence Diagrams

### (a) Increment vs concurrent reservation (the lock is the serialization point)

```mermaid
sequenceDiagram
    participant Admin
    participant Reserver
    participant ES as EventService.AddTicketStockAsync
    participant RS as ReservationService.CreateReservationTransactionalAsync
    participant DB as PostgreSQL (TicketTypes row)

    Admin->>ES: POST stock {+N}
    Reserver->>RS: POST reserve {qty}
    Note over ES,RS: Both CreateExecutionStrategy + BeginTransaction
    ES->>DB: SELECT * FOR UPDATE (wins lock)
    RS->>DB: SELECT * FOR UPDATE (BLOCKS)
    ES->>DB: Quantity += N; SaveChanges; Commit
    ES-->>Admin: 200 {quantity: Quantity, available: ...}
    DB-->>RS: row now reflects new Quantity (lock released)
    RS->>DB: recompute available = Quantity - sold - reserved
    Note over RS: sees the incremented Quantity — no lost update, no oversell
    RS->>DB: INSERT Reservation; Commit
    RS-->>Reserver: 201 Reservation
```

### (b) New ticket type creation (transaction-only; no shared row lock needed)

```mermaid
sequenceDiagram
    participant Admin
    participant AC as AdminController
    participant ES as EventService.AddTicketTypeAsync
    participant DB as PostgreSQL

    Admin->>AC: POST /api/admin/events/{eventId}/ticket-types {name,price,quantity}
    AC->>AC: TryGetUserId(adminId)
    AC->>ES: AddTicketTypeAsync(eventId, name, price, quantity)
    ES->>ES: CreateExecutionStrategy.ExecuteAsync(BeginTransaction)
    ES->>DB: FindAsync(eventId) — null → KeyNotFoundException
    ES->>ES: validate name/price/quantity (incl MaxTicketQuantityPerOperation)
    ES->>DB: INSERT TicketType {new Guid, EventId, ...}; SaveChanges; Commit
    ES->>ES: MapTicketTypeWithAvailabilityAsync(tt)
    ES-->>AC: TicketTypeWithAvailability
    AC->>AC: TryLogAuditAsync(AddTicketType, Event, eventId)
    AC-->>Admin: 201 Created {id,name,price,quantity,available}
```

### (c) Frontend modal flow with TanStack invalidation

```mermaid
sequenceDiagram
    participant Admin
    participant AP as AdminPanel
    participant Modal as AddTicketsModal
    participant uE as useEvent(id)
    participant QC as useQueryClient
    participant API as apiClient (axios)
    participant BE as Backend

    Admin->>AP: click "Agregar entradas" on event row
    AP->>Modal: open {eventId, eventName}
    Modal->>uE: GET /events/{eventId} (TanStack, staleTime 60s)
    uE-->>Modal: EventWithAvailability incl TicketTypes[]
    Admin->>Modal: choose mode (increase|newType) + fields
    Admin->>Modal: submit
    Modal->>API: POST /api/admin/events/{eventId}/ticket-types/{ttId}/stock  (or .../ticket-types)
    alt 200/201
        API-->>Modal: ok
        Modal->>QC: invalidateQueries(['event', eventId])
        Modal->>QC: invalidateQueries(['events'])
        Modal->>AP: onSuccess()
        AP->>AP: refetch admin events list (existing loadData)
    else 400/404
        API-->>Modal: error
        Modal->>Modal: setError(getErrorMessage); state unchanged (ATS-007)
    end
```

## Backend Design

### Service signatures (IEventService.cs additions)

```csharp
/// <summary>Increments an existing TicketType.Quantity under SELECT...FOR UPDATE. Mirrors ReservationService.</summary>
/// <exception cref="KeyNotFoundException">Event or ticket type not found, or EventId mismatch.</exception>
/// <exception cref="ArgumentException">additionalQuantity <= 0 or > MaxAdditionalStock.</exception>
Task<TicketTypeWithAvailability> AddTicketStockAsync(Guid eventId, Guid ticketTypeId, int additionalQuantity);

/// <summary>Creates a new TicketType on an existing event (transaction-only, no row lock).</summary>
/// <exception cref="KeyNotFoundException">Event not found.</exception>
/// <exception cref="ArgumentException">Invalid name/price/quantity.</exception>
Task<TicketTypeWithAvailability> AddTicketTypeAsync(Guid eventId, string name, decimal price, int quantity);
```

### Controller endpoints (AdminController.cs additions)

```csharp
[HttpPost("events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/stock")]
public async Task<IActionResult> AddTicketStock(Guid eventId, Guid ticketTypeId, [FromBody] AddTicketStockRequest request)
{
    if (!TryGetUserId(out var adminId)) return Unauthorized();
    try
    {
        var tt = await _eventService.AddTicketStockAsync(eventId, ticketTypeId, request.AdditionalQuantity);
        await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.AddTicketStock,
            AuditResourceType.Event, eventId, Truncate($"Admin added {request.AdditionalQuantity} tickets to ticket type {tt.Name} (event {eventId})", 1000)));
        return Ok(tt);
    }
    catch (KeyNotFoundException) { return NotFound(new { error = "Event or ticket type not found" }); }
    catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    catch (UnauthorizedAccessException) { return Forbid(); }
    catch (Exception ex) { _logger.LogError(ex, "Error adding ticket stock"); return StatusCode(500, new { error = "An error occurred while adding ticket stock" }); }
}

[HttpPost("events/{eventId:guid}/ticket-types")]
public async Task<IActionResult> AddTicketType(Guid eventId, [FromBody] AddTicketTypeRequest request)
{
    if (!TryGetUserId(out var adminId)) return Unauthorized();
    try
    {
        var tt = await _eventService.AddTicketTypeAsync(eventId, request.Name, request.Price, request.Quantity);
        await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.AddTicketType,
            AuditResourceType.Event, eventId, Truncate($"Admin created ticket type {tt.Name} (price {tt.Price}, quantity {tt.Quantity}) for event {eventId}", 1000)));
        return CreatedAtAction(nameof(AddTicketType), new { eventId, ticketTypeId = tt.Id }, tt);
    }
    catch (KeyNotFoundException) { return NotFound(new { error = "Event not found" }); }
    catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    catch (UnauthorizedAccessException) { return Forbid(); }
    catch (Exception ex) { _logger.LogError(ex, "Error adding ticket type"); return StatusCode(500, new { error = "An error occurred while adding the ticket type" }); }
}

// in openspec records below — request DTOs:
public record AddTicketStockRequest(int AdditionalQuantity);
public record AddTicketTypeRequest(string Name, decimal Price, int Quantity);

private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
```

> Note: AdminController currently injects `IAdminService, IAuthService, IAuditLogService, ILogger`.
> Add `IEventService` to its constructor and DI is already registered (EventController resolves it).

### Exception → HTTP mapping table

| Service exception | HTTP | Body | Spec |
|---|---|---|---|
| `KeyNotFoundException` (event/TT missing or EventId mismatch) | 404 | `{error:"Event or ticket type not found"}` / `{error:"Event not found"}` | ATS-002 "Unknown event or mismatched ticket type" |
| `ArgumentException` (invalid quantity) | 400 | `{error: ex.Message}` | ATS-002 "Invalid additional quantity", ATS-004 "Invalid payload" |
| `UnauthorizedAccessException` | 403 | — (Forbid) | ATS-001 (also enforced by `RequireAdminRole`) |
| Other | 500 | `{error:"An error occurred ...}` | existing pattern |
| Non-admin at pipeline | 403 | — | ATS-001 (class-level `RequireAdminRole`) |

### Audit enum additions (Models/AuditLog.cs)

```csharp
public enum AuditActionType
{
    ViewUsers, ViewEvents, UpdateEvent, DeleteEvent, CreateUser,
    ProcessWebhook, ValidateQr,
    AddTicketStock,   // NEW (ATS-005) — ActionType varchar(100), no migration
    AddTicketType     // NEW (ATS-005)
}
```

### Validation rules table

| Field | Rule | Constant | Source mapping |
|---|---|---|---|
| `additionalQuantity` | int > 0 and ≤ 1000 | `MaxAdditionalStock = 1000` | ATS-002 increment |
| `name` (new type) | non-empty, trimmed, ≤ 100 chars | — (TicketType.Name maxlength 100) | ATS-004 |
| `price` (new type) | decimal ≥ 0 | — | ATS-004 (matches CreateEventAsync price < 0 rule) |
| `quantity` (new type) | int > 0 and ≤ 1000 | `MaxTicketQuantityPerOperation = 1000` | ATS-004 |
| event existence | `_context.Events.FindAsync(eventId)` non-null | — | both |
| EventId match (increment) | ticket type loaded with `AND EventId = eventId`; null → KeyNotFound | — | ATS-002 |

Constants live as `private const int` on `EventService` (no shared validator extracted — see D-7).

## Frontend Design

### Component tree (additions)

```
AdminPanel.jsx
├── events table row  →  <Button onClick={openAddTickets(event)}>Agregar entradas</Button>
└── {addTicketsTarget && <AddTicketsModal ... />}

AddTicketsModal.jsx   (new)
├── useEvent(eventId)            ← TanStack, returns EventWithAvailability { TicketTypes[] }
├── mode toggle: 'increase' | 'newType'
├── increase:  <select ticketType> + <input additionalQuantity>
├── newType:   <input name> + <input price> + <input quantity>
├── apiClient.post('/admin/events/{id}/ticket-types/{ttId}/stock' | '/admin/events/{id}/ticket-types', body)
└── on success: queryClient.invalidateQueries(['event', id]) + (['events'])  →  onSuccess()
```

### State & invalidation

- `import { useQueryClient } from '@tanstack/react-query'` + `import { queryKeys } from '../lib/queryKeys.js'`
  inside `AddTicketsModal`. AdminPanel is already rendered inside `QueryClientProvider` (the buyer
  catalog uses it), so `useQueryClient` resolves — no provider change.
- On success: `queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) })` then
  `{ queryKey: queryKeys.events }`. Then call `onSuccess()` so AdminPanel re-runs its `loadData`
  (existing manual refetch of `/admin/events`).
- On error: `setError(getErrorMessage(err))` inline, no state mutation (ATS-007 "Failure shows error").
- Submit button `disabled={busy || !valid}`.

### apiClient calls (reuse)

```jsx
await apiClient.post(`/admin/events/${eventId}/ticket-types/${ttId}/stock`, { additionalQuantity: Number(qty) })
// or
await apiClient.post(`/admin/events/${eventId}/ticket-types`, { name, price: Number(price), quantity: Number(qty) })
```

`apiClient` auto-injects `X-CSRF-PROTECT` on POST and redirects on 401 (client.js:15-40). No change needed.

### EventForm change (D-2)

In `EventForm.jsx`, wrap the `fieldset.ticket-types-section` (currently always rendered, lines
352-462) so it only shows when `isCreate`. In edit mode render instead:

```jsx
{!isCreate && (
  <div className="ticket-types-section" role="note">
    <h2>Tipos de entrada</h2>
    <p>El stock de entradas se gestiona desde el panel de administración (acción "Agregar entradas").
       Los tipos de entrada no se editan aquí.</p>
  </div>
)}
```

This deletes the silent no-op (the edit PUT body at EventForm.jsx:147-152 already omits ticket types).

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Services/IEventService.cs` | Modify | Add `AddTicketStockAsync`, `AddTicketTypeAsync`; request records `AddTicketStockRequest`, `AddTicketTypeRequest`. |
| `backend/Services/EventService.cs` | Modify | Implement both methods; add `MaxAdditionalStock`/`MaxTicketQuantityPerOperation` consts; add `MapTicketTypeWithAvailabilityAsync` helper reusing `ComputeAvailabilityAggregatesAsync`. |
| `backend/Controllers/AdminController.cs` | Modify | Inject `IEventService`; add 2 endpoints; add `Truncate` helper. |
| `backend/Models/AuditLog.cs` | Modify | Add `AddTicketStock`, `AddTicketType` to `AuditActionType`. |
| `frontend/src/components/AddTicketsModal.jsx` | Create | Modal component (D-8). |
| `frontend/src/pages/AdminPanel.jsx` | Modify | Add "Agregar entradas" button per row + modal mount + `useQueryClient` invalidation wiring (D-3). |
| `frontend/src/components/EventForm.jsx` | Modify | Hide ticket-types fieldset in edit mode, show notice (D-2). |
| `backend/Tests/AdminControllerTicketStockTests.cs` | Create | Controller-level RED tests (ATS-009). |
| `backend/Tests/EventServiceTicketStockTests.cs` | Create | Service-level RED tests, SQLite in-memory + concurrency (ATS-009). |

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (controller, RED first) | 200/403/404/400 mapping for both endpoints; audit `AuditLogContext` (action type, resource id, truncation) verified via `Mock<IAuditLogService>` | `Mock<IEventService>` + `SetAuthenticatedUser` (existing AdminControllerTests.cs pattern). 403 path tested via `[Authorize(Policy=RequireAdminRole)]` semantics — keep consistent with existing tests. |
| Unit (service) | Increment persists `Quantity += N`; mismatched EventId → `KeyNotFoundException`; invalid quantity → `ArgumentException`; new-type insert + 201 shape; availability recomputed (ATS-006). | SQLite in-memory `ReservationStockTestDbContext`-style subclass (ReservationStockTests.cs). |
| Unit (concurrency, ATS-003) | Concurrent increment (+5) and reservation (qty 8) on Quantity=10 serialize: no lost update, no oversell. | Two parallel `Task`s on a SQLite in-memory connection sharing the context (existing ReservationStockTests pattern) — SQLite exercises the no-op-UPDATE branch; PostgreSQL `FOR UPDATE` is structurally identical to the proven reservation path. |
| Integration | `POST /api/admin/events/{id}/ticket-types/{ttId}/stock` requires admin cookie; anon → 403 | `WebApplicationFactory<Program>` + `AuthCookieTests.cs` pattern. |
| E2E / Frontend | ATS-009 SHOULD — **deferred** (D-9). Manual verification: modal submit increments, invalidation refetches EventDetail/catalog. | Manual + follow-up change to add Vitest config. |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Both new endpoints are standard `[ApiController]` actions on the
existing `AdminController`; no `Process.Start`, no dynamic routing, no shell.

## Migration / Rollout

No migration required. Purely additive — two endpoints, two service methods, two enum members,
one new component, one EventForm edit-mode gate. Rollback is `git revert <sha>`; no DB cleanup
needed (an already-applied `Quantity` increment is real capacity, not inventory to undo).

## Open Questions

- [ ] None blocking. (D-9 follow-up: a separate change should add Vitest config to the frontend so ATS-009's SHOULD can become MUST for future UI work.)