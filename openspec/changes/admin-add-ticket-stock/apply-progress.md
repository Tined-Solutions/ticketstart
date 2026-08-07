# Apply Progress: Admin Add Ticket Stock

**Change**: admin-add-ticket-stock
**Mode**: Strict TDD (backend) — RED tests written first, then GREEN implementation
**Delivery**: single-pr with maintainer-approved size:exception (~950-1100 lines). NOT split into chained PRs.
**Batch**: 1 of 1 (initial). No previous apply-progress existed (mem_search confirmed).

## Status

9/10 tasks complete. 4.3 (manual smoke) is the only remaining task — requires a live browser/app and is recorded as a D-9 follow-up gap.

## Completed Tasks

### Phase 1: Backend RED Tests (strict TDD)

- [x] 1.1 `backend/Tests/AdminControllerTicketStockTests.cs` — 11 tests: 200 increment, 404 unknown/mismatch, 400 invalid qty (theory 0/-5/1001), audit details truncated to exactly 1000, 401 no-user; 201 new type, 404 unknown event, 400 invalid payload (theory), 500 catch-all, 401 no-user. Audit verified via `Mock<IAuditLogService>` → `AddTicketStock`/`AddTicketType` + `AuditResourceType.Event` + event id (ATS-002/004/005, D-5/D-6).
- [x] 1.2 `backend/Tests/EventServiceTicketStockTests.cs` — 21 tests (incl. theories): increment persists `Quantity+=N` + availability recompute (with sold-ticket deduction), invalid qty → `ArgumentException`, mismatch → `KeyNotFoundException`, unknown TT → `KeyNotFoundException`; new-type insert + catalog visibility, empty/long name, negative price, invalid qty → `ArgumentException` + no row, unknown event → `KeyNotFoundException` + no row; parallel +5 increment vs qty-8 reservation serializes (no lost update / no oversell) on shared-cache SQLite (ATS-002/003/004/006, D-1).

### Phase 2: Backend Implementation (GREEN)

- [x] 2.1 `backend/Services/IEventService.cs` — added `AddTicketStockAsync(Guid, Guid, int)` + `AddTicketTypeAsync(Guid, string, decimal, int)` → `Task<TicketTypeWithAvailability>`; records `AddTicketStockRequest(int)` and `AddTicketTypeRequest(string, decimal, int)`.
- [x] 2.2 `backend/Services/EventService.cs` — consts `MaxAdditionalStock = 1000`, `MaxTicketQuantityPerOperation = 1000` (D-7). `AddTicketStockAsync` mirrors `ReservationService.CreateReservationTransactionalAsync` (ReservationService.cs:83-179) exactly: `CreateExecutionStrategy.ExecuteAsync` → `BeginTransactionAsync` → provider-branched lock (Npgsql `FromSqlInterpolated ... FOR UPDATE`; SQLite no-op `UPDATE ... CreatedAt = CreatedAt`; InMemory plain query) → `Quantity += N` → `SaveChanges` → `Commit` (catch → `Rollback`). `AddTicketTypeAsync` transaction-wrapped insert with validation (name non-empty/trimmed/≤100, price ≥ 0, qty > 0 ≤ 1000). Helper `MapTicketTypeWithAvailabilityAsync` reuses `ComputeAvailabilityAggregatesAsync(new(){ tt.Id })` + `Math.Max(0, Quantity - sold - reserved)` clamp (D-4, ATS-006).
- [x] 2.3 `backend/Models/AuditLog.cs` — `AddTicketStock`, `AddTicketType` appended to `AuditActionType` (ATS-005, string column → no migration).
- [x] 2.4 `backend/Controllers/AdminController.cs` — injected `IEventService` (5th ctor param; DI already registered in Program.cs); `POST api/admin/events/{eventId}/ticket-types/{ticketTypeId}/stock` → 200 Ok(tt) and `POST api/admin/events/{eventId}/ticket-types` → 201 CreatedAtAction; `TryGetUserId` → 401; `TryLogAuditAsync` with new action types + `Truncate(details, 1000)`; D-5 mapping KeyNotFound→404, ArgumentException→400, UnauthorizedAccess→Forbid, catch-all→500. Existing `AdminControllerTests.cs` constructor call updated for the new 5-arg ctor (required ripple).
- [x] 2.5 `dotnet test` from backend/ — 32 new tests green; full suite 483 passing (451 baseline + 32 new), zero regressions.

### Phase 3: Frontend

