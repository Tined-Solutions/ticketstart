# Tasks: Dynamic Refund Amount

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~700 total (authored add+del) — WU1 ~90, WU2 ~195, WU3 ~150, WU4 ~110, WU5 ~35, WU6 ~120 |
| 400-line budget risk | High |
| Chained PRs recommended | No (delivery strategy mandates one PR) |
| Suggested split | Single PR; 6 work units, each = one commit |
| Delivery strategy | single-pr |
| Chain strategy | size-exception (pending maintainer approval) |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

Forecast (~700 lines) exceeds the 400-line single-PR threshold but fits the 800-line repo budget. Per `single-pr`, flag `size:exception` for maintainer approval before apply. Do NOT plan chained PRs. Rollback = single revert; custom-amount ledger rows remain valid history.

### Work Units (clear start → finish → verification → rollback boundary)

| Unit | Goal | Focused test command | Runtime harness | Rollback boundary |
|------|------|----------------------|-----------------|-------------------|
| WU1 | Mechanical 4-arg/2-arg signature+DTO change; behavior identical (full-price parity) | `dotnet test` | N/A — xUnit InMemory/WebApplicationFactory suite is the harness | Revert commit; 3-arg/1-arg API restored |
| WU2 | Amount guards (D3) + verbatim ledger, RED→GREEN | `dotnet test --filter AdminPurchaseServiceTests` | N/A — InMemory DB unit tests | Revert commit; WU1 parity behavior remains |
| WU3 | FsCheck property suite (test-only file) | `dotnet test --filter AdminPurchaseRefundPropertyTests` | N/A — InMemory DB property tests | Revert commit (new file only) |
| WU4 | Controller body tests + audit amount (D5) | `dotnet test --filter AdminControllerPurchaseTests` | N/A — WebApplicationFactory tests | Revert commit; audit reverts to quantity-only wording |
| WU5 | `formatCurrency` `fractionDigits` option (D2) | `npx vitest run src/lib/__tests__/format.test.js` | N/A — jsdom Vitest suite | Revert commit; default whole-pesos format |
| WU6 | Dialog amount input, % buttons, prefill/dirty, validation, post body (D4) | `npx vitest run src/pages/AdminPurchases.test.jsx` | N/A — jsdom Vitest suite | Revert commit; dialog reverts to quantity-only |

## Phase 1: Mechanical signature/DTO change (WU1 — compiler-driven, no behavior change)

- [x] 1.1 `backend/Controllers/AdminController.cs`: `RefundPurchaseRequest(int Quantity)` → `(int Quantity, decimal Amount)`; action passes `amount` through to service. (APR-003)
- [x] 1.2 `backend/Services/IAdminPurchaseService.cs`: `RefundPurchaseAsync(reservationId, quantity, decimal amount, adminId)` + doc update (admin-defined 0 < A ≤ unit price × K). (APR-003/APR-012)
- [x] 1.3 `backend/Services/AdminPurchaseService.cs`: accept `amount`; ledger keeps `Amount = unitPrice * quantity` until 2.2 (parity preserved). (APR-012)
- [x] 1.4 `backend/Tests/AdminPurchaseServiceTests.cs`: update 13 refund call sites to 4-arg passing full-price amounts (`…InsertsRefundRow…` 200m → 200m parity). Verify: `dotnet test` green. (APR-011/APR-012 "Full-price amount preserves today's ledger semantics")
- [x] 1.5 `backend/Tests/AdminControllerPurchaseTests.cs`: update 7 `RefundPurchaseRequest(…)` sites to 2-arg; mock `Verify` calls to 4-arg. Verify green. (APR-011)

## Phase 2: Amount guards — RED then GREEN (WU2)

