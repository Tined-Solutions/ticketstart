# Design: Dynamic Refund Amount

## Technical Approach

Exploration Approach 1 — amount end-to-end, no migration. `RefundPurchaseRequest`/`RefundPurchaseAsync` gain `amount`; ALL amount validation lives inside the locked transaction (InvalidOperationException → 409); the ledger stores it verbatim. `GetPurchasesAsync` untouched (Σ `Refunds.Amount` survives custom amounts). Wire carries only `{ quantity, amount }`.

## Architecture Decisions

### D1: Percent helper — quick buttons

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Typed % input | Second field; 0 < p ≤ 100 validation; mobile keyboard | Rejected |
| Quick buttons 25/50/75/100 | One tap; 44px targets; no validation surface; spec's 25% scenario; arbitrary values via amount input | **Chosen** |

A percent click is a ONE-SHOT amount write (sets dirty, D4) — no persistent percent state.

### D2: Cent display — extend `formatCurrency`

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Whole pesos only | Contradicts spec (50.5 verbatim); admin confirms blind | Rejected |
| Raw amount beside preview / new formatter | Clutter / duplicates the formatter | Rejected |
| `formatCurrency(amount, { fractionDigits = 0 })` | Default preserves all consumers/tests; dialog preview passes `fractionDigits: 2` | **Chosen** |

Whole-peso convention stays elsewhere; the preview shows cents ("$ 300,50", es-AR comma).

### D3: >2-decimals detection — `decimal.Round(amount, 2) != amount`

| Option | Tradeoff | Decision |
|--------|----------|----------|
| `decimal.GetBits` scale > 2 | Rejects "50.500" although numeric(18,2) stores it exactly | Rejected |
| `decimal.Round(amount, 2) != amount` | Value-based, culture-free; flags only precision that would be LOST (33.333), accepts 50.50/50.500 | **Chosen** |

Inside `RefundPurchaseAsync`, in the transaction, AFTER the quantity guard, BEFORE the Approved-tx check. Order: IsUsed → quantity → amount (`≤ 0` → `> 2 decimals` → `> cap = TicketType.Price × quantity`) → Approved-tx. Exceptions (→ 409; decimals with `CultureInfo.InvariantCulture`):

- `"Refund amount must be greater than zero"`
- `"Refund amount cannot have more than 2 decimal places"`
- `$"Cannot refund {amount} for {quantity} tickets; maximum is {unitPrice * quantity}"`

### D4: Frontend integer-cents math + `isAmountDirty`

Floats drift (0.29 × 100 → 28.999…); cents derive from decimal STRINGS, never float arithmetic:

```js
const toCents = (v) => { const [i, d = ''] = String(v).split('.'); return Number(i) * 100 + Number((d + '00').slice(0, 2)) }
```

`String(number)` is shortest-round-trip → 2-decimal API values round-trip exactly. `unitPriceCents = Math.round(toCents(purchase.amount) / purchase.quantity)` (exact while tx.Amount = Price × Qty; rounding = anomaly fallback). Cap/prefill `= unitPriceCents × K`; percent `= Math.round(pct × capCents / 100)` (half-up). >2-decimal input flagged INLINE (`decPart.length > 2`), mirroring D3.

State: `amount` + `isAmountDirty` boolean (vs null-sentinel: simpler React, cheaper tests). Quantity change: `!dirty → recompute prefill; dirty → keep, re-validate vs the new cap`. Percent click sets both. Resets on remount (cancel → reopen). Submit validates first (`0 < amountCents ≤ capCents`); failure → inline `role="alert"` error, no mutation.

### D5: Audit detail string

`Admin refunded {Quantity} tickets of purchase {ReservationId} for event {EventId}, amount {Amount}` — Amount with `CultureInfo.InvariantCulture`; `Truncate(…, 1000)` unchanged; no motivo (APR-008) preserved.

### D6: Spec drift note

APR-011's "replace" list predates the current suite (tests renamed; frontend mock shape already carries `refundedQuantity`/`refundedAmount`) — tasks are mechanical 4-arg updates against CURRENT names.

## Data Flow

    Dialog (cents math) ──POST {quantity, amount}──→ AdminController → service tx
        (locks → guards → K oldest → ledger Amount = amount → D2 flip) → audit after commit → invalidate query

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Controllers/AdminController.cs` | Modify | DTO gains `Amount`; pass-through; audit (D5) |
| `backend/Services/IAdminPurchaseService.cs` | Modify | 4-arg signature + docs |
| `backend/Services/AdminPurchaseService.cs` | Modify | Amount guards (D3); `Amount = amount` |
| `backend/Models/Refund.cs` | Modify | Doc comment only (D7 no longer always true) |
| `frontend/src/lib/format.js` | Modify | `formatCurrency` options (D2) |
| `frontend/src/pages/AdminPurchases.jsx` | Modify | Amount input (`step 0.01`), % buttons, prefill/dirty, inline validation, post body (D4) |
| `backend/Tests/AdminPurchaseServiceTests.cs` | Modify | 13 sites → 4-arg; new guard/verbatim tests |
| `backend/Tests/AdminControllerPurchaseTests.cs` | Modify | 7 DTO sites → 2-arg; decimal in `Verify`; audit amount |
| `backend/Tests/AdminPurchaseRefundPropertyTests.cs` | Create | FsCheck suite (PaymentPropertyTests pattern) |
| `frontend/src/pages/AdminPurchases.test.jsx` | Modify | Post body, prefill, percent, invalid amount, cap re-validation |
| `frontend/src/lib/__tests__/` | Modify | `formatCurrency` options cases |

## Testing Strategy

Strict TDD (APR-011): mechanical signature/DTO change (compiler-driven) → RED new service tests → GREEN guards → FsCheck suite → controller → frontend.

| Scenario | Test |
|----------|------|
| Partial / full-flip / oldest-K / used / no-tx / unknown / concurrent | Existing `AdminPurchaseServiceTests` (4-arg updates; full-price amounts keep assertions green; parity via `_InsertsRefundRow_…` 200m → 200m) |
| Custom verbatim (50.5); ≤ 0 / > cap (200.01) / >2 dec (33.333, never rounded) rejected; qty guard ordering (assert quantity message) | New service tests |
| Cumulative Σ ≤ tx.Amount; property: Amount == amount, K tickets, Σ ≤ tx.Amount, flip iff 0 active | `_Cumulative_…` with custom amounts; new `AdminPurchaseRefundPropertyTests` (amount gen `Gen.Choose(0, priceCents × K)` → cents/100m) |
| `{quantity, amount}` body + audit with amount, no motivo | `AdminControllerPurchaseTests` (adds `Contains("amount")`, keeps `!Contains("motivo"/"reason")`) |
| Prefill / 25% → 50 / invalid blocks submit / cap re-validation / badges / cents preview | `AdminPurchases.test.jsx` + `lib/__tests__` format tests (Vitest) |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary; in-app validation on an existing endpoint behind existing auth (RequireAdminRole + CSRF middleware).

## Migration / Rollout

No migration required. Single PR; revert restores `Price × K` semantics; custom-amount ledger rows remain valid (Σ unchanged).

## Open Questions

- None blocking. D7 price-mutation cap divergence is pre-existing — out of scope.

## Routing

Next phase: `sdd-tasks` — sequence work per Testing Strategy (mechanical signature/DTO change → RED service guard tests → GREEN guards → FsCheck suite → controller tests → frontend), executing against CURRENT test names per D6.