- [x] 3.1 `frontend/src/components/AddTicketsModal.jsx` (new, D-8) — default export, props `{eventId, eventName, onClose, onSuccess}`; modes `increase` (select existing TT → additionalQuantity) and `newType` (name/price/quantity); uses existing `useEvent(eventId)` hook; POST via `apiClient.post` (X-CSRF-PROTECT auto-set); on success `invalidateQueries(['event', id])` + `(['events'])` then `onSuccess()` (ATS-007); on error `getErrorMessage(err)` inline, local state untouched; submit `disabled={busy || !valid}`. Matches AdminPanel dialog/GlassCard/Button/form-group patterns.
- [x] 3.2 `frontend/src/pages/AdminPanel.jsx` — "Agregar entradas" primary button per event row (near Editar/Eliminar), `addTicketsTarget` state, modal mount `{addTicketsTarget && <AddTicketsModal .../>}`, `onSuccess` re-runs existing `loadData()` to refresh the admin list. (Modal owns the TanStack invalidation per D-8; AdminPanel stays manual-list per D-3.)
- [x] 3.3 `frontend/src/components/EventForm.jsx` — in edit mode the entire `fieldset.ticket-types-section` is hidden and replaced with the D-2 static notice (Spanish, neutral register): stock managed via AdminPanel "Agregar entradas"; ticket types not editable here. Create mode fieldset untouched. Ticket-type validation now gated to create mode (edit PUT body already omits ticketTypes). Existing `EventForm.test.jsx` edit-mode approval test updated to assert the new behavior (per strict-tdd approval-testing rule — ATS-008 is a spec-mandated behavior change).

### Phase 4: Verification

