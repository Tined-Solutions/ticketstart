```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:a579f7f303e7bb3434995c967e494c2ec96bdc4f2f7bdf8e19ec7da0d26b0ee8
verdict: fail
blockers: 1
critical_findings: 0
requirements: 8/8
scenarios: 18/18
test_command: dotnet test
test_exit_code: 1
test_output_hash: sha256:d4c1da8740de14ec1fa7a90a0dc0fe947217bca5fddfcad9a799a460a504e0a0
build_command: dotnet build
build_exit_code: 0
build_output_hash: sha256:eb20d6dc3353ff554c431c47b8d4bb57adabd943c03d690ed9219e40c1eaccc0
```

# Verification Report: admin-partial-refunds

**Change**: admin-partial-refunds
**Version**: Delta spec `openspec/changes/admin-partial-refunds/specs/admin-purchase-refunds/spec.md` (modifies canonical `openspec/specs/admin-purchase-refunds/spec.md`)
**Mode**: Strict TDD (backend xUnit / frontend Vitest — `openspec/config.yaml` `testing.strict_tdd: true`)
**Authoritative spec counts (read from disk)**: 8 requirements (APR-002/003/010/011 modified; APR-012/013/014/015 added) · 18 scenarios

## Verdict Rationale (read this first)

**Machine verdict: `fail`** — the strict-TDD gate requires `fail` whenever the declared test command exits non-zero. `dotnet test` exits 1 because the repository carries **pre-existing baseline failures** (5 backend / 3 frontend). This is a *command-exit blocker*, NOT a defect of this change:

- **ZERO new failures** in either suite. Every failure in every run is a documented pre-existing baseline in an **unmodified file** (backend: CSRF webhook, S3 upload, MP webhook signature ×2, email retry — `AuthCookieTests.cs`, `EventImageUploadTests.cs`, `PaymentControllerTests.cs`, `PaymentPropertyTests.cs`, `PendingEmailRetryTests.cs`; frontend: `Checkout.test.jsx` ×2 DNI/PATCH, `identityValidation.test.js` DNI letters).
- **All 39 change-scope tests pass at runtime**: backend focused 32/32 (AdminPurchaseServiceTests 19, AdminControllerPurchaseTests 11, AddRefundsTable_BackfillContainsPureSqlInsertSelect 2), frontend AdminPurchases.test.jsx 7/7.
- **All 8 requirements / 18 scenarios are COMPLIANT** with green covering tests (matrix below).
- `critical_findings: 0` — no spec requirement unmet, no regression introduced by this change.

The single blocker (`blockers: 1`) is the non-zero full-suite exit, caused exclusively by pre-existing baseline tests. Per the gate this report is **persistable but not archive-ready**; the archive decision is the orchestrator's, informed by the evidence below. The change's own content requires **no remediation**.

Rollout note: task 5.2 (`dotnet ef database update` against the shared Supabase dev DB) is **PENDING-OWNER** — a rollout step explicitly NOT executed in apply and NOT run during this verification (per orchestrator instruction). The backfill is validated structurally (`AddRefundsTable_BackfillContainsPureSqlInsertSelect`, pure-SQL `INSERT…SELECT` asserted) plus the read-side regression test (`GetPurchasesAsync_LegacyRefundWithBackfilledRow_KeepsCountingTotalRefunded`). Live-DB backfill confirmation remains an owner rollout step.

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 14 |
| Tasks complete | 13 |
| Tasks incomplete | 1 — **5.2 Apply migration (manual PG), PENDING-OWNER rollout** (structural + read-side verified; live apply intentionally not run) |

## Build & Tests Execution

**Build**: ✅ Passed — `dotnet build` (backend/) → 0 errors, exit 0. Output hash `sha256:eb20d6dc3353ff554c431c47b8d4bb57adabd943c03d690ed9219e40c1eaccc0`.

**Tests**:
- Backend `dotnet test` (from `backend/`): **640 passed / 5 failed / 645 total**, exit 1. All 5 failures are the documented pre-existing baselines (identical set to apply-progress §Test results), all in files untouched by this change. **ZERO new.** Output hash `sha256:d4c1da8740de14ec1fa7a90a0dc0fe947217bca5fddfcad9a799a460a504e0a0`.
  - `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` (AuthCookieTests.cs)
  - `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client` (EventImageUploadTests.cs)
  - `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` (PaymentControllerTests.cs)
  - `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` (PaymentPropertyTests.cs)
  - `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` (PendingEmailRetryTests.cs)
