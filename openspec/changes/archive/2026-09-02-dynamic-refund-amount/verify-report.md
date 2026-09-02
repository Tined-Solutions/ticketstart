```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:1f130ec84799c7ca5ce906387cee57960d20a88180a05f67071c589f3f899d70
verdict: pass
blockers: 0
critical_findings: 0
requirements: 4/4
scenarios: 23/23
test_command: dotnet test --filter "FullyQualifiedName!~TicketeraOnline.Api.Tests.AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader&FullyQualifiedName!~TicketeraOnline.Api.Tests.EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client&FullyQualifiedName!~TicketeraOnline.Api.Tests.EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately&FullyQualifiedName!~TicketeraOnline.Api.Tests.PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~TicketeraOnline.Api.Tests.PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~TicketeraOnline.Api.Tests.PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted" (cwd backend/)
test_exit_code: 0
test_output_hash: sha256:48796e3a0f64f34779fc5af95cea743fcc7ae542f7320914a72626af54c02000
build_command: dotnet build (cwd backend/) exit 0; npm run build (cwd frontend/) exit 0
build_exit_code: 0
build_output_hash: sha256:8d56e7f42a25344dbd6cb10483b83f874483041a3aa6902978fbd2c87519b878
```

# Verify Report: dynamic-refund-amount

- **Change**: `dynamic-refund-amount`
- **Branch**: `feat/dynamic-refund-amount` — candidate `165ea49763bca13d8201eb68ba360931b31c020b`, base `bd7b7ccb1c8f789787bc2ccd7cb9383f638a7eb4`
- **Date**: 2026-09-02
- **Mode**: Strict TDD (backend xUnit/FsCheck per `skills/dotnet-testing`; frontend Vitest per `skills/react-testing`)
- **Verdict**: **PASS** — 4/4 requirements, 23/23 scenarios, 0 blockers, 0 CRITICAL, 0 WARNING
- **Evidence-revision preimage**: `cat verify-evidence-backend-exbaseline.txt verify-evidence-frontend.txt | sha256sum` → `sha256:1f130ec84799c7ca5ce906387cee57960d20a88180a05f67071c589f3f899d70` (logs preserved at `/tmp/opencode/`)

## Verdict rationale (read this first)

The envelope `test_command` is the change-scope suite: the full `dotnet test` run **minus exactly the 6 named pre-existing baseline tests** (out of scope for this change — each excluded by full test FQN, listed verbatim in the filter; same convention as the admitted `admin-user-management` verification). The full unfiltered runs were also executed — twice each — and are documented below with zero new failures on every run.

- **ZERO new failures** in either suite. Every failure in every unfiltered run is a documented pre-existing baseline, all verified failing at base `bd7b7cc` (backend: CSRF webhook, S3 upload, notification-queue timing, MP webhook signature ×2, email retry; frontend: Checkout ×2, identityValidation DNI letters). None touch refund code.
- **All change-scope suites pass at runtime**: refund-filtered backend run **40/40** (service 24 + controller 11 + FsCheck 5); frontend refund dialog/format suites green.
- **All 4 requirements / 23 scenarios COMPLIANT** with green covering tests (mapping below); all six design decisions D1–D6 hold in code; all five non-goals hold.
- `critical_findings: 0`, `blockers: 0` — no spec requirement unmet, no regression introduced by this change.

## Observed test evidence (exact commands and counts)

| # | Command (cwd) | Observed result | Exit |
|---|---|---|---|
| 1 | `dotnet test` (backend/) — full unfiltered, run A | **Passed 725 / Failed 5 / Total 730** | 1 (baselines only) |
| 2 | `dotnet test` (backend/) — full unfiltered, run B | **Passed 725 / Failed 5 / Total 730** (identical counts) | 1 (baselines only) |
| 3 | `dotnet test --filter "…!~<6 baseline FQNs>"` (backend/) — change-scope envelope command | **Passed 724 / Failed 0 / Total 724** | **0** |
| 4 | `dotnet test --filter "FullyQualifiedName~AdminPurchaseServiceTests\|FullyQualifiedName~AdminControllerPurchaseTests\|FullyQualifiedName~AdminPurchaseRefundPropertyTests"` (backend/) — refund suites | **Passed 40 / Failed 0 / Total 40** (service 24, controller 11, FsCheck 5) | 0 |
| 5 | `npm test` (frontend/) — full unfiltered | **Passed 490 / Failed 3 / Total 493** (re-run reproduced identical counts) | 1 (baselines only) |
| 6 | `dotnet build` (backend/) | 0 Warning(s), 0 Error(s) | **0** |
| 7 | `npm run build` (frontend/) | built, chunk-size warnings only | **0** |

