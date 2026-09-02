# Delta for admin-purchase-refunds

Change: admin-purchases-net-amount. All other requirements (APR-001…APR-015) and the existing Non-Goals paragraph are UNCHANGED; this delta adds display-only net-amount behavior to the admin purchases page.

## MODIFIED Purpose

Admins MUST be able to list an event's confirmed purchases and refund K of N tickets of a purchase — partially or fully, cumulatively, for an admin-defined decimal amount (0 < amount ≤ unit price × K), recording each operation in a `Refunds` ledger. Refunding MUST mark the refunded tickets as refunded (not deleted), flip the Approved Transaction to `Refunded` only when an operation leaves zero active tickets, exclude refunded tickets from every sold-count computation, block refunded QRs at scan, and write an audit entry — without moving money via Mercado Pago, sending email, or recording a motivo. The admin purchases page MUST display each refunded row's net amount (`amount − refundedAmount`) and a `Total · Reembolsado · Neto` event summary so retained revenue stays visible after partial refunds.
(Previously: the purpose did not cover net row amounts or the Total/Reembolsado/Neto summary.)

## ADDED Requirements

### Requirement: APR-016: Net amount display in admin purchases

The admin purchases page (APR-010) MUST render each row's Monto cell as `purchase.amount − purchase.refundedAmount` when `refundedQuantity > 0`, and as the original `purchase.amount` otherwise. The page MUST NOT mutate `purchase.amount`, because the refund dialog derives `unitPriceCents`/`capCents` from it. The header MUST render `Total: $X · Reembolsado: $Y · Neto: $Z`, where X = Σ `purchase.amount`, Y = `data.totalRefunded` rendered verbatim (equal to Σ `Refunds.Amount`, per APR-002/APR-012), and Z = X − Y; the case-insensitive `Reembolsado: $Y` fragment MUST remain intact. A fully refunded row (`refundedQuantity >= quantity`) MUST render Monto `$ 0` with the existing error badge still visible.

#### Scenario: Partially refunded row shows net amount

- GIVEN a purchase with `amount` 200, `refundedQuantity` 2, and `refundedAmount` 50
- WHEN the page renders the row's Monto cell
- THEN it shows `$ 150` (amount − refundedAmount)
- AND the partial-refund badge remains visible

#### Scenario: Fully refunded row shows zero

- GIVEN a purchase where `refundedQuantity >= quantity` and `refundedAmount` equals `amount`
- WHEN the page renders the row's Monto cell
- THEN it shows `$ 0`
- AND the error-variant refund badge remains visible

#### Scenario: Non-refunded row keeps original amount

- GIVEN a purchase with `refundedQuantity` 0
- WHEN the page renders the row's Monto cell
- THEN it shows the original `purchase.amount`

#### Scenario: Header summary shows Total, Reembolsado, and Neto

- GIVEN an event whose purchases sum to 500 and whose `data.totalRefunded` is 150
- WHEN the page renders the header
- THEN it shows Total 500, Reembolsado 150 matching the existing `/reembolsado: \$ 150/i` assertion, and Neto 350

#### Scenario: Header Reembolsado equals Σ Refunds.Amount

- GIVEN `data.totalRefunded` from the purchases payload (APR-002)
- WHEN the page renders the header
- THEN the `Reembolsado: $Y` value equals Σ `Refunds.Amount` and is rendered verbatim

## ADDED Non-Goals

This change MUST NOT alter backend, API, database, or refund-dialog semantics (the `{ quantity, amount }` payload and the `unitPriceCents`/`capCents` derivation stay untouched); MUST NOT mutate `purchase.amount`; MUST NOT add a per-row amount-breakdown variant or optional per-purchase badge enrichment; and MUST NOT touch OrganizerDashboard/MetricsService revenue-asymmetry work.