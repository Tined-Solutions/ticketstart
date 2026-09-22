# Tasks: Organizer Revenue Net of Registered Refunds

Direct implementation plan (no SDD phase pipeline). Requirement: APR-017; APR-005 revenue clause modified.

## Phase 0: Documentation (done)

- [x] 0.1 Proposal with intent/rationale/scope: `proposal.md`.
- [x] 0.2 Spec delta (`specs/admin-purchase-refunds/spec.md`): APR-005 modified + APR-017 added.

## Phase 1: RED — tests first

- [x] 1.1 Update `backend/Tests/MetricsConsolidationTests.cs`:
  - Rework `GetOrganizerMetricsAsync_RefundedTickets_ExcludedFromSoldAndRevenue`: sold exclusion stays; revenue asserts become money-based.
  - Add: percentage refund (1×100 refunded 50 → revenue 50, sold 0); partial-quantity full price (3×100, one refunded 100 → revenue 200, sold 2); full refund (2×100 refunded 200 → 0/0); no-refund baseline; two-event isolation.
  - Add parity test: same fixture through `MetricsService` and `AdminPurchaseService.GetPurchasesAsync` → organizer revenue == Σ `purchase.amount` − `TotalRefunded`.
  - (Extra) Re-seeded `GetOrganizerMetricsAsync_ReturnsCorrectAggregatesForAllEvents` with a Confirmed reservation + Approved transaction so its 250 revenue assert stays valid under APR-017.
- [x] 1.2 Update `backend/Tests/MetricsPropertyTests.cs`:
  - Property 35 (`GetEventMetrics_TotalRevenue_MatchesSumOfTicketPrices`): restricted to no-refund fixtures and redefined as charged amounts (`GetEventMetrics_TotalRevenue_MatchesSumOfChargedAmounts`).
  - Refunded property (~line 704): sold still excludes; revenue == charged − Σ refunds for arbitrary valid (K, amount); revenue ≥ 0.
  - [x] Verify: `cd backend && dotnet test --filter "FullyQualifiedName~Metrics"` (expect failures on new assertions).

## Phase 2: GREEN — implementation

- [x] 2.1 `backend/Services/MetricsService.cs` — `GetOrganizerMetricsAsync`: drop price-join revenue from ticket aggregates (keep sold/scanned); add charged aggregate (Transactions Approved|Refunded ⋈ Confirmed reservations of the event) and refunded aggregate (Refunds ⋈ Confirmed reservations); `TotalRevenue = charged − refunded` per event.
- [x] 2.2 `backend/Services/MetricsService.cs` — `CalculateMetricsAsync`: same money-based revenue for the single event (charged − refunded; Confirmed reservations only). Keep sold/scanned/inventory untouched.
- [x] 2.3 Comments reference APR-017; no API/DTO/migration/frontend changes.

## Phase 3: Verify

- [x] 3.1 Focused: `cd backend && dotnet test --filter "FullyQualifiedName~Metrics"`.
- [x] 3.2 Full suite: `cd backend && dotnet test` — 800 passed, 4 pre-existing unrelated failures (`PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`, `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`); all 4 reproduce on the clean tree without this change.
- [x] 3.3 Spot-check parity: organizer revenue vs admin Neto on the parity fixture; revenue ≥ 0 invariant holds.

## Acceptance

APR-017 scenarios pass; full-price refund behavior is unchanged; no out-of-scope files touched; no commits made.