**Backend unfiltered failures (runs A/B — all ⊂ the 6 pre-existing baselines, none in refund code)**: `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`, `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client`, `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`. The sixth baseline (`EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` — a wall-clock timing assertion, "Enqueue took 3897ms, expected <1000ms") flaked GREEN in both full runs (hence 725/5 vs the 724/6 base baseline) and flaked RED once in the intermediate filtered run, confirming its documented env-dependent (timing/load-sensitive) nature. Unfiltered run B preimage: `sha256:1a213bf42baec540ca4a6ee84b46f94b6fa256c390662a57643c2ad84ee4696b` (`/tmp/opencode/verify-evidence-backend.txt`).

**Frontend failures (exactly the 3 pre-existing baselines)**: `Checkout.test.jsx > returns to the reservation form when clicking Editar datos, preserving input data`; `Checkout.test.jsx > sends a PATCH request when saving edits on an existing reservation`; `identityValidation.test.js > rejects DNI with letters`. Zero new failures; refund dialog/format suites fully green. Full-suite preimage: `sha256:c6c45061493b10041578d37fbaf10d13fa3b87d3d92e707ae0b6d01149a56bd5` (`/tmp/opencode/verify-evidence-frontend.txt`).

## Requirements and scenarios (4/4 requirements, 23/23 scenarios)

### APR-003: Atomic quantity-based refund — PASS (11/11 scenarios)

Implementation evidence (`backend/Services/AdminPurchaseService.cs`, `backend/Controllers/AdminController.cs`, `backend/Services/IAdminPurchaseService.cs`):

- DTO `RefundPurchaseRequest(int Quantity, decimal Amount)` (AdminController.cs:557) — annotation-free positional record per repo convention; missing body → 400 via `[ApiController]`.
- 4-arg signature `RefundPurchaseAsync(reservationId, quantity, amount, adminId)` (IAdminPurchaseService.cs:43); controller passes `request.Quantity, request.Amount` through (AdminController.cs:298).
- Guard order inside the locked transaction is exactly D3: IsUsed (line 157) → quantity `<= 0 || > active` (166–171) → amount `<= 0` (178) → `decimal.Round(amount, 2) != amount` (183) → `amount > unitPrice * quantity` (188) → Approved-tx check (197–203). Quantity guards demonstrably fire before amount guards.
- All three exception messages match design D3 verbatim, `CultureInfo.InvariantCulture` via `string.Create` (181, 186, 191–192); `InvalidOperationException` → 409 mapping (317–320); controller semantics 200/404/409/500 preserved.
- K oldest non-refunded/non-used tickets selected by `CreatedAt` (207–211); exactly those marked `IsRefunded`/`RefundedAt` (213–217), never deleted.
- One `Refunds` row per operation with `Amount = amount` stored verbatim (222–230).
- Flip invariant: `approvedTx.Status = Refunded` only when `active == quantity` (234–238); partial ops leave the transaction Approved.
- Audit after commit (APR-007) with the amount, no motivo (303–309).
- Scenario → test mapping: partial happy path (`RefundPurchaseAsync_Partial_MarksTwoTicketsAndLeavesTxApproved`), custom verbatim 50.5 (`RefundPurchaseAsync_CustomAmountStoredVerbatim` + FsCheck Property 1), full flip at zero active (`RefundPurchaseAsync_FullAtZeroActive_FlipsTransaction`), no approved tx (`RefundPurchaseAsync_NoApprovedTransaction_ThrowsNoChange`), qty > active (`RefundPurchaseAsync_QuantityAboveActiveRemaining_ThrowsNoChange`), qty ≤ 0 (`RefundPurchaseAsync_QuantityZeroOrNegative_ThrowsNoChange`, [Theory] 0/−1), guard ordering (`RefundPurchaseAsync_QuantityGuardFiresBeforeAmountGuard`, asserts `DoesNotContain("Refund amount")`), amount > cap 200.01 (`RefundPurchaseAsync_AmountAboveCap_ThrowsNoChange`), amount ≤ 0 (`RefundPurchaseAsync_AmountZeroOrNegative_ThrowsNoChange`), > 2 decimals 33.333 rejected not rounded (`RefundPurchaseAsync_AmountMoreThanTwoDecimals_RejectedNotRounded`), concurrent serialization (`RefundPurchaseAsync_ConcurrentQuantityGuard_SecondSeesFirstCommittedState`).

### APR-010: Admin UI — PASS (7/7 scenarios)

Implementation evidence (`frontend/src/pages/AdminPurchases.jsx`, `frontend/src/lib/format.js`):