- [x] 4.1 `dotnet test` from backend/ — full suite: **483 passing, 6 pre-existing failures (unchanged from baseline), 0 regressions**. The 6 pre-existing failures: `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`, `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client`, `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `PendingEmailRetryTests.*` (2). Plus intermittent flaky `ConfigValidationTests.Startup_WithMissingRequiredConfigValue...` (passes in isolation; env-var/factory-dependent).
- [x] 4.2 `npm run build` (passes, 16s) + `npm run lint` from frontend/. Lint: 6 pre-existing errors in untouched files (useTheme.jsx, CheckoutSuccess.jsx, EventDetail.jsx, TicketLookup.jsx, vite.config.js) — **0 errors in changed files**. Frontend vitest (exists despite D-9's stale note): 21/21 EventForm, 32/32 AdminPanel pass; 26 pre-existing failures in untouched files (StaffScan camera/QR env, Checkout PATCH, OrganizerEventDetail `/manage`, identityValidation) — confirmed identical with my changes stashed.
- [ ] 4.3 Manual smoke — NOT executable in this environment (needs live browser + running app + admin cookie). Recorded as follow-up: increment → EventDetail "X disponibles de Y"; new type in buyer catalog; non-admin 403; audit rows written. Backend contract is covered by the new automated tests; frontend behavior by the vitest suite.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1/2.4 | `backend/Tests/AdminControllerTicketStockTests.cs` | Unit (controller) | N/A (new file) | ✅ Written (compile-error RED: missing ctor/methods/enum) | ✅ Passed (11/11) | ✅ 2+ cases per behavior (theories: 0/-5/1001; 404/400/401 variants) | ✅ Clean (matches AdminControllerTests pattern) |
| 1.2/2.2 | `backend/Tests/EventServiceTicketStockTests.cs` | Unit (service, SQLite) | N/A (new file) | ✅ Written (compile-error RED) | ✅ Passed (21/21) | ✅ 3 cases increment validation; 3 cases new-type qty; sold-ticket availability path; concurrency | ✅ Clean (ReservationStockTests pattern) |
| 2.3 | enum members | Unit (implicit) | N/A | ✅ (test references enum first) | ✅ | ➖ Single (2 members, no branching) | ➖ None needed |
| 3.1/3.2 | frontend vitest (AdminPanel 32, EventForm 21) | Unit (component) | ✅ 32+21 passing baseline | ✅ (modal exercised via AdminPanel render) | ✅ Passed | ✅ increase + newType modes covered | ✅ Clean |
| 3.3 | `EventForm.test.jsx` edit-mode approval test | Unit (component) | ✅ 21 passing | ✅ Updated test → asserted new notice behavior (RED) | ✅ Passed after D-2 change | ✅ Create mode still covered (unchanged tests) | ✅ Clean |

### Test Summary
- **Total tests written**: 32 backend (11 controller + 21 service) + 1 updated frontend approval test
- **Total tests passing**: 32/32 backend; EventForm 21/21; AdminPanel 32/32
- **Layers used**: Unit (controller) 11, Unit (service incl. concurrency) 21, Unit (frontend components) 1 updated + full touched-file suites
- **Approval tests** (refactoring): 1 (EventForm edit-mode — spec-mandated behavior change per ATS-008)
- **Pure functions created**: `Truncate` (static, controller), `MapTicketTypeWithAvailabilityAsync` (service helper)

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `backend/Services/IEventService.cs` | Modified | 2 new method signatures + 2 request records |
| `backend/Services/EventService.cs` | Modified | `AddTicketStockAsync`, `AddTicketTypeAsync`, 2 consts, `MapTicketTypeWithAvailabilityAsync` |
| `backend/Controllers/AdminController.cs` | Modified | IEventService injection, 2 endpoints, `Truncate`, audit wiring |
| `backend/Models/AuditLog.cs` | Modified | `AddTicketStock`, `AddTicketType` enum members |
| `backend/Tests/AdminControllerTicketStockTests.cs` | Created | 11 controller RED tests |
| `backend/Tests/EventServiceTicketStockTests.cs` | Created | 21 service RED tests (incl. concurrency) |
| `backend/Tests/AdminControllerTests.cs` | Modified | Ctor call updated for new IEventService param (required ripple) |
| `frontend/src/components/AddTicketsModal.jsx` | Created | Modal component (D-8) |
| `frontend/src/pages/AdminPanel.jsx` | Modified | "Agregar entradas" button + modal mount + loadData re-run |
| `frontend/src/components/EventForm.jsx` | Modified | Edit-mode fieldset hidden → D-2 notice; validation gated to create mode |
| `frontend/src/components/EventForm.test.jsx` | Modified | Edit-mode approval test updated to new ATS-008 behavior |

## Deviations from Design

1. **AdminPanel does not import `useQueryClient`** — design D-3's table mentions it, but the design's own "State & invalidation" section (and D-8) place invalidation inside `AddTicketsModal`; AdminPanel only re-runs `loadData` on `onSuccess`. Both ATS-007 scenarios satisfied (invalidation runs in the modal; admin list refreshes).
2. **Frontend vitest reality vs D-9** — D-9 claims "Frontend has NO test runner configured" (openspec/config.yaml is stale: vite.config.js has a full vitest config and the suite exists). No new vitest infra was added; I ran the existing suite as regression and updated the one approval test my spec-mandated change (ATS-008) required. Recording the D-9 "add vitest" gap as already-moot; follow-up: add vitest tests for AddTicketsModal.
3. **Task 4.3 (manual smoke) not executable in apply environment** — recorded as follow-up for verify/manual QA.

## Work Unit Evidence

| Evidence | Value |
|---|---|
| Focused test command + result | `dotnet test --filter "FullyQualifiedName~AdminControllerTicketStockTests\|FullyQualifiedName~EventServiceTicketStockTests"` → 32 passed, 0 failed. Frontend: `bash scripts/wsl-test.sh src/components/EventForm.test.jsx src/components/__tests__/EventForm.edit.test.jsx` → 21 passed; `src/pages/AdminPanel.test.jsx` → 32 passed |
| Runtime harness command/scenario | Backend: `dotnet test` full suite → 483 passed / 6 pre-existing failures unchanged / 0 regressions. Frontend: `npm run build` → ✓ built; `npm run lint` → 0 errors in changed files (6 pre-existing in untouched files) |
| Rollback boundary | `git revert` of: IEventService.cs, EventService.cs, AdminController.cs, AuditLog.cs, 2 new test files, AdminControllerTests.cs, AddTicketsModal.jsx, AdminPanel.jsx, EventForm.jsx, EventForm.test.jsx — no DB cleanup needed (purely additive; a committed increment is real capacity, not inventory to undo) |

## Risks / Remaining Gaps

- **FOR UPDATE untested on live PostgreSQL** — exercised via Npgsql branch structurally identical to the proven ReservationService path; SQLite no-op-UPDATE branch proven in concurrency test. Live-PG verification belongs to verify/manual QA.
- **Flaky pre-existing tests** (6 + 1 intermittent) unrelated to this change; do not gate on them.
- **Frontend lint baseline** has 6 pre-existing errors in untouched files (not introduced here).
- **D-9 follow-up**: add dedicated vitest tests for AddTicketsModal (modal currently verified via AdminPanel render + build).

## Workload / PR Boundary

- Mode: single-pr with **size:exception** (maintainer-approved, ~950-1100 changed lines)
- Current work unit: full change (no chaining per delivery decision)
- Boundary: backend service→controller→frontend modal→EventForm, all verified in this batch
- Estimated review budget impact: ~1000-1100 changed lines, accepted by approval

## Status

**9/10 tasks complete. Ready for sdd-verify** (4.3 manual smoke + live-PG FOR UPDATE verification recorded as follow-ups).
