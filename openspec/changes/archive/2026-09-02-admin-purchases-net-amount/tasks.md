# Tasks: Net Amount in Admin Purchases

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~120 (AdminPurchases.jsx ~25, AdminPurchases.test.jsx ~95) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR; 1 work unit = 1 commit |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Work Units

| Unit | Goal | Focused test command | Runtime harness | Rollback boundary |
|------|------|----------------------|-----------------|-------------------|
| WU1 | Net Monto cell + Total/Reembolsado/Neto header (RED→GREEN, tests first) | `npx vitest run src/pages/AdminPurchases.test.jsx` | N/A — jsdom Vitest suite | Revert the single frontend commit; no API/DB rollback |

## Phase 1: RED — net-amount tests first (WU1)

- [x] 1.1 `frontend/src/pages/AdminPurchases.test.jsx`: add a test seeding a partial-refund purchase (amount 200, refundedQuantity 1, refundedAmount 50) asserting row Monto `$ 150` + warning badge; assert fully refunded `res-2` shows `$ 0` + error badge; assert non-refunded `res-1` keeps `$ 200`. (APR-016 scenarios 1–3)
- [x] 1.2 `frontend/src/pages/AdminPurchases.test.jsx`: assert header `Total: $ 350 · Reembolsado: $ 150 · Neto: $ 200` on the existing fixture, keeping `/reembolsado: \$ 150/i`; assert `/reembolsado: \$ 350/i` still passes after the refund-flow refetch (`totalRefunded` 350). (APR-016 scenarios 4–5)
- [x] 1.3 Verify RED: `cd frontend && npx vitest run src/pages/AdminPurchases.test.jsx` — new assertions fail; existing dialog/refund tests still pass.

## Phase 2: GREEN — net rendering (WU1)

- [x] 2.1 `frontend/src/pages/AdminPurchases.jsx`: compute `totalAmount = data.purchases.reduce((s, p) => s + p.amount, 0)` and `netAmount = totalAmount - data.totalRefunded`; render header `Total: {formatCurrency(totalAmount)} · Reembolsado: {formatCurrency(data.totalRefunded)} · Neto: {formatCurrency(netAmount)}`, preserving the case-insensitive `Reembolsado: $Y` fragment. (APR-016)
- [x] 2.2 `frontend/src/pages/AdminPurchases.jsx`: Monto cell renders `formatCurrency(purchase.refundedQuantity > 0 ? purchase.amount - purchase.refundedAmount : purchase.amount)`; do NOT mutate `purchase.amount`; leave dialog `unitPriceCents`/`capCents` (lines 51–52) and the `{ quantity, amount }` payload untouched. (APR-016 non-goals)
- [x] 2.3 Verify: `cd frontend && npx vitest run src/pages/AdminPurchases.test.jsx` green, then full `npm test` — zero new failures.
- [x] 2.4 Commit (Spanish, conventional): `feat(frontend): columna Monto neta y resumen Total/Reembolsado/Neto en compras del admin`.

## Phase 3: Final verification

- [x] 3.1 `cd frontend && npm test` — full suite green; any failing test verified pre-existing at base before this change.