- [x] 2.1 RED `backend/Tests/AdminPurchaseServiceTests.cs`, new tests: `RefundPurchaseAsync_AmountZeroOrNegative_ThrowsNoChange` (0/negative → 409, no state change); `RefundPurchaseAsync_AmountAboveCap_ThrowsNoChange` (price 100, qty 2, amount 200.01 → 409); `RefundPurchaseAsync_AmountMoreThanTwoDecimals_RejectedNotRounded` (33.333 → 409, no Refunds row, never rounded); `RefundPurchaseAsync_QuantityGuardFiresBeforeAmountGuard` (qty 3 + valid amount → asserts quantity violation message); `RefundPurchaseAsync_CustomAmountStoredVerbatim` (qty 2, 50.5 → Refunds.Amount == 50.5). Verify RED. (APR-003)
- [x] 2.2 GREEN `backend/Services/AdminPurchaseService.cs`: inside tx, after quantity guard, before Approved-tx check (D3 order: IsUsed → quantity → amount (`≤ 0` → `> 2 decimals` → `> cap = TicketType.Price × quantity`) → Approved-tx): `amount <= 0` → "Refund amount must be greater than zero"; `decimal.Round(amount, 2) != amount` → "Refund amount cannot have more than 2 decimal places"; `amount > TicketType.Price × quantity` → `$"Cannot refund {amount} for {quantity} tickets; maximum is {unitPrice * quantity}"` (InvariantCulture in all); ledger `Amount = amount` verbatim. (APR-003/APR-012)
- [x] 2.3 `backend/Models/Refund.cs`: doc comment only — Amount is admin-defined, not always unit price × K. (APR-012)
- [x] 2.4 Extend `RefundPurchaseAsync_Cumulative_SecondRefundAppendsAndFlipsAtZero` with custom-amount operations; assert Σ Refunds ≤ tx.Amount after every op. Verify: filter green. (APR-012 "Cumulative custom amounts never exceed total paid")

## Phase 3: FsCheck property suite (WU3)

- [x] 3.1 Create `backend/Tests/AdminPurchaseRefundPropertyTests.cs` (PaymentPropertyTests pattern; FsCheck.Xunit with GenStatic/PropStatic; InMemory DB; amount gen `Gen.Choose(0, priceCents × K)` → cents/100m). Properties: ledger Amount == amount; exactly K tickets marked IsRefunded; Σ Refunds ≤ tx.Amount after every operation; flip iff 0 active tickets. Verify: filter green. (APR-011/APR-012)

## Phase 4: Controller — body validation + audit (WU4)

- [x] 4.1 RED `backend/Tests/AdminControllerPurchaseTests.cs`: success test Verifies service called with exact decimal amount (4-arg); audit assertion adds `Contains("amount")` and keeps `!Contains("motivo")`/`!Contains("reason")`. (APR-003/APR-008)
- [x] 4.2 GREEN `backend/Controllers/AdminController.cs`: audit detail = `Admin refunded {Quantity} tickets of purchase {ReservationId} for event {EventId}, amount {Amount}` (InvariantCulture; Truncate(…, 1000) unchanged). Verify: filter green. (D5/APR-008)

## Phase 5: Frontend (WU5 → WU6)

- [x] 5.1 RED+GREEN `frontend/src/lib/__tests__/format.test.js` + `frontend/src/lib/format.js`: `formatCurrency(amount, { fractionDigits = 0 })`; default preserves every existing consumer/test; `fractionDigits: 2` renders "$ 300,50" (es-AR comma). (D2/APR-010)
- [x] 5.2 RED `frontend/src/pages/AdminPurchases.test.jsx`: post body `{ quantity, amount }` (update existing `{quantity}`-body test); prefill = K × unit price recomputed on quantity change (qty 3, price 100 → 300); 25% helper → 50 via integer-cents math, body never carries a percent; amount ≤ 0 / > cap blocks submit with inline `role="alert"` error, no mutation; cap re-validation on quantity change when dirty; cents preview. Keep badge/disabled/non-admin cases green (mock shape already carries `refundedQuantity`/`refundedAmount` per D6). (APR-010)
- [x] 5.3 GREEN `frontend/src/pages/AdminPurchases.jsx`: amount input `step 0.01`; 25/50/75/100 quick buttons (one-shot amount write, D1); `amount` + `isAmountDirty` state; `toCents` decimal-string math, `unitPriceCents = Math.round(toCents(purchase.amount) / purchase.quantity)`; prefill `unitPriceCents × K`; percent `Math.round(pct × capCents / 100)`; submit gate `0 < amountCents ≤ capCents`; state resets on remount. (APR-010/D4)

## Phase 6: Final verification (APR-011 "Suite stays green")

- [x] 6.1 `cd backend && dotnet test` — full suite green. (724/730 passed; the 6 failures are pre-existing env-dependent tests verified failing at base bd7b7cc — zero new failures)
- [x] 6.2 `cd frontend && npm test` — full suite green. (490/493 passed; the 3 failures — Checkout ×2, identityValidation ×1 — verified failing at base bd7b7cc; zero new failures)

## Decision Needed Before Apply

Yes — obtain `size:exception` (maintainer approval) before `sdd-apply`: ~700-line forecast exceeds the 400-line single-PR threshold.
