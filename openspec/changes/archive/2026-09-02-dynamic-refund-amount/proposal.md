# Proposal: Dynamic Refund Amount (Admin-Defined Refund Amount)

## Intent

The admin refund flow forces `Refunds.Amount = TicketType.Price × K`. Admins need partial-amount refunds (goodwill, service-fee adjustments) while still selecting which K tickets become unusable. Model A: `POST /admin/events/{eventId}/purchases/{reservationId}/refund` gains one admin-defined decimal `amount` with `0 < amount ≤ TicketType.Price × quantity`; percent input is frontend sugar only. Refund stays ledger-only.

**Why now**: product owner agreement; ledger-only refunds require no external coordination (no MP, no migration — `Refund.Amount` decimal(18,2) suffices).

## Scope

### In Scope
- Backend: `RefundPurchaseRequest` gains positional `decimal Amount`; `RefundPurchaseAsync` signature gains `amount`; ledger stores it verbatim.
- Service validation (InvalidOperationException → 409): amount ≤ 0, amount > `TicketType.Price × K`, >2 decimals (reject, never round). Quantity guard fires first.
- Audit detail string gains the amount; APR-008 no-motivo invariant preserved.
- Frontend: amount input (`step 0.01`) prefilled to `K × unitPrice` (untouched dialog = today's behavior); percent→amount via integer-cents math; mutation posts `{ quantity, amount }`.
- Tests: mechanical updates (~12 service + ~8 DTO sites), new guard/parity/cumulative tests, FsCheck property suite, Vitest dialog tests.
- Spec deltas: APR-003, APR-010, APR-011, APR-012 + Purpose paragraph.

### Out of Scope
- No Mercado Pago money movement (ledger-only, APR-008/015 unchanged).
- No DB migration; "custom refund" is derived (`Amount ≠ Price × K`), never stored.
- No per-ticket partial amounts; no per-ticket UI selection (quantity-based).
- No organizer-facing refund flow — Admin-only.
- D7 cap-basis quirk (Price mutation diverges cap from historical `tx.Amount`) documented, not fixed.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `admin-purchase-refunds`: body gains `amount` + new guards (APR-003); dialog + post body (APR-010); test coverage list (APR-011); Amount semantics "admin-defined ≤ unit price × K" (APR-012); Purpose mention.

## Approach

DTO stays annotation-free (repo convention); all amount validation lives in `AdminPurchaseService.RefundPurchaseAsync` inside the transaction, checked against the locked reservation's TicketType (race-safe). Controller passes amount through and appends it to the audit string (≤1000 chars). Frontend owns percent sugar, prefill, and inline UX errors. `GetPurchasesAsync` (Σ `Refunds.Amount`) needs no change — math already survives custom amounts.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Controllers/AdminController.cs` | Modified | DTO + action + audit string |
| `backend/Services/AdminPurchaseService.cs` | Modified | Amount guards; ledger `Amount = amount` |
| `backend/Services/IAdminPurchaseService.cs` | Modified | Signature + doc comments |
| `backend/Models/Refund.cs` | Modified | Doc comment only |
| `frontend/src/pages/AdminPurchases.jsx` | Modified | Amount input + percent helper |
| `frontend/src/lib/format.js` | Modified | Possible 2-decimal preview variant |
| backend/frontend tests | Modified | Per exploration inventory |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Float drift in percent→amount | Med | Integer-cents client math + backend 2-decimal rejection |
| `formatCurrency` hides cents while input accepts them | Med | Decide cent-visible preview in design |
| Wide mechanical signature change | Low | Compiler catches all ~20 sites |
| Breaking body shape for scripted callers | Low | SPA is only known consumer |
| Audit string change breaks no-motivo test | Low | Keep assertion; update details text |

## Rollback Plan

Single PR; revert restores strict `Price × K` ledger amounts. No schema to roll back. Ledger rows written with custom amounts remain valid historical records (Σ semantics unchanged).

## Dependencies

None — no migration, no external service, no new packages.

## Success Criteria

- [ ] Amount == full price behaves identically to today (parity test green)
- [ ] Custom amount stored verbatim; guards reject ≤0, >cap, >2 decimals with 409
- [ ] Cumulative custom refunds never exceed total paid (FsCheck property)
- [ ] `dotnet test` + `npm test` green; no-motivo audit invariant holds

## Open Design Questions (carried to design phase)

- Percent helper: typed input vs 25/50/100% quick buttons (or both).
- Cent display in previews (`formatCurrency` is whole-pesos ARS).
- Reject-vs-round for >2 decimals — decided: reject.
