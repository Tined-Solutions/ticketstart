# Proposal: Net Amount in Admin Purchases

## Intent

After a partial refund, the admin purchases table continues to show the original purchase amount, obscuring the amount still retained. Make the table communicate both event-level net revenue and each refunded row's remaining amount without changing refund or API semantics.

## Scope

### In Scope
- Update `AdminPurchases.jsx` Monto cells: when `refundedQuantity > 0`, render `purchase.amount - purchase.refundedAmount`; otherwise render the original `purchase.amount`.
- Replace the header refund line with `Total: $X · Reembolsado: $Y · Neto: $Z`, using `Σ purchase.amount`, `data.totalRefunded` verbatim, and `Total − Reembolsado` respectively.
- Add focused Vitest coverage for net-only row rendering, the header totals, and preservation of existing refund/dialog behavior.
- Fully refunded rows render Monto `$ 0`; this is acceptable and is mitigated by the existing refund badge.

### Out of Scope
- No backend, API, database, or canonical-spec change.
- No OrganizerDashboard/MetricsService APR-005 revenue-asymmetry work.
- No per-row amount breakdown variant and no optional per-purchase badge enrichment.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `admin-purchase-refunds`: clarify admin-table display of net row amounts and the event summary after refunds. The canonical spec remains unchanged during this change.

## Approach

Make two display-only changes in `frontend/src/pages/AdminPurchases.jsx`, reusing existing `refundedQuantity`, `refundedAmount`, `totalRefunded`, and `formatCurrency` data. Preserve the exact case-insensitive `Reembolsado: $Y` fragment because existing tests match `/reembolsado: \$ 150/i` and `/reembolsado: \$ 350/i`. **HARD CONSTRAINT:** never change `purchase.amount`; the refund dialog derives `unitPriceCents` and `capCents` from it at lines 51–52.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/pages/AdminPurchases.jsx` | Modified | Net Monto rendering and full header summary. |
| `frontend/src/pages/AdminPurchases.test.jsx` | Modified | Assertions for row net amount and summary totals. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Existing header regexes break | Low | Preserve `Reembolsado: $Y` verbatim. |
| `$ 0` could look ambiguous for fully refunded rows | Low | Keep the existing refund badge visible. |
| Accidental refund-cap regression | Low | Leave dialog lines 51–52 and API payload untouched. |

## Rollback Plan

Revert the frontend commit; this restores original row and header display. No backend, API, or data rollback is required.

## Dependencies

None; the required refund fields already exist in the purchases payload.

## Success Criteria

- [ ] Refunded rows show net amount and non-refunded rows retain original amount.
- [ ] Header shows Total, the preserved Reembolsado fragment, and Neto with correct values.
- [ ] Existing refund/dialog tests and focused frontend tests pass without API changes.
