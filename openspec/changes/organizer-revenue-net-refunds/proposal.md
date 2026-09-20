# Proposal: Organizer Revenue Net of Registered Refunds

**Change name:** `organizer-revenue-net-refunds`

> Direct implementation (not a full SDD pipeline). This folder documents the change and its rationale. Canonical spec sync into `openspec/specs/` follows the usual archive step.

## Intent

Make the organizer dashboard **"Ingresos"** show the same value as the admin purchases page **"Neto"**: the money retained after every refund recorded by an admin. Today a percentage refund voids tickets and removes their full list price from organizer revenue, even when only a fraction of that money was actually returned.

## Context and rationale

| Concept | Current formula | Where |
|---------|-----------------|-------|
| Admin **Neto** (per event) | Σ `Transaction.Amount` (Approved ∪ Refunded, Confirmed reservations) − Σ `Refunds.Amount` | `AdminPurchaseService.GetPurchasesAsync` + `AdminPurchases.jsx` |
| Organizer **Ingresos** (today) | (quantity − refunded tickets) × `TicketType.Price` | `MetricsService.GetOrganizerMetricsAsync` / `CalculateMetricsAsync` |

Both coincide when every refund equals the full unit price of the voided tickets (`A = Price × K`). They diverge when an admin records a refund **below the cap** (the percentage-refund case allowed by APR-003). Example:

- Purchase 1 × $100; admin refunds $50 (50%) and the ticket is voided.
- Admin Neto = $50 retained. Organizer "Ingresos" = **$0** today, **$50** after this change.
- 3 × $100 with one ticket refunded at $50: Admin Neto $250 vs organizer $200 today.

Revenue becomes **money-based** and matches the admin Neto by construction. `TicketsSold` / `TicketsScanned` stay ticket-based (a voided ticket still stops counting as sold, APR-005): the dashboard may show fewer sold tickets than the retained money implies — that is the honest split between attendance and money.

**Merchant processor fees (Mercado Pago) are explicitly out of scope.** This is a net-of-refunds figure, not a net-of-commissions figure. A fee layer can be added later on top of this base.

## Scope

### In scope
- `MetricsService.GetOrganizerMetricsAsync`: revenue = Σ charged − Σ refunded, per event.
- `MetricsService.CalculateMetricsAsync` (single-event endpoint): same money-based semantics, kept coherent even though the frontend does not consume it today.
- Tests: `MetricsConsolidationTests` and `MetricsPropertyTests` updated/added.

### Out of scope (Non-Goals)
- No MP/processor fee capture or subtraction.
- No refund flow, refund dialog, `AdminPurchases` UI or `GetPurchasesAsync` changes.
- No `Transaction`/`Refund` model changes, no migrations.
- No frontend changes (`totalRevenue` field name and payload shape stay identical).
- No change to sold/scanned/availability semantics.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `backend/Services/MetricsService.cs` | Modified | Money-based revenue (charged − refunded) in both metric paths. |
| `backend/Tests/MetricsConsolidationTests.cs` | Modified | Updated refund-revenue expectations + new scenarios. |
| `backend/Tests/MetricsPropertyTests.cs` | Modified | Property 35 and refunded-property redefined; revenue invariants. |
| `openspec/changes/organizer-revenue-net-refunds/` | New | This proposal, tasks and spec delta. |

## Approach

For each event: `TotalRevenue = Σ Transaction.Amount where Status ∈ {Approved, Refunded} and Reservation is Confirmed − Σ Refunds.Amount for those reservations`. Uses the exact filters of `GetPurchasesAsync` so the values match. Two event-scoped aggregate queries (charged, refunded) in the organizer path; two sums in the single-event path. Invariant: `Σ refunds ≤ Σ caps ≤ charged`, so revenue is always ≥ 0.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Partial-amount refunds change a number organizers already saw | Low (only affected case; full-price refunds unchanged) | Documented delta; dashboard label unchanged |
| Existing tests assert "refunded excluded from revenue" | Certain (by design) | Update to new semantics with explicit scenarios |
| Formula drift between metrics and admin purchases | Low | APR-017 pins the formula and parity; parity test |
| Refunds made only in the MP panel remain invisible | Known/accepted | Same on both sides (parity holds); out of scope |

## Rollback Plan

Revert the `MetricsService` commit; arithmetic returns to list-price-based revenue. No data, migration or API rollback required.

## Success Criteria

- [ ] 1 × $100 with a $50 refund → organizer revenue $50 (was $0), sold 0.
- [ ] 3 × $100 with one full-price refund → revenue $200 (unchanged), sold 2.
- [ ] Full-price refunds produce the same values as before.
- [ ] Organizer revenue equals admin Neto for the same event fixture.
- [ ] `dotnet test` fully green.
