# Design: Admin Purchases Net Amount

## Technical Approach

Implement the APR-016 display change entirely in `AdminPurchases.jsx`. Derive row net amounts and the event summary from the already-loaded purchases payload; do not change refund requests, dialog derivation, API contracts, or canonical specifications.

## Architecture Decisions

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Mutate `purchase.amount` after refund | Simplifies later rendering but corrupts the dialog's unit-price/cap inputs | **Reject** — keep source data immutable and derive the display value |
| Aggregate totals in the backend | Adds API and server-scope changes for a display-only requirement | **Reject** — sum `purchase.amount` in the frontend; use `data.totalRefunded` verbatim |
| Add a formatter or fraction options | Unnecessary surface for whole-peso values | **Reject** — reuse `formatCurrency` defaults |

Settled decisions carried forward: refunded rows use `amount - refundedAmount` only when `refundedQuantity > 0`; non-refunded rows use the original amount; fully refunded rows display `$ 0` while retaining the existing error badge; the header preserves the case-insensitive `Reembolsado: $Y` fragment.

## Data Flow

    API purchases payload → AdminPurchases render derivations → formatCurrency → row/header display
             │                         │
             └── data.totalRefunded ───┴── Y; X = Σ amount; Z = X − Y

Before rendering the table, calculate `totalAmount` with a frontend reduction over `data.purchases`, assign `refundedTotal` from `data.totalRefunded`, and calculate `netAmount = totalAmount - refundedTotal`. Render exactly `Total: $X · Reembolsado: $Y · Neto: $Z`; `Y` is not recomputed or normalized independently. For each row, pass the conditional derived value to `formatCurrency`, without assigning to or otherwise mutating `purchase.amount`. Leave dialog lines 51–52 and the refund payload unchanged.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/pages/AdminPurchases.jsx` | Modify | Add summary reductions and conditional net Monto rendering; preserve existing badges and dialog behavior. |
| `frontend/src/pages/AdminPurchases.test.jsx` | Modify | Add focused assertions for partial, full, and non-refunded row amounts plus Total/Reembolsado/Neto values; retain existing refund-flow assertions. |

## Interfaces / Contracts

No new interface or API contract. Existing payload fields remain authoritative:

```js
const displayedAmount = purchase.refundedQuantity > 0
  ? purchase.amount - purchase.refundedAmount
  : purchase.amount
const totalAmount = data.purchases.reduce((sum, purchase) => sum + purchase.amount, 0)
const netAmount = totalAmount - data.totalRefunded
```

All values use `formatCurrency` with its default zero fraction digits, preserving the project’s `$ 150` style.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit/component | Partial row shows `$ 150` from `$ 200 − $ 50`; full `res-2` shows `$ 0`; non-refunded `res-1` remains `$ 200`. | React Testing Library row-scoped assertions; use a focused partial fixture and existing mock rows. |
| Unit/component | Header shows correct Total, preserved `Reembolsado` values (`150` and `350` in existing fixtures), and Neto. | Assert case-insensitive `/reembolsado: \$ 150/i` and `/reembolsado: \$ 350/i`, plus Total/Neto text. |
| Regression | Dialog still derives from the original purchase amount and posts `{ quantity, amount }`. | Keep the existing dialog and mutation tests unchanged. |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

No migration required. This is a frontend-only display update; reverting the frontend change restores the previous presentation.

## Open Questions

- None.