- Amount input `step 0.01` (line 146) prefilled to K × unit price via integer-cents math; prefill recomputes on quantity change when not dirty (62–68).
- `toCents` derives cents from decimal STRINGS, never float arithmetic (27–30); `unitPriceCents = Math.round(toCents(purchase.amount) / purchase.quantity)` (51); cap `= unitPriceCents × K`.
- Percent helper = quick buttons 25/50/75/100 (153–165), one-shot amount write with `Math.round((pct * capCents) / 100)` half-up (77–80); no persistent percent state (D1).
- Inline validation mirroring D3: > 2 decimals (`decPart.length > 2`, 84–88) → ≤ 0 (89–90) → > cap (91–93); inline error with `role="alert"` (171); `handleConfirm` blocks when `amountError` set (95–96) — no mutation sent.
- Live cents preview via `formatCurrency(…, { fractionDigits: 2 })` (166–169).
- Mutation posts `{ quantity, amount }` only (215–218) — a percent never crosses the wire; on success invalidates the purchases query (220–225); on failure shows the error without mutating the list (232).
- Refund button disabled when fully refunded (316); badges render "X de Y reembolsadas" with error variant when fully refunded and warning when partial (37–43).
- Dialog state resets on remount (conditionally rendered with `useState` initializers, 334–343).
- Scenario → test mapping (`frontend/src/pages/AdminPurchases.test.jsx`): non-admin route guard denied (419) / admin allowed (432); post body `{quantity, amount}` + row update (150); prefill K × price → 300 (275); 25% → 50, never posts a percent (296); 100% cap (321); amount ≤ 0 blocks (328); > cap blocks (340); > 2 decimals flagged inline (352); cap re-validation while dirty on quantity change (364); cents preview (385); remount reset (395); failure shows error, list unchanged (220); fully-refunded row disabled + badge variant (108, 216).

### APR-011: Test coverage — PASS (1/1 scenario)

- Strict TDD honored per `skills/dotnet-testing` (Red→Green); mechanical 4-arg/2-arg updates in place; replaced-test drift is D6-sanctioned (current suite names; the renamed controller test `RefundPurchase_Success_PassesAmountBodyAndWritesAuditWithoutMotivo` — a mechanical, spec-sanctioned rename absent from the apply-progress deviation list — is legitimate, not a discrepancy).
- New service guard/verbatim/cumulative tests present (AdminPurchaseServiceTests.cs: 24 cases including the qty ≤ 0 [Theory]).
- FsCheck property suite (`AdminPurchaseRefundPropertyTests.cs`, 5 properties, integer-cents generators per design): ledger Amount == amount verbatim (:157), exactly K tickets marked (:188), Σ Refunds ≤ tx.Amount after EVERY operation (:213), flip iff 0 active (:254), invalid amounts rejected with NO state change (:281). Suite green (5/5 within the 40-test refund-filter run).
- Controller tests cover the `{ quantity, amount }` body with an EXACT decimal `Verify` — `RefundPurchaseAsync(reservationId, 2, 200m, adminId)` (AdminControllerPurchaseTests.cs:173–174) — and the audit string carrying "amount"/"200" while `!Contains("motivo")`/`!Contains("reason")` (:179–189).
- Frontend Vitest covers mock shape (`refundedQuantity`, `refundedAmount` at lines 58/70), post body, prefill, percent conversion, invalid amount blocking, cap re-validation, badge variants; `format.test.js` covers `fractionDigits: 2` ("$ 300,50" / "$ 50,00" / "$ 1.234,56"), null/undefined "$ --", and default whole-pesos preservation (:92–109).
- "Suite stays green" scenario: both full suites reproduce baseline behavior with zero new failures on every run (see evidence table).

### APR-012: Cumulative refund operation record — PASS (4/4 scenarios)

- Exactly one `Refunds` row per operation with ReservationId, `TicketIds[]`, Quantity, Amount (verbatim), AdminId, CreatedAt (AdminPurchaseService.cs:222–230); row-recorded test (`RefundPurchaseAsync_InsertsRefundRow_WithTicketIdsQuantityUnitPriceAmountAdminId`, full-price parity 200m) and verbatim test (50.5).
- Per-event `TotalRefunded` = Σ `Refunds.Amount` (AdminPurchaseService.cs:118); `AdminPurchaseRow` exposes `RefundedQuantity`/`RefundedAmount` and derived `Refunded` (:103–114).
- Cumulative second refund appends with custom amounts 150.25/199.5 and asserts Σ ≤ tx.Amount after every op (`RefundPurchaseAsync_Cumulative_SecondRefundAppendsAndFlipsAtZero`, :216–242) + FsCheck Property 3.
- "Custom refund" is derived contextually only — no stored flag anywhere in the model or listing.

### MODIFIED Purpose — PASS

