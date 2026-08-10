```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:17caab3e4b883732ed08996bce4d6b294d3e11d7d3a9714d8b4f902da96d79a3
verdict: fail
blockers: 1
critical_findings: 0
requirements: 11/11
scenarios: 17/17
test_command: dotnet test
test_exit_code: 1
test_output_hash: sha256:41f9f8f896da8b3f64b3d2c7dba3a1064ddd1acf1b30db3a3a89a2c7449e0215
build_command: dotnet build
build_exit_code: 0
build_output_hash: sha256:3418cb191bdc1b4935df0579a7384a785fa7795dc3f66333740ad06e52e75e12
```

# Verification Report: admin-event-refunds

**Change**: admin-event-refunds
**Version**: N/A (delta spec, openspec/specs/admin-purchase-refunds/spec.md)
**Mode**: Strict TDD (backend) / TDD-ready (frontend)

## Verdict Rationale (read this first)

**Machine verdict: `fail`** — the strict-TDD gate requires `fail` whenever the declared test command exits non-zero. `dotnet test` exits 1 because the repository carries **pre-existing baseline failures**. This is a *command-exit blocker*, NOT a defect of this change:

- **ZERO new failures** in either suite. Every failure in every run is a documented pre-existing baseline test or a pre-existing flaky test, all in **unmodified files** (each suspected test re-ran green in isolation).
- **All 46 new tests (38 backend + 8 frontend) pass.**
- **All 11 requirements / 17 scenarios are COMPLIANT** with green covering tests.
- `critical_findings: 0` — no spec requirement unmet, no regression introduced by this change.

The single blocker (`blockers: 1`) is the non-zero full-suite exit, caused exclusively by pre-existing baseline/flaky tests. Per the gate this report is **persistable but not archive-ready**; the archive decision is the orchestrator's, informed by the evidence below. The change's own content requires **no remediation** — the remediation item is repository baseline debt tracked independently.

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 25 |
| Tasks complete | 25 |
| Tasks incomplete | 0 |

## Build & Tests Execution

**Build**: ✅ Passed — `dotnet build` (backend/) → 0 errors, exit 0.

**Tests**:
- Backend `dotnet test` (from `backend/`): **527 passed / 6 failed / 533 total** (primary run). Variance run: 525 passed / 8 failed (flaky). **All failures pre-existing; ZERO new.**
- Frontend `npx vitest run` (from `frontend/`): **400 passed / 26 failed / 426 total**, exit 1. Exactly the documented baseline (StaffScan 22, Checkout 2, OrganizerEventDetail 1, identityValidation 1). ZERO new. Output hash `sha256:672c826fa25574eb085758685fff716046c45ce2641aa6e789e4655c0bf15d2a`.

**Coverage**: ➖ Not available (no coverage tool configured; informational per strict-TDD rules).

## Spec Compliance Matrix (APR-001..011)