- Backend focused (change suites): `--filter "…AdminPurchaseServiceTests|AdminControllerPurchaseTests|AddRefundsTable"` → **32/32 passed** (AdminPurchaseServiceTests 19, AdminControllerPurchaseTests 11, AddRefundsTable_BackfillContainsPureSqlInsertSelect 2).
- Frontend `npx vitest run` (from `frontend/`): **438 passed / 3 failed / 441 total** (43 files), exit 1. All 3 failures are the documented pre-existing baselines in untouched files: `Checkout.test.jsx > returns to the reservation form when clicking Editar datos, preserving input data`, `Checkout.test.jsx > sends a PATCH request when saving edits on an existing reservation`, `identityValidation.test.js > rejects DNI with letters`. **ZERO new.** Output hash `sha256:7c919af000a3b6f98b5ed759120177086037a73c129ae057436756e7d8a5b20c`.
- Frontend focused: `npx vitest run src/pages/AdminPurchases.test.jsx` → **7/7 passed**.

**Coverage**: ➖ Not available (no coverage tool configured; informational per strict-TDD rules).

## Spec Compliance Matrix (APR-002/003/010/011 modified, APR-012/013/014/015 added)

| Requirement | Scenario | Covering test (all PASSED at runtime) | Result |
|-------------|----------|---------------------------------------|--------|
| APR-012 Cumulative refund operation record | Partial refund records one operation row | `RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved` (exactly 2 of 4 `IsRefunded`, tx stays Approved → row shows RefundedQuantity=2); `RefundPurchaseAsync_InsertsRefundRow_WithTicketIdsQuantityUnitPriceAmountAdminId` (single Refunds row: ReservationId, TicketIds = selected, Quantity=2, Amount=200 = 100×2, AdminId, CreatedAt) | ✅ COMPLIANT |
| APR-012 | Cumulative second refund appends | `RefundPurchaseAsync_Cumulative_SecondRefundAppendsAndFlipsAtZero` (2 Refunds rows, Σ=400); `GetPurchasesAsync_TotalRefunded_SumOfRefundsAmount` (TotalRefunded = Σ Refunds.Amount = 200, excludes approved) | ✅ COMPLIANT |
| APR-013 Deterministic ticket selection | Oldest tickets refunded first | `RefundPurchaseAsync_SelectsOldestTickets_ByCreatedAt` (distinct CreatedAt → exactly the 2 earliest marked `IsRefunded`); selection `OrderBy(CreatedAt).Take(K)` under row lock `AdminPurchaseService.cs:183-187` + `AcquireTicketLocksAsync` (`:235-259` Npgsql FOR UPDATE / SQLite no-op UPDATE / InMemory) | ✅ COMPLIANT |
| APR-014 Legacy refund backfill | Legacy refund keeps counting | `AddRefundsTable_BackfillContainsPureSqlInsertSelect.Migration_And_Designer_Files_Exist` + `.Up_Contains_Pure_Sql_InsertSelect_Backfill` (migration + Designer exist; `migrationBuilder.Sql` `INSERT…SELECT` `array_agg` Status=3, no EF-context, no try/catch — `20260814134333_AddRefunds.cs:49-65`); `GetPurchasesAsync_LegacyRefundWithBackfilledRow_KeepsCountingTotalRefunded` (seeds `Refund{AdminId=null}` + Refunded tx → TotalRefunded=100, no regression to 0) | ✅ COMPLIANT |
| APR-015 Non-goals as negative requirements | Partial refund stays local and irreversible | `RefundPurchase_Success_DoesNotTouchPaymentServiceOrEmail` (controller depends only on `IAdminPurchaseService` + `IAuditLogService`; no MP/email dep); `RefundPurchase_Success_PassesQuantityBodyAndWritesAuditWithoutMotivo` (audit `Details` asserts NO "motivo"/"reason"); `git log` → `PaymentService.cs`/`IMercadoPagoClient`/`MetricsService`/`EventService`/`ReservationService`/`TicketService` 0 commits in apply range; no refund-edit/revert endpoint exists; Reservation status never written by `RefundPurchaseAsync` | ✅ COMPLIANT |
| APR-002 List event purchases | Happy path listing | `GetPurchasesAsync_PartialAndFullRefunded_ReturnsRefundedQuantityRefundedAmountAndDerivedFlag` (raw buyer email/DNI, RefundedQuantity 1, RefundedAmount 100, derived flag: partial false / full true, TotalRefunded=200); `GetPurchases_HappyPath_ReturnsListingWithTotalRefunded` (11-arg row, 200m) | ✅ COMPLIANT |
| APR-002 | Event not found | `GetPurchasesAsync_EventNotFound_ThrowsKeyNotFound`; `GetPurchases_MissingEvent_ReturnsNotFound` (404) | ✅ COMPLIANT |
| APR-003 Atomic quantity-based refund | Partial refund happy path | `RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved` (4 unused tickets, K=2 → exactly 2 refunded, 1 Refunds row, tx stays Approved); controller 200 via `RefundPurchase_Success_*` | ✅ COMPLIANT |
| APR-003 | Full refund flips transaction only at zero active | `RefundPurchaseAsync_FullAtZeroActive_FlipsTransaction` (all tickets refunded, `Assert.Single` transaction row for MercadoPagoId, Status=Refunded, UpdatedAt set); flip at `AdminPurchaseService.cs:209-213` (active == quantity) | ✅ COMPLIANT |
| APR-003 | No approved transaction | `RefundPurchaseAsync_NoApprovedTransaction_ThrowsNoChange` (Pending tx → `InvalidOperationException`, no ticket/Refunds change); → 409 (`AdminController.cs:293-296`) | ✅ COMPLIANT |
| APR-003 | Quantity above active remaining is blocked | `RefundPurchaseAsync_QuantityAboveActiveRemaining_ThrowsNoChange` (K=3 > 2 active → throw; no ticket/Refunds/tx change); → 409 path | ✅ COMPLIANT |
| APR-003 | Quantity zero or negative is blocked | `RefundPurchaseAsync_QuantityZeroOrNegative_ThrowsNoChange` `[Theory]` (K=0 and K=-1 → throw, no state change); `RefundPurchase_InvalidQuantity_ReturnsConflict` (409, no audit) | ✅ COMPLIANT |
| APR-003 | Concurrent partial refunds serialize | `RefundPurchaseAsync_ConcurrentQuantityGuard_SecondSeesFirstCommittedState` (sequential InMemory: second op observes first's committed state — 4 tickets refunded once each, 2 Refunds rows, Σ Quantity 4, no double refund). FOR UPDATE arm is Npgsql-only, covered by the ReservationService/EventService trio precedent (not integration-tested) | ✅ COMPLIANT |
| APR-010 Admin UI | Non-admin blocked from route | `AdminPurchases.test.jsx` "denies access to non-admin users on the purchases route" (403) + "allows access for admin users"; route `App.jsx:104-107` `ProtectedRoute` + `RoleGuard allowedRoles={['Admin']}`; AdminPanel "Compras" action `AdminPanel.jsx:421-425` → `/admin/events/:id/purchases` (pre-existing, test `AdminPanel.test.jsx:214`); backend class-level `RequireAdminRole` `AdminControllerPurchaseTests.AdminController_HasClassLevelRequireAdminRolePolicy` | ✅ COMPLIANT |
| APR-010 | Partial refund via quantity selector | `AdminPurchases.test.jsx` "partial refund via quantity selector posts {quantity} and updates the row" (number input min=1 max=activeRemaining=2, live preview "2 × $100 = $200", POST `/…/res-1/refund` body `{ quantity: 2 }`, invalidation refetch → "2 de 2 reembolsadas" + totalRefunded $350); selector `AdminPurchases.jsx:60-73`, mutation body `:115` | ✅ COMPLIANT |
| APR-010 | Refund failure shows error | `AdminPurchases.test.jsx` "refund failure shows the error and leaves the list unchanged" (`role=alert` with used-ticket error, `mockGet` called once, badges unchanged); `onError` `AdminPurchases.jsx:130` without state mutation | ✅ COMPLIANT |
| APR-010 | Fully refunded row disables refund | `AdminPurchases.test.jsx` "renders purchases with raw buyer data and refunded badge variants" (res-2 1/1 → button disabled, badge "1 de 1 reembolsadas" error variant; res-1 0/2 → "Confirmada" success) + post-refund "2 de 2 reembolsadas" row disabled; `disabled={refundedQuantity >= quantity}` `AdminPurchases.jsx:214`, `refundBadge` `:24-30` | ✅ COMPLIANT |
| APR-011 Test coverage | Suite stays green | Full suites: backend 640 pass / 5 pre-existing fail; frontend 438 pass / 3 pre-existing fail; focused change suites 32/32 (backend) + 7/7 (frontend); ripple consumers (D10) unaffected — all failing tests in untouched files | ✅ COMPLIANT |

**Compliance summary**: 18/18 scenarios compliant. **No CRITICAL, no UNTESTED, no FAILING** for this change's content.

## Correctness — Critical Invariants (static evidence)

| Invariant | Status | Evidence |
|-----------|--------|----------|
| Exactly one `Refunds` row per operation (APR-012) | ✅ | `AdminPurchaseService.cs:197-205` (Add inside the same transaction as the ticket marks); tests `_InsertsRefundRow…` + `_Cumulative…` (2 rows) |
| `Amount` = unit price × K from `TicketType.Price` (D7) | ✅ | `AdminPurchaseService.cs:196` `reservation.TicketType.Price * quantity`; test asserts 200m for Price=100 × K=2 |
| `TicketIds` snapshot = selected ticket ids (D5) | ✅ | `AdminPurchaseService.cs:200` `selected.Select(t => t.Id).ToArray()`; test asserts equality with the selected 2 |
| K oldest non-refunded/non-used selection (APR-013) | ✅ | `AdminPurchaseService.cs:183-187` `Where(!IsRefunded && !IsUsed).OrderBy(CreatedAt).Take(quantity)`; oldest-2 test green |
| Flip Approved→Refunded ONLY at 0 active; never a second tx row (D2) | ✅ | `AdminPurchaseService.cs:209-213` `if (active == quantity)` mutates the existing row; `_FullAtZeroActive…` asserts `Assert.Single` |
| Quantity guard under lock: K>0 && K≤active (APR-003) | ✅ | `AdminPurchaseService.cs:164-169` on the row-locked list; tests K=3, K=0, K=-1 throw with no change |
| IsUsed blocks whole refund + re-check under lock (APR-004) | ✅ | `AdminPurchaseService.cs:155-159` after `AcquireTicketLocksAsync`; `_UsedTicket_ThrowsNoChange` |
| Legacy backfill pure SQL, AdminId NULL, Status=3 (APR-014) | ✅ | `20260814134333_AddRefunds.cs:49-65` `INSERT…SELECT … array_agg … WHERE "Status" = 3` with `NULL` AdminId, no EF-context/no try-catch; structural test asserts absence |
| `TotalRefunded` = Σ `Refunds.Amount` (read-side, incl. legacy rows) | ✅ | `AdminPurchaseService.cs:117` `refundsByRes.Values.Sum()`; tests `_TotalRefunded…`, `_LegacyRefundWithBackfilledRow…`, `_PartialAndFullRefunded…` (200m) |
| `Refunded` derived = `RefundedQuantity >= Quantity` | ✅ | `AdminPurchaseService.cs:112`; tests partial false / full true |
| No N+1 in listing group queries | ✅ | `refundedTicketCounts` (`:77-84`) + `refundsByRes` (`:87-94`) grouped `AsNoTracking`, parallel to `linkedTicketCounts` |
| Controller body validation + audit without motivo | ✅ | `RefundPurchaseRequest(int Quantity)` plain record `AdminController.cs:408` (D8), `[FromBody]` `:271`, audit after commit `:279-285` with quantity, no motivo; tests assert 3-arg call + audit predicate + 409-without-audit |
| No Reservation status change / no refund-editing path (APR-015) | ✅ | `RefundPurchaseAsync` never assigns `reservation.Status`; no revert/edit endpoint exists; `PaymentService.InitiateRefundAsync` untouched (git, 0 commits) |

## Coherence (Design D1–D10)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| D1 New immutable `Refunds` ledger table (not read-time derivation) | ✅ Yes | `Refund.cs` (no `UpdatedAt`), `DbSet<Refund>` `ApplicationDbContext.cs:28`, OnModelCreating block `:241-255` |
| D2 Flip Approved→Refunded only at 0 active; partial keeps Approved | ✅ Yes | `AdminPurchaseService.cs:209-213`; tests `_Partial…` (stays Approved), `_FullAtZeroActive…` (flips, single row) |
| D3 K oldest `OrderBy(CreatedAt).Take(K)` under row lock | ✅ Yes | `AdminPurchaseService.cs:183-187`; oldest-selection test |
| D4 Pure-SQL `INSERT…SELECT` `array_agg` backfill (no EF-context, no try/catch) | ✅ Yes | `20260814134333_AddRefunds.cs:49-65`; structural test + design-comment rewrite noted in apply-progress |
| D5 `TicketIds` as `Guid[]` + `uuid[]` (PG-only; InMemory native) | ✅ Yes | `Refund.cs:22-23` `[Column(TypeName="uuid[]")]`; `ApplicationDbContext.cs:245` `HasColumnType("uuid[]").IsRequired()` |
| D6 `Restrict` delete behavior Refund→Reservation | ✅ Yes | `ApplicationDbContext.cs:251-254`; migration FK `ReferentialAction.Restrict` |
| D7 `Amount = TicketType.Price × K` | ✅ Yes | `AdminPurchaseService.cs:196`; 200m test assertion |
| D8 Plain record, no data annotations; service throws → 409 | ✅ Yes | `AdminController.cs:408`; `RefundPurchase_InvalidQuantity_ReturnsConflict` |
| D9 Quantity in request body `{ quantity }` | ✅ Yes | `[FromBody] RefundPurchaseRequest` `AdminController.cs:270-271`; frontend `{ quantity }` post; `RefundPurchase_Success_PassesQuantityBody…` |
| D10 No ripple consumer changes | ✅ Yes | `git log` apply range: PaymentService/IMercadoPagoClient/MetricsService/EventService/ReservationService/TicketService 0 commits; `AuditActionType.RefundPurchase` re-used (no enum change) |

## Deferred Decisions (confirmed resolved)

| Decision | Resolution | Evidence |
|----------|-----------|----------|
| Scan-race coverage for partial state (APR-011) | ✅ InMemory sequential serialize test + trio precedent | `RefundPurchaseAsync_ConcurrentQuantityGuard_SecondSeesFirstCommittedState`; FOR UPDATE arm documented Npgsql-only |
| Migration apply (5.2) live-DB check | ⏸ Owner rollout | Structurally validated (`AddRefundsTable_BackfillContainsPureSqlInsertSelect` 2/2) + read-side regression test; `ef migrations list` per apply shows 16/16 with AddRefunds pending apply |

## Documented Deviations (from apply-progress, verified)

1. None from design — implementation matches design.md D1–D10, sequence diagrams, migration SQL and test map (apply-progress §Deviations: "None").
2. Test-helper path walk-up + Designer glob `*_AddRefunds*.cs` resolved in-test (apply-progress §Issues); verified on disk (structural test 2/2 green).
3. **Doc nit (apply-progress)**: §Test results reports `AdminControllerPurchaseTests 13/13`; the file actually contains **11** tests (focused run: controller suite contributes 11 of the 32). Combined change-scope total 32/32 (19 service + 11 controller + 2 structural) and the "645 total / 5 failed" claims are accurate.

## TDD Compliance (Strict TDD)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | apply-progress TDD Cycle Evidence table (4 rows, RED→GREEN per task 1.1-1.4, 2.1-2.3, 3.1-3.2, 4.1-4.2) |
| All tasks have tests | ✅ | 13/13 implemented tasks; build-only tasks verified via `dotnet build` + migration structural test; 5.2 is rollout (owner) |
| RED confirmed (tests exist on disk) | ✅ | 19 service + 11 controller + 2 structural + 7 frontend test cases present |
| GREEN confirmed (tests pass) | ✅ | Focused runs at verification time: backend 32/32, frontend 7/7; full-suite evidence confirms zero new failures |
| Triangulation adequate | ✅ | 13 distinct service behaviors, 11 controller behaviors, 2 migration-structural, 7 frontend; no single-case spec scenario |
| Safety Net for modified files | ✅ | Pre-existing baselines re-confirmed at verification time (5 backend / 3 frontend, identical to apply-progress) |

**TDD Compliance**: 6/6 checks passed.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit (backend InMemory/Moq) | 32 change-scope | `AdminPurchaseServiceTests.cs` (19), `AdminControllerPurchaseTests.cs` (11), `AddRefundsTable_BackfillContainsPureSqlInsertSelect.cs` (2) | xUnit + Moq |
| Integration (frontend page/route) | 7 | `AdminPurchases.test.jsx` | Vitest + RTL + jsdom |
| E2E | 0 | 0 | not installed |
| **Total change-scope** | **39** | | |

## Changed File Coverage

Coverage analysis skipped — no coverage tool configured in `dotnet test`/`vitest` runs for this repo. Informational per strict-TDD rules (never a failure).

## Assertion Quality

✅ All assertions verify real behavior — no tautologies, no type-only standalone assertions, no ghost loops. Service tests assert state deltas (counts, exact ids, Σ amounts, status flips, no-change on failure); controller tests assert exact error mapping + audit predicates; frontend tests assert badge text, disabled state, POST body, invalidation refetch and error surfacing. `RefundPurchaseAsync_QuantityZeroOrNegative_ThrowsNoChange` uses a `[Theory]` with 0 and -1. Frontend mock ratio healthy (4 `vi.mock` : 24 `expect`-bearing assertions across 7 tests).

## Issues Found

**CRITICAL (change content)**: None.

**BLOCKER (machine gate)**: `blockers: 1` — full-suite `dotnet test` exits 1 due exclusively to the **5 pre-existing baseline failures** (CSRF webhook, S3 upload, MP webhook signature ×2, email retry), all in files untouched by this change and identical to the apply-progress baseline; frontend `npx vitest run` exits 1 due to the **3 pre-existing baseline failures** (Checkout ×2, identityValidation ×1). No failure is attributable to this change; every change-scope test (39) passes. This blocker requires no change-scope remediation; it is repository baseline debt.

**WARNING**:
1. Task 5.2 (migration apply, manual PG) is **PENDING-OWNER**: `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext` has NOT been run against the shared Supabase dev DB (and must not be run during verification). Structurally validated + read-side tested, but live backfill row-count/AdminId-NULL confirmation is a rollout step the owner must execute (and confirm `ef migrations list` 16/16, backfilled rows AdminId NULL).
2. Apply-progress §Test results misstates `AdminControllerPurchaseTests 13/13` — the file contains 11 tests (combined 32/32 focused count is correct). Documentation nit only; no test is missing from the file.

**SUGGESTION**:
1. The prompt/orchestrator brief cites "17 scenarios"; the delta spec on disk authoritatively contains **18** scenario headings (APR-003 contributes 6: partial happy, full-at-zero flip, no Approved, K>active, K≤0, concurrent serialize). This report uses the on-disk count; keep the canonical spec as the source of truth for the archive report.
2. The InMemory serialize test is sequential, not a true concurrent run; the Npgsql `FOR UPDATE` arm remains covered by the ReservationService/EventService trio precedent rather than a dedicated integration test. A future Npgsql-backed integration test of two interleaved refunds would close the only remaining runtime gap (informational; design D10/APR-011 accept the precedent).

## Verdict

**FAIL (machine gate: command-exit)** — persistable, not archive-ready **solely because the repository baseline suite is red** (5 backend + 3 frontend pre-existing failures in untouched files). From the change's own content: **PASS** — 8/8 requirements, 18/18 scenarios COMPLIANT with green covering tests, zero new failures, all 39 change-scope tests green, all invariants verified in code and design D1–D10 followed. Archive can proceed once the orchestrator acknowledges the pre-existing baseline-red suite and completes the owner rollout step 5.2 (migration apply), both outside this change's scope.