The Purpose paragraph holds end-to-end: admin-defined decimal amount (0 < A ≤ unit price × K), ledger-only refunds, refunded tickets marked not deleted, flip only at zero active, refunded tickets excluded from sold counts, no Mercado Pago movement, no email, no motivo.

## Design decisions D1–D6 — all hold in code

- **D1** quick buttons 25/50/75/100, one-shot write, no persistent percent state — AdminPurchases.jsx:75–80, 153–165. ✔
- **D2** `formatCurrency(amount, { fractionDigits = 0 })`; default preserves all consumers; `fractionDigits: 2` renders es-AR cents ("$ 300,50") — format.js:64–70; tests format.test.js:98–109. ✔
- **D3** value-based `decimal.Round(amount, 2) != amount` detection (accepts 50.500, rejects 33.333); guard order IsUsed → quantity → amount (≤0 → >2 dec → >cap) → Approved-tx; exact messages, InvariantCulture — AdminPurchaseService.cs:177–203. ✔
- **D4** integer-cents string math, `isAmountDirty` flag, `Math.round` unit-price fallback, half-up percent, inline >2-decimal flag, remount reset — AdminPurchases.jsx:24–98. ✔
- **D5** audit detail `Admin refunded {Quantity} tickets of purchase {ReservationId} for event {EventId}, amount {Amount}` with InvariantCulture and `Truncate(…, 1000)` — AdminController.cs:303–309. ✔
- **D6** mechanical updates executed against CURRENT test names; sanctioned rename present. ✔

## Task coverage (17/17 checked — verified against code)

WU1 signature/DTO (1.1–1.5), WU2 guards + verbatim ledger + cumulative extension (2.1–2.4), WU3 FsCheck suite (3.1), WU4 controller body + audit amount (4.1–4.2), WU5 `fractionDigits` (5.1), WU6 dialog (5.2–5.3), Phase 6 full suites (6.1–6.2). All verified above with file/line/test evidence.

## Non-goal checks — all hold

1. **No DB migration**: `git diff --name-only bd7b7cc..HEAD -- backend/Migrations` is empty; only `backend/Models/Refund.cs` changed under Models (doc comment only, task 2.3). No Program.cs changes.
2. **No Mercado Pago call in the manual refund path**: `RefundPurchaseAsync` makes zero external-service calls (EF Core transaction only); controller test `RefundPurchase_Success_DoesNotTouchPaymentServiceOrEmail` (AdminControllerPurchaseTests.cs:251) proves PaymentService is never invoked.
3. **No email** in the refund path: same test asserts no email interaction; no Resend references in the refund code.
4. **No motivo field**: DTO is `(Quantity, Amount)` only; audit assertions pin `!Contains("motivo")`/`!Contains("reason")` (APR-008 invariant preserved).
5. **Percent never crosses the wire**: the mutation posts `{ quantity, amount }` only (AdminPurchases.jsx:217); tests 296/321 assert the body shape and that no percent key is sent.

## Security review (backend-security skill conventions)

- Endpoint remains behind `[Authorize(Policy = "RequireAdminRole")]` (AdminController.cs:15) + `X-CSRF-PROTECT` mutating-request middleware — unchanged.
- Audit written after commit, best-effort, truncated to 1000 chars; no PII/secrets introduced in new log statements (amount/reservation ids are business fields consistent with existing logging).
- No new secrets/config; no new abuse-prone public surface (Admin-only), so no new rate-limiter policy required. Threat matrix "N/A" in design remains accurate.

## Findings by severity

- **CRITICAL**: none.
- **WARNING**: none.
- **SUGGESTION 1** (baseline flake, no action): the six pre-existing backend baselines are all env-dependent, and `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` is a wall-clock timing assertion (< 1000ms) that flakes with machine load (green in both full runs here, red once under a filtered run). Consider a lenient threshold or a load-guard for that test in a future hygiene change.
- **SUGGESTION 2** (edge case, no action for this change): `<input type="number">` can accept scientific-notation strings (e.g. "1e2") in some browsers; the dialog's `toCents`/`Number` conversion still classifies such input correctly against the cap/decimals guards, so spec behavior is preserved. Worth remembering if the dialog ever moves to a text-based input.
- **SUGGESTION 3** (already acknowledged): authored line total 939 vs ~700 forecast (size:exception accepted by maintainer ledger reset, per apply progress) — recorded here for provenance only; no verify action.

## Conclusion

The implementation matches the proposal, the delta spec (APR-003, APR-010, APR-011, APR-012, MODIFIED Purpose), all six design decisions, and all 17 tasks. All 23 delta scenarios map to green, observed tests; all five non-goals hold; both full suites reproduce their pre-existing baselines with zero new failures, and the change-scope (baseline-excluded) backend suite plus both builds exit 0. **Status: PASS — ready for archive.**
