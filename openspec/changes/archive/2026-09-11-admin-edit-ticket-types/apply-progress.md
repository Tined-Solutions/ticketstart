# Apply Progress: Admin Edit of Pre-Approval Ticket Types

**Change**: `admin-edit-ticket-types`
**Mode**: Strict TDD (backend `dotnet test`, frontend Vitest `npm test`)
**Delivery**: single PR, maintainer-approved `size:exception` (2,000-line review budget)
**Status**: all 6 phases complete — ready for `sdd-verify`
**Branch**: `feat/admin-edit-ticket-types` (base `origin/dev` `974bdfc`; planning commit `4c34dd5`)

## Commits

| Hash | Subject |
|------|---------|
| `3005728` | `feat(backend): reemplazo atomico de tipos de entrada (admin)` |
| `ddc46f6` | `feat(backend): endpoint PUT para editar tipos de entrada (admin)` |
| `a932ff4` | `feat(frontend): modal para editar tipos de entrada (admin)` |
| `8a1220b` | `feat(frontend): accion de editar entradas segun estado (admin)` |
| _(this commit)_ | `docs(openspec): apply-progress y tareas de admin-edit-ticket-types` |

## Completed Tasks

All tasks in `tasks.md` are marked `[x]`:

- Phase 1 — backend contracts (DTOs, `ReplaceTicketTypesAsync`, 409 exceptions, `AuditActionType.EditTicketTypes`).
- Phase 2 — `ReplaceTicketTypesAsync` with pinned guard order + SQLite tests + fault-injection rollback.
- Phase 3 — `PUT /api/admin/events/{id}/ticket-types` + audit + Moq tests + WAF 403/409 harness.
- Phase 4 — `EditTicketsModal` + Vitest.
- Phase 5 — `AdminPanel` status-aware action + `AddTicketsModal` three-key invalidation + tests.
- Phase 6 — full backend and frontend suites run.

## Files Changed (authored diff: 1,674 insertions / 41 deletions)

| File | Action | What Was Done |
|------|--------|---------------|
| `backend/Services/IEventService.cs` | Modified | `ReplaceTicketTypesAsync` contract + `ReplaceTicketTypesRequest`/`ReplaceTicketTypeRequest` DTOs. |
| `backend/Services/EventService.cs` | Modified | Atomic replacement transaction: tracked `Include`, guard order (`EnsureMutable` → eligibility → history → payload → ids), update/insert/delete, rollback, batch availability mapping. |
| `backend/Models/Exceptions.cs` | Modified | `TicketTypesNotEditableException`, `TicketTypesReferencedException`. |
| `backend/Models/AuditLog.cs` | Modified | `AuditActionType.EditTicketTypes` (varchar-backed, no migration). |
| `backend/Controllers/AdminController.cs` | Modified | `[HttpPut("events/{eventId:guid}/ticket-types")]` with RFC 7807 mapping (404/400/409×3/500) and one audit on success. |
| `backend/Tests/EventServiceTicketStockTests.cs` | Modified | 20 new ATE service tests + fault-injection context. |
| `backend/Tests/AdminControllerTicketStockTests.cs` | Modified | 10 new controller/WAF tests. |
| `frontend/src/components/EditTicketsModal.jsx` | Created | Full-edit dialog (`useManagementEvent`, `useDialog`, complete-list PUT, three-key invalidation, 409 copy). |
| `frontend/src/components/__tests__/EditTicketsModal.test.jsx` | Created | 8 tests. |
| `frontend/src/components/AddTicketsModal.jsx` | Modified | Three-key invalidation, including `managementEvent(id)` (ATS-007). |
| `frontend/src/components/__tests__/AddTicketsModal.test.jsx` | Created | 3 tests. |
| `frontend/src/pages/AdminPanel.jsx` | Modified | `editTicketsTarget`, status-aware label/icon (`Pending`/`Rejected` → "Editar entradas" + `TicketCheck`; `Approved` → "Agregar entradas" + `TicketPlus`), mount `EditTicketsModal`, organizer "Editar" untouched. |
| `frontend/src/pages/AdminPanel.test.jsx` | Modified | 4 status-aware action tests. |

No schema migration. No canonical `openspec/specs/` edits (archive-time work).

## TDD Cycle Evidence (Strict TDD — all units)