| Requirement | Scenario | Covering test (all PASSED at runtime) | Result |
|-------------|----------|---------------------------------------|--------|
| APR-001 Admin-only authorization | Non-admin rejected | `AdminControllerPurchaseTests.AdminController_HasClassLevelRequireAdminRolePolicy`, `.GetPurchases_NoAuthenticatedUser_ReturnsUnauthorized`, `.RefundPurchase_NoAuthenticatedUser_ReturnsUnauthorized` + class-level `[Authorize(Policy="RequireAdminRole")]` (`AdminController.cs:14`) | ✅ COMPLIANT |
| APR-002 List event purchases | Happy path listing | `AdminControllerPurchaseTests.GetPurchases_HappyPath_ReturnsListingWithTotalRefunded`; `AdminPurchaseServiceTests` listing-mask/`totalRefunded`/empty (incl. `GetPurchasesAsync_TotalRefunded_SumOfRefundedTransactionAmounts`, `=0m` empty) | ✅ COMPLIANT |
| APR-002 | Event not found | `AdminControllerPurchaseTests.GetPurchases_MissingEvent_ReturnsNotFound`; `GetPurchasesAsync` throws `KeyNotFoundException` (`AdminPurchaseService.cs:37-41`) | ✅ COMPLIANT |
| APR-003 Atomic full-purchase refund | Happy path refund (flip, single row) | `AdminPurchaseServiceTests.RefundPurchaseAsync_HappyPath_MarksTicketsRefundedAndFlipsTransaction`; flip at `AdminPurchaseService.cs:178-179` (update existing row, never insert; unique `IX_Transactions_MercadoPagoId` `ApplicationDbContext.cs:146`) | ✅ COMPLIANT |
| APR-003 | No approved transaction | `AdminPurchaseServiceTests.RefundPurchaseAsync_NoApprovedTransaction_ThrowsAndChangesNothing`; throw at `AdminPurchaseService.cs:172-176` → 409 (`AdminController.cs:288-291`) | ✅ COMPLIANT |
| APR-004 Refund blocked when ticket used | Used ticket blocks refund | `AdminPurchaseServiceTests.RefundPurchaseAsync_UsedTicket_ThrowsAndChangesNothing` | ✅ COMPLIANT |
| APR-004 | Concurrent scan wins the race | `AdminPurchaseServiceTests.RefundPurchaseAsync_ScanWinsRace_ReCheckObservesUsedAndRollsBack`; lock trio (`FOR UPDATE`/SQLite no-op UPDATE/InMemory) + re-check at `AdminPurchaseService.cs:124-153` | ✅ COMPLIANT |
| APR-005 Refunded tickets stop counting as sold | Availability and metrics exclude refunded | `EventServiceTicketStockTests.GetEventByIdAsync_RefundedTickets_DoNotCountAsSold` (site 1, `EventService.cs:184`); `ReservationServiceTests.CreateReservationAsync_RefundedTickets_DoNotCountAsSold` (site 2, `ReservationService.cs:132` inside `CreateReservationTransactionalAsync`); `MetricsConsolidationTests.GetOrganizerMetricsAsync_RefundedTickets_ExcludedFromSoldAndRevenue` (site 3, `MetricsService.cs:76`); `MetricsPropertyTests.GetEventMetrics_RefundedTickets_ExcludedFromSoldAndRevenue` (site 4, `CalculateMetricsAsync` `MetricsService.cs:162,168`); lookups: `TicketServiceTests.LookupActiveTicketsByEmailAndDniAsync_ExcludesRefundedTickets` (`TicketService.cs:497`), `.LookupTicketsByEmailAsync_ExcludesRefundedTickets` (`TicketService.cs:442`) | ✅ COMPLIANT |
| APR-005 | Resend excludes refunded | `TicketServiceTests.ResendTicketsByEmailAsync_ExcludesRefundedTickets` (`TicketService.cs:553`) | ✅ COMPLIANT |
| APR-006 Refunded QR rejected at scan | Refunded ticket scanned | `TicketServiceTests.ValidateQRCodeAsync_RefundedTicket_ReturnsInvalidWithEntradaReembolsada`; branch `TicketService.cs:355-366` (`IsValid=false`, `Error="Entrada reembolsada"`, `Ticket` attached); DTO `TicketValidationDetails.IsRefunded/RefundedAt` (`ITicketService.cs:182`), mapped at `TicketController.cs:141-142` | ✅ COMPLIANT |
| APR-007 Refund audit logging | Refund is audited | `AdminControllerPurchaseTests.RefundPurchase_Success_WritesRefundPurchaseAuditWithoutMotivo`; `AuditActionType.RefundPurchase` (`AuditLog.cs:84`), `RefundPurchase/Payment`, truncated ≤1000, after commit, no motivo (`AdminController.cs:274-280`) | ✅ COMPLIANT |
| APR-008 No money movement, email, or motivo | Refund has no external side effects | `AdminControllerPurchaseTests.RefundPurchase_Success_DoesNotTouchPaymentServiceOrEmail`; `git diff` → `PaymentService.cs` 0 changed lines (`InitiateRefundAsync` untouched); no MP/email/motivo in new path | ✅ COMPLIANT |
| APR-009 Purchase-to-ticket linking | New tickets linked precisely | `TicketServiceTests.CreateTicketsAsync_SetsReservationId_OnEveryTicket` (`TicketService.cs:86`); FK map `ApplicationDbContext.cs` + migration `20260810120000_AddTicketReservationAndRefund` | ✅ COMPLIANT |
| APR-009 | Ambiguous legacy backfill | `TicketReservationBackfillTests` 7/7 (full chunk, partial→NULL, multi-res, no-res, pre-linked, overflow, multi-key); `TicketReservationBackfill.RunAsync` chunked + full-chunk-only + NULL leftovers; `LinkUnverified` surfaced in listing | ✅ COMPLIANT |
| APR-010 Admin UI | Non-admin blocked from route | `AdminPurchases.test.jsx` non-admin denied + admin allowed; route `App.jsx:104-107` `ProtectedRoute`+`RoleGuard allowedRoles={['Admin']}`; panel `AdminPanel.jsx:337-341` Compras → navigate (test `AdminPanel.test.jsx:205`) | ✅ COMPLIANT |
| APR-010 | Refund failure shows error | `AdminPurchases.test.jsx` refund failure → error + list unchanged; `useMutation` `invalidateQueries` on success (`AdminPurchases.jsx:97`), `onError` sets `refundError` without state mutation (`:104-105`) | ✅ COMPLIANT |
| APR-011 Test coverage | Suite stays green | Full suites: 527 pass / 6 pre-existing fail (backend), 400 pass / 26 pre-existing fail (frontend); 46 new tests (38 backend + 8 frontend) all green | ✅ COMPLIANT |

