# Design: Admin Edit of Pre-Approval Ticket Types

## Technical Approach

Add an admin-only `PUT` replacement. The service loads tracked ticket types, applies the guards, validates the payload, and updates/inserts/deletes in the existing execution-strategy transaction. It returns batch-recomputed `TicketTypeWithAvailability[]`; the controller audits only after commit.

## Architecture Decisions

| Decision | Choice | Rejected alternative | Rationale |
|---|---|---|---|
| API shape | One atomic replacement PUT | Separate CRUD calls | One request is all-or-nothing. |
| Guard order | exists → `EnsureMutable` → eligibility/history → payload → IDs | status/payload first | Every past event must produce `event-finalized`, including past Approved/Rejected events. |
| History | `Any` Ticket OR Reservation by `TicketTypeId`, every state | only active sales/reservations | Restrict FKs protect every state; Pending can follow Approved. |
| Concurrency | No row lock for eligible history-free events | `FOR UPDATE` on every type | Invariant excludes commercial rows; transaction/FK remain safety net. Revisit if sales concurrency changes. |
| Error identifiers | Preserve `ticket-types-not-editable` and `ticket-types-referenced` | Rename them | Canonical strings; renaming affects specs. |

## Data Flow

```text
AdminPanel → EditTicketsModal → PUT controller → ReplaceTicketTypesAsync
                                      │             ├─ tracked Event.Include(TicketTypes)
                                      │             ├─ validate/history/replace transaction
                                      │             └─ availability aggregates
                                       └─ success → audit + cache invalidation
```

Flow: authenticate → find → finalized → eligibility/history → payload/IDs → replace → commit → availability → one audit/200; failures rollback/no audit.

## Backend Design

In `IEventService.cs` add:

```csharp
Task<IReadOnlyList<TicketTypeWithAvailability>> ReplaceTicketTypesAsync(
    Guid eventId, ReplaceTicketTypesRequest request);
public sealed class ReplaceTicketTypesRequest { public List<ReplaceTicketTypeRequest> TicketTypes { get; set; } = new(); }
public sealed record ReplaceTicketTypeRequest(Guid? Id, string Name, decimal Price, int Quantity);
```

The `[HttpPut("events/{eventId:guid}/ticket-types")]` action uses `TryGetUserId`, maps `KeyNotFoundException` to 404, `ArgumentException` to 400, and domain exceptions to RFC 7807 409 with fields and request-path instance. Kebab-case types remain exactly `event-finalized`, `ticket-types-not-editable`, and `ticket-types-referenced`; unexpected failures return 500. Add the two domain exceptions and `AuditActionType.EditTicketTypes` (string conversion means no migration).

Inside one execution-strategy transaction, use tracked `Events.Include(e => e.TicketTypes).SingleOrDefaultAsync`. Ticket and Reservation both carry `EventId`; the history predicate intentionally uses `TicketTypeId` belonging to this event, without state filters. Validate ≥1 row, trimmed name 1..100, price `>= 0`, and quantity 1..1000, then verify every ID belongs to the event. Update tracked rows, add ID-less rows, remove absent rows, commit, and map availability from a sold/reserved aggregate query.

## Frontend Design

Create `EditTicketsModal.jsx`, mirroring `AddTicketsModal` and `useDialog`: `useManagementEvent(id)`, loading/error states, local add/edit/delete rows, and complete-list PUT. Validate name ≤100, price `>=0`, integer quantity 1..1000, and non-empty list. Use labels, `aria-invalid`/`aria-describedby`, `role="alert"`, and first-error focus. For `ticket-types-referenced`, explain that add-only remains available. On success invalidate `queryKeys.managementEvent(id)`, `queryKeys.event(id)`, and `queryKeys.events`, then close/reload AdminPanel.

Update `AddTicketsModal.jsx` to invalidate three keys, including `managementEvent`.

AdminPanel gets `editTicketsTarget`; Pending/Rejected render “Editar entradas” with a ticket-edit icon; Approved keeps “Agregar entradas”/`TicketPlus`; past mutation actions are disabled. Wire desktop/mobile actions without changing organizer “Editar”.

## File Changes

| File | Action | Description |
|---|---|---|
| `backend/Services/IEventService.cs` | Modify | DTOs and contract. |
| `backend/Services/EventService.cs` | Modify | Replacement transaction and availability. |
| `backend/Models/Exceptions.cs`, `AuditLog.cs` | Modify | 409 exceptions and audit enum. |
| `backend/Controllers/AdminController.cs` | Modify | PUT, ProblemDetails, audit. |
| `frontend/src/components/EditTicketsModal.jsx` | Create | Full-edit dialog. |
| `frontend/src/components/AddTicketsModal.jsx` | Modify | Three-key invalidation. |
| `frontend/src/pages/AdminPanel.jsx` | Modify | Status action and modal state. |
| `backend/Tests/EventServiceTicketStockTests.cs`, `AdminControllerTicketStockTests.cs` | Modify | Strict-TDD service/controller coverage. |
| `frontend/src/components/__tests__/EditTicketsModal.test.jsx`, `frontend/src/pages/AdminPanel.test.jsx` | Create/Modify | Vitest UI and action coverage. |
| `frontend/src/components/__tests__/AddTicketsModal.test.jsx` | Create | Add-only behavior and invalidation. |

## Testing Strategy

Extend SQLite `EventServiceTicketStockTests` with success, mixed replacement, validation/cap, foreign IDs, all-state history, finalized-before-status, availability, and no-mutation cases. A fault-injecting context throws after base `SaveChangesAsync` inside the transaction; a fresh context must see the original list. Extend Moq `AdminControllerTicketStockTests` for 404/400/three 409s/500, exact ProblemDetails, and audit count; cover 403 through the authorization integration harness. Vitest covers `EditTicketsModal` loading, CRUD, `$0`, focus/a11y, PUT payload, invalidation, blocked-Rejected copy, and status/past actions. `AddTicketsModal` covers add-only behavior and three-key invalidation.

## Threat Matrix

| Boundary | Applicability | Design response / RED test |
|---|---|---|
| Documentation-like paths | N/A — no executable-file classification | None. |
| Git repository selection | N/A — no VCS automation | None. |
| Commit state | N/A — no commit commands | None. |
| Push state | N/A — no push automation | None. |
| PR commands | N/A — no PR automation | None. |

## Migration / Rollout

No migration or feature flag. Deploy backend and frontend together; rollback is a code revert only.

## Risks / Open Questions

- Add-only methods remain status-agnostic per proposal; blocked events use them.
- Legacy totals above 1000 are rejected, but may be lowered and resubmitted.
- No schema changes; verify provider-specific transaction behavior during apply.