| Task | RED (test written first) | GREEN | REFACTOR | Evidence |
|------|--------------------------|-------|----------|----------|
| 2.1/2.2 | 20 `EventServiceTicketStockTests` failed (stub `NotImplementedException`) | `ReplaceTicketTypesAsync` implemented | history/validation extracted into `ValidateReplacementPayload` + `MapTicketTypesWithAvailabilityAsync` | `dotnet test --filter FullyQualifiedName~EventServiceTicketStockTests` → **39 passed / 0 failed** |
| 3.1 | 9 `AdminControllerTicketStockTests` failed (stub action) | PUT action implemented | extracted `Problem` mapping per exception; `instance` added | `dotnet test --filter FullyQualifiedName~AdminControllerTicketStockTests` → **25 passed / 0 failed** |
| 3.3 | WAF 403 test (auth gate) + WAF 409 problem+json test | passes without production change for the 403 path | — | included in the 25 above |
| 4.1/4.2 | `EditTicketsModal.test.jsx` failed (module missing) | component created | — | `npm test -- EditTicketsModal` → **8 passed / 0 failed** |
| 5.1/5.2 | 2 `AddTicketsModal` tests failed (missing `managementEvent` key) | invalidation updated | — | `npm test -- AddTicketsModal` → **3 passed / 0 failed** |
| 5.3/5.4 | 3 `AdminPanel` tests failed (no status-aware action) | `AdminPanel` updated | — | `npm test -- AdminPanel AddTicketsModal` → **63 passed / 0 failed** |

## Work Unit Evidence

| Unit | Focused test command + exact result | Runtime harness + exact result | Rollback boundary |
|------|--------------------------------------|--------------------------------|-------------------|
| 1 — Domain + service | `cd backend && dotnet test --filter FullyQualifiedName~EventServiceTicketStockTests` → 39 passed / 0 failed | SQLite in-memory transaction; the `FaultingReplaceTicketTypesDbContext` throws after `SaveChangesAsync` and a fresh read proves the original list survives (ATE-001 atomicity). N/A beyond that — no browser/HTTP boundary in this unit. | `IEventService.cs`, `EventService.cs`, `Exceptions.cs`, `AuditLog.cs` + `EventServiceTicketStockTests.cs` |
| 2 — Controller + audit | `cd backend && dotnet test --filter FullyQualifiedName~AdminControllerTicketStockTests` → 25 passed / 0 failed | `WebApplicationFactory<Program>` via `EventCatalogApiFactory`: organizer → 403 on PUT (ATS-001); Approved event → 409 `ticket-types-not-editable` as `application/problem+json` with `instance` (ATE-002/007). | `AdminController.cs` + `AdminControllerTicketStockTests.cs` |
| 3 — `EditTicketsModal` | `cd frontend && npm test -- EditTicketsModal` → 8 passed / 0 failed | N/A — jsdom is the repo convention; no browser harness for components. | `EditTicketsModal.jsx` + its test |
| 4 — `AdminPanel` + `AddTicketsModal` | `cd frontend && npm test -- AdminPanel AddTicketsModal` → 63 passed / 0 failed | N/A — jsdom is the repo convention; no browser harness for components. | `AdminPanel.jsx`, `AddTicketsModal.jsx` + their tests |

## Full-Suite Results

| Suite | Result | Notes |
|-------|--------|-------|
| `cd backend && dotnet test` | **782 passed / 4 failed / 786 total** | 4 failures are **pre-existing baseline failures**, unrelated to this change (see below). |
| `cd frontend && npm test` | **530 passed / 0 failed (50 files)** | Fully green. |

### Baseline failures (pre-existing — do NOT treat as regressions)

All four were reproduced at the planning commit `4c34dd5` in a throwaway `git worktree` (with an equivalent `appsettings.Development.json` present so live-DB-gated tests actually execute):

1. `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` — expects `UnauthorizedObjectResult`, receives `OkObjectResult`.
2. `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` — same webhook-signature behavior.
3. `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` — expected `1`, actual `0`.
4. `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` — `POST /webhook` returns `BadRequest` instead of a non-400.

None of these paths (payment signature validation, pending-email retry, CSRF middleware) is touched by this change. Note: `VerifyDatabaseSchema` did **not** fail in this run; it is guarded/skipped without a live Supabase connection.

## Deviations from Design

- **Explicit RFC 7807 `instance`.** The design requires "RFC 7807 409 with fields and request-path instance". `ControllerBase.Problem(...)` does not auto-populate `Instance`, so the new action passes `instance: HttpContext.Request.Path` explicitly on all three 409 branches. Existing endpoints keep their prior (instance-less) behavior — out of scope. Verified over HTTP: the WAF test asserts `instance` is present.
- **`MapTicketTypesWithAvailabilityAsync` batch helper.** The design said "map availability from a sold/reserved aggregate query"; implemented as a single batched helper (two aggregate queries, no N+1) rather than per-row `MapTicketTypeWithAvailabilityAsync`.
- **`TicketTypesReferencedException` message.** Its `Message` already states that add-only remains available, so no extra controller copy is needed; `EditTicketsModal` additionally special-cases the `ticket-types-referenced` 409 type for a Spanish, user-facing explanation (ATE-010).

## Issues Found

- The raw `ObjectResult.ContentTypes` returned by `Problem()` is empty in a direct (non-executed) unit call; the `application/problem+json` media type is asserted over HTTP in the WAF test instead. No production impact.
- Pre-existing XML-doc warning `CS1570` at `IEventService.cs` line 96 (`additionalQuantity <= 0 or > ...`) is untouched (out of scope).

## Remaining Tasks

- None. All `tasks.md` items are `[x]`.

## Next Recommended

`sdd-verify` — independent verification against the ATE/ATS/PEM spec deltas.