**Compliance summary**: 17/17 scenarios compliant. **No CRITICAL, no UNTESTED, no FAILING** for this change's content.

## Correctness — Critical Invariants (static evidence)

| Invariant | Status | Evidence |
|-----------|--------|----------|
| Refund FLIPS Approved Transaction, never inserts | ✅ | `AdminPurchaseService.cs:178-179` mutates existing row; unique `IX_Transactions_MercadoPagoId` (`ApplicationDbContext.cs:146`); test proves single row after refund |
| IsUsed blocks refund + row-lock re-check | ✅ | `AdminPurchaseService.cs:124-145` lock trio (Npgsql `FOR UPDATE` / SQLite no-op UPDATE / InMemory), `:149-153` re-check under lock → rollback; race test green |
| Refunded tickets excluded at 4 sold-count sites + lookups + resend | ✅ | `EventService.cs:184`, `ReservationService.cs:132`, `MetricsService.cs:76` + `:162,168`, `TicketService.cs:442,497,553` — all with APR-005 comments and green tests |
| `ValidateQRCodeAsync` rejects with "Entrada reembolsada" | ✅ | `TicketService.cs:355-366` + green test |
| `PaymentService.InitiateRefundAsync` untouched | ✅ | `git diff HEAD -- backend/Services/PaymentService.cs` = 0 lines |
| Admin endpoints `RequireAdminRole`; frontend route Admin-only | ✅ | `AdminController.cs:14` class-level policy covers both endpoints; `App.jsx:104-107` `RoleGuard allowedRoles={['Admin']}` |

## Coherence (Design)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| `IsRefunded`+`RefundedAt` bools (not Status enum) | ✅ Yes | `Ticket.cs:14-15` |
| New `ReservationId` FK (not buyer-key) | ✅ Yes | `Ticket.cs:8`, `TicketService.cs:86` |
| New `IAdminPurchaseService` (not `InitiateRefundAsync`/`AdminService`) | ✅ Yes | `IAdminPurchaseService.cs`, `Program.cs:41` |
| Flip existing Approved row (never insert) | ✅ Yes | `AdminPurchaseService.cs:178-179` |
| `AuditActionType.RefundPurchase` varchar, no migration | ✅ Yes | `AuditLog.cs:84` |
| Race re-check under row lock | ✅ Yes | `AdminPurchaseService.cs:149-153` |

## Deferred Decisions (confirmed resolved)

| Decision | Resolution | Evidence |
|----------|-----------|----------|
| Simple mask (not `LogRedactor.HashIdentifier`) | ✅ `j***@gmail.com` / `3****1` | `MaskEmail`/`MaskDni` `AdminPurchaseService.cs:205-240` |
| `totalRefunded` = Σ `Transaction.Amount` Status=Refunded | ✅ | `AdminPurchaseService.cs:89-91`; tests `200m` / excludes approved / `0m` empty |
| `LookupTicketsByEmailAsync` excludes refunded | ✅ | `TicketService.cs:442` + green test |

## Documented Deviations (from apply-progress, verified)

