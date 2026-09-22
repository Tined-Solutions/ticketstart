# Delta for admin-purchase-refunds

Change: `organizer-revenue-net-refunds`. All requirements other than APR-005 are UNCHANGED; this delta redefines the revenue side of APR-005 and adds APR-017 (money-based organizer revenue, parity with the admin purchases Neto).

## MODIFIED Requirements

### Requirement: APR-005: Refunded tickets stop counting as sold

Refunded tickets MUST NOT count as sold in ANY availability computation: `EventService.ComputeAvailabilityAggregatesAsync`, `ReservationService.CreateReservationTransactionalAsync`, and the `TicketsSold` figure of `MetricsService.CalculateMetricsAsync` and `MetricsService.GetOrganizerMetricsAsync`. Refunded tickets MUST also be excluded from resend (`ResendTicketsByEmailAsync`) and active-ticket lookups. Revenue is NO LONGER derived from ticket state: it is money-based per APR-017.

#### Scenario: Availability and sold counts exclude refunded

- GIVEN an event with sold tickets and one refunded purchase
- WHEN availability aggregates or metrics are computed
- THEN the refunded tickets are excluded from sold counts
- AND revenue is computed per APR-017 (charged amounts minus recorded refunds)

#### Scenario: Resend excludes refunded

- GIVEN a resend request for a buyer whose purchase was refunded
- WHEN `ResendTicketsByEmailAsync` runs
- THEN refunded tickets are not re-sent

## ADDED Requirements

### Requirement: APR-017: Organizer revenue equals retained money

Organizer-facing revenue (`MetricsService.GetOrganizerMetricsAsync` and `MetricsService.CalculateMetricsAsync`) MUST be computed money-based, per event, as:

`Σ Transaction.Amount where Transaction.Status ∈ {Approved, Refunded} AND the linked Reservation is Confirmed − Σ Refunds.Amount for those reservations`

It MUST equal the admin purchases page "Neto" (`Σ purchase.amount − totalRefunded`, APR-016) for the same event. Revenue MUST NOT be derived from `TicketType.Price` of non-refunded tickets. `TicketsSold` and `TicketsScanned` remain ticket-based (APR-005). `EventMetrics.TotalRevenue` keeps its name and payload shape; only the value changes. Merchant-processor fees are NOT deducted by this requirement.

#### Scenario: Percentage refund retains proportional money

- GIVEN one confirmed purchase of 1 ticket at 100 with a recorded refund of 50 (voided ticket)
- WHEN organizer metrics are computed
- THEN `totalRevenue` is 50 and `ticketsSold` is 0
- AND the admin purchases Neto for the same event is 50

#### Scenario: Partial-quantity full-price refund matches remaining tickets

- GIVEN one confirmed purchase of 3 tickets at 100 with one ticket refunded at 100
- WHEN organizer metrics are computed
- THEN `totalRevenue` is 200 and `ticketsSold` is 2

#### Scenario: Full refund yields zero

- GIVEN one confirmed purchase of 2 tickets at 100 refunded in full (200)
- WHEN organizer metrics are computed
- THEN `totalRevenue` is 0 and `ticketsSold` is 0
- AND the flipped `Refunded` transaction is counted exactly once (no double subtraction)

#### Scenario: No refunds equals charged amounts

- GIVEN confirmed purchases without refunds
- WHEN organizer metrics are computed
- THEN `totalRevenue` equals the sum of their transaction amounts

#### Scenario: Per-event isolation

- GIVEN two events with different refund histories owned by the same organizer
- WHEN organizer metrics are computed
- THEN each event's `totalRevenue` reflects only its own charged/refunded amounts

#### Scenario: Revenue is never negative

- GIVEN arbitrary valid refunds (0 < amount ≤ unit price × K per operation)
- WHEN organizer metrics are computed
- THEN `totalRevenue` is ≥ 0

## ADDED Non-Goals

This change MUST NOT introduce MP/processor fee deduction; MUST NOT alter refund flow, refund dialog, `AdminPurchases` UI, `GetPurchasesAsync`, `Transaction`/`Refund` models or migrations; MUST NOT change `EventMetrics` payload shape; MUST NOT change sold/scanned/availability semantics.
