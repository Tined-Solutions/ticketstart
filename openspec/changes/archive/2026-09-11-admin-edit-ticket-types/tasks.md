# Tasks: Admin Edit of Pre-Approval Ticket Types

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1,300–1,700 (backend prod ~200, backend tests ~450, frontend prod ~350, frontend tests ~500) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes (>400 authored lines) |
| Suggested split | One PR under the maintainer's 2,000-line budget; 4 work-unit commits |
| Delivery strategy | single-pr (2,000-line review budget) |
| Chain strategy | size-exception |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

> Honest call: this change is well above the default 400-line guard. It fits only because the maintainer set a 2,000-line budget for one PR; `size:exception` must be recorded before apply.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | Domain contracts + `ReplaceTicketTypesAsync` + SQLite tests | PR 1 | `cd backend && dotnet test --filter FullyQualifiedName~EventServiceTicketStockTests` | N/A — SQLite in-memory transaction is the runtime boundary | `IEventService.cs`, `EventService.cs`, `Exceptions.cs`, `AuditLog.cs` + service tests |
| 2 | PUT controller + audit + Moq tests | PR 1 | `cd backend && dotnet test --filter FullyQualifiedName~AdminControllerTicketStockTests` | `WebApplicationFactory<Program>` 403 case | `AdminController.cs` + controller tests |
| 3 | `EditTicketsModal` + Vitest | PR 1 | `cd frontend && npm test -- EditTicketsModal` | N/A — jsdom is the repo convention; no browser harness | `EditTicketsModal.jsx` + its test |
| 4 | `AdminPanel` action + `AddTicketsModal` invalidation + tests | PR 1 | `cd frontend && npm test -- AdminPanel AddTicketsModal` | N/A — jsdom is the repo convention; no browser harness | `AdminPanel.jsx`, `AddTicketsModal.jsx` + tests |

Dependency order: 1 → 2 → 3 → 4.

## Phase 1: Backend contracts

- [x] 1.1 Add `ReplaceTicketTypesRequest`/`ReplaceTicketTypeRequest` DTOs and `ReplaceTicketTypesAsync` to `backend/Services/IEventService.cs`.
- [x] 1.2 Add `TicketTypesNotEditableException` (`ticket-types-not-editable`) and `TicketTypesReferencedException` (`ticket-types-referenced`) to `backend/Models/Exceptions.cs`.
- [x] 1.3 Add `AuditActionType.EditTicketTypes` to `backend/Models/AuditLog.cs` (varchar-backed, no migration).

## Phase 2: Backend service (strict TDD)

- [x] 2.1 RED: extend `backend/Tests/EventServiceTicketStockTests.cs` — mixed add/edit/delete, empty list, name/price/qty validation, per-type total ≤1000, foreign id, all-state history, Pending-with-history, finalized-before-status, recomputed availability, no-mutation.
- [x] 2.2 RED: fault-injection context throws after `SaveChangesAsync`; a fresh context sees the original list (ATE-001 atomicity).
- [x] 2.3 GREEN: implement `ReplaceTicketTypesAsync` in `backend/Services/EventService.cs` — tracked `Include`, guard order finalized→eligibility/history→payload→IDs, update/add/remove, transaction rollback, availability mapping.
- [x] 2.4 REFACTOR: extract history/validation helpers; keep green.

## Phase 3: Backend controller (strict TDD)

- [x] 3.1 RED: extend `backend/Tests/AdminControllerTicketStockTests.cs` (Moq) — 404/400/three 409s/500, exact ProblemDetails fields, audit once on success, never on failure.
- [x] 3.2 GREEN: add `[HttpPut("events/{eventId:guid}/ticket-types")]` to `backend/Controllers/AdminController.cs` — `TryGetUserId`, exception→RFC 7807 mapping, `TryLogAuditAsync` on success only.
- [x] 3.3 RED/GREEN: prove 403 non-admin via `WebApplicationFactory<Program>` authorization harness (ATS-001).

## Phase 4: EditTicketsModal (strict TDD)

- [x] 4.1 RED: create `frontend/src/components/__tests__/EditTicketsModal.test.jsx` — `managementEvent` load, CRUD rows, `$0` price, validation/focus/`role="alert"`, PUT payload, three-key invalidation, blocked-Rejected copy.
- [x] 4.2 GREEN: create `frontend/src/components/EditTicketsModal.jsx` — `useManagementEvent`, `useDialog` a11y, complete-list PUT, invalidate `managementEvent(id)`/`event(id)`/`events`, 409 copy.

## Phase 5: AdminPanel + AddTicketsModal (strict TDD)

- [x] 5.1 RED: create `frontend/src/components/__tests__/AddTicketsModal.test.jsx` — add-only behavior + `managementEvent(id)` invalidation (ATS-007).
- [x] 5.2 GREEN: update `frontend/src/components/AddTicketsModal.jsx` invalidation to the three keys.
- [x] 5.3 RED: extend `frontend/src/pages/AdminPanel.test.jsx` — Pending/Rejected show a distinct "Editar entradas" + edit icon, Approved keeps "Agregar entradas", past disabled.
- [x] 5.4 GREEN: update `frontend/src/pages/AdminPanel.jsx` — `editTicketsTarget`, status-aware label/icon, mount `EditTicketsModal`, keep organizer "Editar".

## Phase 6: Verification

- [x] 6.1 `cd backend && dotnet test` — full suite green.
- [x] 6.2 `cd frontend && npm test` — full suite green.
- [x] 6.3 Confirm ATE/ATS/PEM scenarios covered; do NOT edit canonical `openspec/specs/` (archive-time work).
