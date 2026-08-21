# Apply Progress — past-events-readonly

**Change**: past-events-readonly — Past Events Read-Only (Event Immutability)
**Phase**: apply (sdd-apply, hybrid artifact store)
**Date**: 2026-08-21
**Branch**: `fix/admin-past-events-edit` (single-pr; 4 work-unit commits)
**Mode**: Strict TDD (backend) + manual verification (frontend, no test runner)

## Status

All 17 tasks complete (1.1–5.4). Ready for sdd-verify.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `backend/Tests/EventFinalizedGuardTests.cs` | Unit | N/A (new) | ✅ Written | ✅ 3/3 | ✅ 3 cases (expired/active/exact) | ➖ None needed |
| 1.2 | (same file — exception) | Unit | N/A (new) | ✅ (compile RED) | ✅ | ➖ Single (message assertion) | ➖ None needed |
| 1.3 | `EventFinalizedGuardTests.cs` | Unit | N/A (new) | ✅ Written | ✅ | ✅ 3 cases | ➖ None needed |
| 2.1 | `backend/Tests/EventServiceImmutabilityTests.cs` | Unit (InMemory) | ✅ 155/155 baseline | ✅ 6 fail / 1 pass | ✅ 7/7 | ✅ 5 methods + flag-disabled + future | ➖ None needed |
| 2.2 | `backend/Tests/AdminServiceTests.cs` | Unit (InMemory) | N/A (new) | ✅ compile RED (ctor) | ✅ 4/4 | ✅ Approve+Reject × past/future | ➖ None needed |
| 2.3 | (EventService guard calls) | Unit | ✅ 155/155 | ✅ (2.1 RED) | ✅ 102/102 | ✅ via 2.1 cases | ➖ None needed |
| 2.4 | (AdminService TimeProvider + guards) | Unit | ✅ 155/155 | ✅ (2.2 RED) | ✅ 102/102 | ✅ via 2.2 + AdminPropertyTests/Batch7 updated | ➖ None needed |
| 3.1 | `backend/Tests/EventControllerTests.cs` | Integration (WAF) | ✅ 155/155 | ✅ 7 fail (500→409) | ✅ 76/76 | ✅ 7 endpoints + GET-manage-200 | ✅ WAF InMemory txn-warning ignore |
| 3.2 | `backend/Tests/AdminControllerTests.cs` | Unit (mock) | ✅ 155/155 | ✅ 4 fail | ✅ 29/29 | ✅ Approve/Reject/Stock/Type + audit Never | ➖ None needed |
| 3.3 | `backend/Tests/ErrorHandlingPropertyTests.cs` | Unit (handler) | ✅ 155/155 | ✅ 1 fail | ✅ (in 76) | ➖ Single (mirrors :206) | ➖ None needed |
| 3.4/3.5/3.6 | controllers + handler | Unit+Integration | ✅ 155/155 | ✅ (3.1–3.3 RED) | ✅ 76/76 | ✅ via 3.1–3.3 | ➖ None needed |
| 4.1–4.5 | frontend (no runner) | Manual | ✅ 445/448 baseline | N/A | N/A | N/A | ✅ fixture dates bumped (2 test files) |

## Work Unit Evidence

| Unit | Focused test command + result | Runtime harness | Rollback boundary |
|------|------------------------------|-----------------|-------------------|
| 1 — guard foundation | `dotnet test --filter EventFinalizedGuardTests` → Passed 3/3 | N/A — pure logic proven by unit tests | Revert: `EventFinalizedException.cs`, `EventFinalizedGuard.cs`, `EventFinalizedGuardTests.cs` (commit cb8c5cd) |
| 2 — service guards | `dotnet test --filter EventServiceImmutabilityTests\|AdminServiceTests` → Passed 11/11; + EventServiceTests/AdminPropertyTests/Batch7 suite → 102/102 | N/A — service-layer InMemory unit tests | Revert: guard calls in EventService/AdminService + TimeProvider ctor + 2 new test files + 2 updated test files (commit bfb119f) |
| 3 — controllers + handler | `dotnet test --filter EventControllerTests\|AdminControllerTests\|ErrorHandlingPropertyTests` → Passed 76/76 | WAF integration: 7 endpoints → 409 `type:"event-finalized"`, GET manage → 200 (EventCatalogApiFactory, frozen clock, real cookies, CSRF header) | Revert: 7 controller catches + GlobalExceptionHandler case + 3 test files (commit d027432) |
| 4 — frontend | `npm run build` ✅; `npx eslint <my 5 files>` ✅ exit 0; `npm test` (affected files) 81/81; full suite 445/448 (3 pre-existing) | N/A — manual smoke deferred (no frontend test runner; design follow-up) | Revert: EventForm readOnly + EventReadOnlyView + App route + AdminPanel/OrganizerDashboard edits + 2 test fixture files (commit 9b6ae7b) |

## Implementation Notes / Deviations from Design

- **None — implementation matches design.md.** All insertion points, exception/guard shapes, controller catches, handler fallback, and frontend patterns follow the design exactly.
- WAF test-infra addition (not a deviation): `EventCatalogApiFactory` gained `SeedAdmin()`, `SeedTicketType(...)`, and `.ConfigureWarnings(...Ignore(InMemoryEventId.TransactionIgnoredWarning))` on its InMemory registration — required because the stock/ticket-type endpoints open a FOR UPDATE transaction that the InMemory provider no-ops with a promoted warning (established convention in AdminPropertyTests).
- `AddTicketStockAsync` loads the Event entity inside the FOR UPDATE transaction (identity-map hit) before calling the guard — the design's guard-table row implies the event must be materialized for `EnsureMutable(Event, ...)`; this is the ADR-7 in-transaction check.
- Fixture adjustment (not new tests): `AdminPanel.test.jsx` / `OrganizerDashboard.test.jsx` mock event-1's date bumped 2026-08-15 → 2026-11-15 so the existing mutation-action tests keep exercising enabled buttons (event-1 was past-dated relative to today and its Editar/Eliminar are now correctly disabled). Required by "keep existing tests/behaviors intact".

## Pre-existing Failures (NOT caused by this change — proven on baseline commit 599642b)

Backend `dotnet test` (full): 679 passed / 5 failed — all 5 reproduced on the pre-change baseline:
1. `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`
2. `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client`
3. `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`
4. `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`
5. `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` (live-DB environment-driven: `HasLiveDatabase()` true when `appsettings.Development.json` has a real connection string; fails identically on baseline with dev settings present)
   - Note: `QRCodePropertyTests.Property21` is flaky (fails in full run, passes in isolation and on baseline).

Frontend `npm test` (full): 445 passed / 3 failed — all pre-existing (reproduced on baseline):
1. `Checkout.test.jsx` × 2 (reservation edit flows)
2. `identityValidation.test.js` × 1 (DNI letters)

`dotnet format --verify-no-changes`: 457 WHITESPACE errors — identical on baseline; **0 errors in any file touched by this change**.

Frontend lint: `vite.config.js` has 2 pre-existing `no-undef` errors; **0 errors in the 5 files touched by this change**.

## Deliverables

- 4 work-unit commits (conventional, no AI attribution):
  - `cb8c5cd` feat(events): add EventFinalizedGuard + exception
  - `bfb119f` feat(events): guard service mutations on past events
  - `d027432` feat(events): map past-event mutations to 409
  - `9b6ae7b` feat(web): read-only Ver view for past events

## Next Recommended

sdd-verify (full `dotnet test` + npm build/lint gates; note the 5 pre-existing backend failures and 3 pre-existing frontend failures above so they are not attributed to this change).