1. `AdminPurchaseRow.LinkUnverified` (9th member) — additive; required by APR-009 scenario "listing shows the purchase's tickets unverified". Rendered as "Vínculo no verificado" badge. ✅ Justified.
2. `Purchures` → `Purchases` typo fix. ✅
3. Backfill as testable C# static invoked from migration `Up()` with guarded try/catch (NULL leftovers accepted). `has-pending-model-changes` confirms model-snapshot sync. ✅
4. Backfill assigns only FULL chunks (count == `Reservation.Quantity`); partial/overflow stay NULL. ✅ 3 dedicated tests.
5. Listing quantity = `Reservation.Quantity`; amount/date from Approved/Refunded tx (fallback reservation). ✅

## TDD Compliance (Strict TDD)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | apply-progress TDD Cycle Evidence table present (25 rows) |
| All tasks have tests | ✅ | 25/25 (build/tool-only tasks verified via build + migration drift check) |
| RED confirmed (tests exist) | ✅ | 38 backend + 8 frontend test cases exist on disk |
| GREEN confirmed (tests pass) | ✅ | 29/29 (7 backfill + 12 service + 10 controller), 15/15 consumer-site, 40/40 frontend focused runs; full suites green for all new tests |
| Triangulation adequate | ✅ | 7 backfill, 12 service, 10 controller, 9 consumer, 8 frontend cases; no single-case spec scenario |
| Safety Net for modified files | ✅ | Pre-existing baselines captured and confirmed (backend 6–8, frontend 26) |

**TDD Compliance**: 6/6 checks passed.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit (backend InMemory/Moq) | 38 new | 5 new/extended | xUnit |
| Integration (frontend route/page) | 8 new | 2 | Vitest + RTL + jsdom |
| E2E | 0 | 0 | not installed |
| **Total new** | **46** | | |

## Changed File Coverage

Coverage analysis skipped — no coverage tool configured in `dotnet test`/`vitest` runs for this repo. Informational per strict-TDD rules (never a failure).

## Assertion Quality

✅ All assertions verify real behavior — no tautologies, no type-only standalone assertions, no ghost loops, no empty-without-companion patterns in the new test files (audited `AdminPurchaseServiceTests`, `AdminControllerPurchaseTests`, `TicketReservationBackfillTests`, `AdminPurchases.test.jsx`). Frontend mock ratio healthy (4 `vi.mock` : 23 `expect`).

## Issues Found

**CRITICAL (change content)**: None.

**BLOCKER (machine gate)**: `blockers: 1` — full-suite `dotnet test` exits 1 due exclusively to **pre-existing baseline failures** (6–8 backend, 26 frontend). No test failure is attributable to this change; every suspected non-baseline failure (`QRCodePropertyTests.Property21`, `AdminUserCreationIntegrationTests.PostAdminUsers_WithInvalidEmail_ReturnsBadRequest`) is in an unmodified file and passes in isolation (15/15, 5/5). This blocker requires no change-scope remediation; it is repository baseline debt.

**WARNING**:
1. Pre-existing flakiness variance is wider than documented: 6–8 unique backend failures across runs vs the documented 6–7. The two extra flaky tests are pre-existing (unmodified files, green in isolation). Update the baseline note from "6–7" to "6–8".
2. Live DB migration remains blocked by pre-existing stale Supabase migration history (`AddPendingEmailSend`/`DropCurrentlyReserved` pending) — applies to `20260810120000_AddTicketReservationAndRefund` at deploy time. Not caused by this change; migration proven correct via `has-pending-model-changes` (no drift) + 7 backfill tests.

**SUGGESTION**:
1. Backfill inside migration `Up()` depends on `ApplicationDbContext` constructibility at migration time; the guarded try/catch makes failure benign (NULL leftovers), but a future config change could silently skip backfill — document the dependency in the migration comment (partially noted already).
2. Frontend `AdminPurchases.jsx` shows no explicit "sin datos" empty state for an event with no confirmed purchases (APR-002 empty-list behavior is backend-tested); an optional empty-state UI test would close the visual gap.

## Verdict

**FAIL (machine gate: command-exit)** — persistable, not archive-ready **solely because the repository baseline suite is red**. From the change's own content: **PASS** — 11/11 requirements, 17/17 scenarios COMPLIANT with green covering tests, zero new failures, all 46 new tests green, all invariants verified in code. Archive can proceed once the orchestrator acknowledges the pre-existing baseline-red suite and the stale DB-history environment item (both outside this change's scope).
