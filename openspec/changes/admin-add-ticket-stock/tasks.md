# Tasks: Admin Add Ticket Stock

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~950–1100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (service) → PR 2 (controller) → PR 3 (frontend), or single PR w/ size:exception |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Service layer: IEventService + EventService + service tests (RED→GREEN) | PR 1 | `dotnet test --filter EventServiceTicketStockTests` | N/A — logic proven by unit tests; e2e needs PR 2 | Revert IEventService/EventService + test file |
| 2 | Controller: AuditLog enum + AdminController endpoints + controller tests | PR 2 | `dotnet test --filter AdminControllerTicketStockTests` | `dotnet run`; curl POST both endpoints w/ admin cookie; check audit row | Revert AdminController/AuditLog + test file |
| 3 | Frontend: AddTicketsModal + AdminPanel + EventForm | PR 3 | `npm run build && npm run lint` | Manual: increment → EventDetail updates; new type in catalog | Revert 3 frontend files |

## Phase 1: Backend RED Tests (strict TDD)

- [x] 1.1 Create `backend/Tests/AdminControllerTicketStockTests.cs` (`Mock<IEventService>` + `SetAuthenticatedUser`): 200 increment, 404 unknown event/mismatch TT, 400 invalid qty, 201 new type, 404 unknown event, 400 invalid payload (ATS-002/004, D-5); audit verify via `Mock<IAuditLogService>` → `AddTicketStock`/`AddTicketType` + `AuditResourceType.Event` + Details ≤1000 (ATS-005, D-6).
- [x] 1.2 Create `backend/Tests/EventServiceTicketStockTests.cs` (SQLite in-memory, ReservationStockTests pattern): increment persists `Quantity+=N` + availability recompute (ATS-002/006); `ArgumentException` invalid qty; `KeyNotFoundException` mismatch (ATS-002); new-type insert + validation (ATS-004); parallel (+5 inc vs qty-8 reservation) serialize — no lost update/oversell (ATS-003, D-1).

## Phase 2: Backend Implementation (GREEN)

- [x] 2.1 `backend/Services/IEventService.cs`: add `AddTicketStockAsync(Guid, Guid, int)` + `AddTicketTypeAsync(Guid, string, decimal, int)` → `Task<TicketTypeWithAvailability>`; records `AddTicketStockRequest(int)`, `AddTicketTypeRequest(string, decimal, int)`.
- [x] 2.2 `backend/Services/EventService.cs`: consts `MaxAdditionalStock=1000`, `MaxTicketQuantityPerOperation=1000` (D-7); `AddTicketStockAsync` mirroring `ReservationService.CreateReservationTransactionalAsync` (FOR UPDATE; Npgsql/SQLite no-op-UPDATE/InMemory branches, D-1); `AddTicketTypeAsync` transaction insert (ATS-004); helper `MapTicketTypeWithAvailabilityAsync` reusing `ComputeAvailabilityAggregatesAsync` (D-4, ATS-006).
- [x] 2.3 `backend/Models/AuditLog.cs`: add `AddTicketStock`, `AddTicketType` to `AuditActionType` (ATS-005, no migration).
- [x] 2.4 `backend/Controllers/AdminController.cs`: inject `IEventService`; both `[HttpPost]` endpoints, D-5 error mapping, `Truncate` helper, `TryLogAuditAsync` (ATS-001/005, D-6).
- [x] 2.5 `dotnet test` from `backend/` → suite green, zero regressions (ATS-009).

## Phase 3: Frontend

- [x] 3.1 Create `frontend/src/components/AddTicketsModal.jsx` (D-8): props `{eventId, eventName, onClose, onSuccess}`; modes increase|newType; `useEvent(id)`; `apiClient.post`; success → `invalidateQueries(['event',id])` + `(['events'])` → `onSuccess()` (ATS-007); error → `getErrorMessage` inline, state untouched; submit `disabled={busy||!valid}`.
- [x] 3.2 Modify `frontend/src/pages/AdminPanel.jsx` (D-3): "Agregar entradas" button per row + modal mount + `useQueryClient`; onSuccess re-runs existing `loadData`.
- [x] 3.3 Modify `frontend/src/components/EventForm.jsx` (D-2): hide `fieldset.ticket-types-section` in edit mode → static notice; create mode unchanged (ATS-008). Existing edit-mode test updated (approval test) to assert the new behavior.

## Phase 4: Verification

- [x] 4.1 `dotnet test` from `backend/` — full suite green (ATS-009). 483 passing (451 baseline + 32 new); 6 pre-existing failures unchanged; 1 flaky (ConfigValidationTests, passes in isolation).
- [x] 4.2 `npm run build` + `npm run lint` from `frontend/`. Build passes; 6 pre-existing lint errors in untouched files, 0 in changed files.
- [ ] 4.3 Manual smoke: increment → EventDetail "X disponibles de Y"; new type in buyer catalog; non-admin 403; audit rows written. D-9: Vitest deferred, record gap as follow-up. (Frontend vitest suite exists: 21/21 EventForm, 32/32 AdminPanel pass; 26 pre-existing failures in untouched files.)
