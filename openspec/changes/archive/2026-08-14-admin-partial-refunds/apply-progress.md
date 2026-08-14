# Apply Progress: Admin partial refunds (per-quantity, cumulative)

> Change `admin-partial-refunds`. Hybrid artifact (this file + Engram `sdd/admin-partial-refunds/apply-progress`).
> Delivery: **single-pr** + maintainer-approved **size:exception (budget 4000 lines)** — NOT chained.
> Strict TDD active (RED→GREEN per task). Migrations are MANUAL: 5.2 is a PENDING-OWNER rollout step, not executed here.

## Summary

All 14 tasks implemented. Backend: entity + DbSet + migration with pure-SQL backfill, 3-arg
quantity-based refund service with Refunds ledger + flip-at-zero (D2), controller body DTO with
quantity audit. Frontend: badge "X de Y reembolsadas" + quantity selector with live preview.
Full suites green except documented pre-existing failures (5 backend / 3 frontend, all in files
untouched by this change).

## Task-by-task status

| # | Task | Status | Verification |
|---|------|--------|--------------|
| 1.1 | RED structural backfill test (`AddRefundsTable_BackfillContainsPureSqlInsertSelect`) | ✅ done | RED: failed (no migration); GREEN after 1.4: 2/2 passed |
| 1.2 | `Models/Refund.cs` (NEW) — D5/D6/D7 | ✅ done | build OK |
| 1.3 | `ApplicationDbContext.cs` — `DbSet<Refund>` + OnModelCreating block | ✅ done | build OK |
| 1.4 | Migration `20260814134333_AddRefunds` (+ mandatory Designer) + pure-SQL backfill | ✅ done | 1.1 passes; `ef migrations list` shows 16/16 with AddRefunds (Pending) |
| 2.1 | RED rewrite `AdminPurchaseServiceTests.cs` (13 refund/listing tests, 3-arg calls) | ✅ done | RED: compile-fail; GREEN after 2.3: 19/19 passed |
| 2.2 | `IAdminPurchaseService.cs` — 11-arg `AdminPurchaseRow`, 3-arg `RefundPurchaseAsync` | ✅ done | build OK |
| 2.3 | `AdminPurchaseService.cs` — group queries, quantity guard, oldest-K, Refund row, flip-at-zero | ✅ done | 2.1 tests pass |
| 3.1 | RED `AdminControllerPurchaseTests.cs` — 11-arg row, 3-arg mocks, body DTO, new 409 test | ✅ done | RED: compile-fail (`RefundPurchaseRequest` missing); GREEN after 3.2: 13/13 passed |
| 3.2 | `AdminController.cs` — `RefundPurchaseRequest(int Quantity)` + `[FromBody]` + 3-arg + quantity audit | ✅ done | 3.1 tests pass |
| 4.1 | RED `AdminPurchases.test.jsx` — refundedQuantity/refundedAmount shape, badge, selector, invalidation | ✅ done | RED: 3 failed; GREEN after 4.2: 7/7 passed |
| 4.2 | `AdminPurchases.jsx` — `refundBadge`, disabled rule, quantity selector + preview, `{quantity}` mutation | ✅ done | 4.1 tests pass |
| 5.1 | Full suites | ✅ done | backend 640 pass / 5 pre-existing fail; frontend 438 pass / 3 pre-existing fail |
| 5.2 | Apply migration (manual PG) | ⏸️ **PENDING-OWNER** | NOT executed in apply (rollout step). Command: `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext` |
| 5.3 | Rollback doc | ✅ done | recorded below (rollback notes) |

## TDD Cycle Evidence

| Task | RED (test first) | GREEN (impl) | REFACTOR | Result |
|------|------------------|--------------|----------|--------|
| 1.1→1.4 | `AddRefundsTable_BackfillContainsPureSqlInsertSelect` failed (no migration) | Refund.cs + DbContext + migration with pure-SQL backfill | path walk-up fix in test helper | ✅ 2/2 |
| 2.1→2.3 | rewritten service tests compile-fail (3-arg + 11-arg missing) | interface + service | extracted `AcquireTicketLocksAsync`; fixed test seeding to tracked entities | ✅ 19/19 |
| 3.1→3.2 | controller tests compile-fail (`RefundPurchaseRequest` missing) | record + `[FromBody]` + audit | — | ✅ 13/13 |
| 4.1→4.2 | 3 tests failed (old binary shape) | refundBadge + selector + `{quantity}` mutation | — | ✅ 7/7 |

## Test results

- **Backend** (`dotnet test` from `backend/`): **640 passed / 5 failed** (645 total).
  All 5 failures pre-existing and unrelated to this change (files untouched):
  - `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` (AuthCookieTests.cs)
  - `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client`
  - `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`
  - `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`
  - `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`
- **Frontend** (`npx vitest run` from `frontend/`): **438 passed / 3 failed** (441 total).
  All 3 pre-existing and unrelated (files untouched): 2× `Checkout.test.jsx` (DNI
  formatting / PATCH edit), 1× `identityValidation.test.js` (DNI letters validation).
- AdminPurchases-related suites: `AdminPurchaseServiceTests` 19/19, `AdminControllerPurchaseTests` 13/13, `AddRefundsTable_BackfillContainsPureSqlInsertSelect` 2/2, `AdminPurchases.test.jsx` 7/7.

## Commits

| Hash | Message |
|------|---------|
| `98a3332` | `feat(backend): reembolso parcial admin — tabla Refunds con backfill por SQL puro` |
| `bcbfc58` | `feat(backend): reembolso parcial por cantidad con ledger Refunds y body {quantity}` |
| `9163d3d` | `feat(frontend): reembolso parcial admin — selector de cantidad y badge X de Y reembolsadas` |

(No pushes — owner decides delivery.)

## Rollback notes (5.3, for PR description)

- **Pre-deploy**: `dotnet ef migrations remove` (removes `20260814134333_AddRefunds`; history back to 15/15).
- **Post-deploy**: drop the `Refunds` table (`DROP TABLE "Refunds";`) — additive schema, no FK consumer
  blocks revert; backfill rows lost but restorable by re-running the migration's INSERT…SELECT.
- **Code**: revert service/DTO/row/UI to binary refund (`RefundPurchaseAsync(reservationId, adminId)`,
  9-arg `AdminPurchaseRow`, no `RefundPurchaseRequest`, binary badge/button) — revert commits
  `9163d3d` and `bcbfc58`; keep `98a3332`'s entity only if the table is kept.
- **Data**: `IsRefunded` flags on Tickets + audit rows are KEPT (no data loss); refunds history on
  the ledger is dropped only by the explicit table drop above.

## Deviations from design

None — implementation matches design.md (D1–D10), the sequence diagrams, migration SQL and test map.

## Issues found

- Test helper path: xunit runs from `bin/Debug/net9.0`, so the structural test walks up to find
  `Migrations/`; designer file glob needs `*_AddRefunds*.cs` (not `*_AddRefunds.cs`, which misses
  the `.Designer.cs` suffix). Resolved in-test, no production impact.
- Migration Up() comment must not contain the literal "try"/"catch" — the strict structural test
  asserts their absence; reworded the comment accordingly.
