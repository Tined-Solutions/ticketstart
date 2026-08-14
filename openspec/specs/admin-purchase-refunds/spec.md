# Admin Purchase Refunds Specification

## Purpose

Admins MUST be able to list an event's confirmed purchases and refund K of N tickets of a purchase — partially or fully, cumulatively, recording each operation in a `Refunds` ledger. Refunding MUST mark the refunded tickets as refunded (not deleted), flip the Approved Transaction to `Refunded` only when an operation leaves zero active tickets, exclude refunded tickets from every sold-count computation, block refunded QRs at scan, and write an audit entry — without moving money via Mercado Pago, sending email, or recording a motivo.

## Non-Goals

Mercado Pago money movement or any external refund call, refund email/buyer notification, motivo/refund-reason field, per-ticket UI selection (selection by quantity only — tickets fungible), Reservation status change, changes to the auto-refund path (`PaymentService.InitiateRefundAsync`), organizer-facing refund view, and editing or reverting a refund operation once applied.

## Requirements

### Requirement: APR-001: Admin-only authorization

Both purchase endpoints MUST enforce the `RequireAdminRole` policy.

#### Scenario: Non-admin rejected

- GIVEN a non-admin user (e.g., organizer)
- WHEN they call either purchase endpoint
- THEN the system returns 403

### Requirement: APR-002: List event purchases

The system MUST provide `GET /api/admin/events/{eventId}/purchases` listing confirmed Reservations with their Approved Transactions: raw buyer email/DNI, ticket type, quantity, amount, date, status, and refunded flag, plus a per-event `totalRefunded` equal to Σ `Refunds.Amount`. Each row MUST expose `RefundedQuantity` and `RefundedAmount`; `Refunded` is derived (fully refunded). Events with no confirmed purchases SHALL return an empty list.

#### Scenario: Happy path listing

- GIVEN an event with confirmed purchases, one partially refunded and one fully refunded
- WHEN an admin requests the listing
- THEN each row shows raw buyer data, RefundedQuantity, RefundedAmount, and the derived refunded flag
- AND `totalRefunded` equals Σ Refunds.Amount across purchases

#### Scenario: Event not found

- GIVEN no event exists for the eventId
- WHEN an admin requests the listing
- THEN the system returns 404

### Requirement: APR-003: Atomic quantity-based refund

The system MUST provide `POST /api/admin/events/{eventId}/purchases/{reservationId}/refund` accepting body `{ "quantity": K }` (`RefundPurchaseRequest`, validated K > 0). In one atomic transaction it MUST: require an Approved transaction; block when ANY ticket `IsUsed` (APR-004); block when K ≤ 0 or K > active non-refunded tickets; select the K oldest non-refunded/non-used tickets (APR-013); mark exactly those K tickets `IsRefunded`/`RefundedAt` (never deleted); insert one `Refunds` row (APR-012); and flip the Approved Transaction to `Refunded` ONLY when the operation leaves 0 active tickets — never inserting a second transaction row. Partial operations leave the transaction `Approved`. Controller semantics stay 200/404/409/500; audit runs after commit (APR-007) with no motivo (APR-008).

#### Scenario: Partial refund happy path

- GIVEN a confirmed reservation with 4 unused tickets and an Approved transaction
- WHEN an admin posts quantity=2
- THEN exactly 2 tickets are marked refunded, one Refunds row is inserted, and the transaction stays Approved

#### Scenario: Full refund flips transaction only at zero active

- GIVEN the same reservation with 2 active tickets remaining
- WHEN an admin posts quantity=2
- THEN all tickets are refunded, the transaction becomes Refunded, and exactly one row remains for that MercadoPagoId

#### Scenario: No approved transaction

- GIVEN a reservation with no Approved transaction
- WHEN an admin posts the refund
- THEN the refund fails with no state change

#### Scenario: Quantity above active remaining is blocked

- GIVEN a reservation with 2 active non-refunded tickets
- WHEN an admin posts quantity=3
- THEN the refund fails with no ticket, Refunds, or transaction change

#### Scenario: Quantity zero or negative is blocked

- GIVEN a confirmed reservation
- WHEN an admin posts quantity=0 or a negative value
- THEN the refund fails with no state change

#### Scenario: Concurrent partial refunds serialize

- GIVEN two concurrent partial refund requests for the same purchase
- WHEN both run under lock
- THEN each selects from the non-refunded tickets observed under lock and no ticket is refunded twice

### Requirement: APR-004: Refund blocked when a ticket is used

The refund MUST fail if any ticket of the reservation `IsUsed`. The check MUST run inside the transaction under row lock and be re-checked after locking (scan-vs-refund race protection).

#### Scenario: Used ticket blocks refund

- GIVEN a reservation where at least one ticket IsUsed
- WHEN an admin posts the refund
- THEN the refund fails and no ticket or transaction changes

#### Scenario: Concurrent scan wins the race

- GIVEN a staff scan validating a ticket while an admin posts the refund
- WHEN both run concurrently
- THEN the refund re-check observes IsUsed and rolls back without changes

### Requirement: APR-005: Refunded tickets stop counting as sold

Refunded tickets MUST NOT count as sold in ANY availability or revenue computation: `EventService.ComputeAvailabilityAggregatesAsync`, `ReservationService.CreateReservationTransactionalAsync`, `MetricsService.CalculateMetricsAsync`, and `MetricsService.GetOrganizerMetricsAsync`. Refunded tickets MUST also be excluded from resend (`ResendTicketsByEmailAsync`) and active-ticket lookups.

#### Scenario: Availability and metrics exclude refunded

- GIVEN an event with sold tickets and one refunded purchase
- WHEN availability aggregates or metrics are computed
- THEN the refunded tickets are excluded from sold counts and revenue

#### Scenario: Resend excludes refunded

- GIVEN a resend request for a buyer whose purchase was refunded
- WHEN `ResendTicketsByEmailAsync` runs
- THEN refunded tickets are not re-sent

### Requirement: APR-006: Refunded QR rejected at scan

`ValidateQRCodeAsync` MUST return `IsValid=false` with `Error="Entrada reembolsada"` and the ticket attached for a refunded ticket. `TicketValidationDetails` MUST expose `IsRefunded`/`RefundedAt` so StaffScan renders the message.

#### Scenario: Refunded ticket scanned

- GIVEN a staff user scans a refunded ticket's QR
- WHEN validation runs
- THEN validation returns invalid with "Entrada reembolsada" and the ticket attached

### Requirement: APR-007: Refund audit logging

A successful refund MUST write an AuditLog entry with new `AuditActionType.RefundPurchase` (varchar-stored, no migration) and `AuditResourceType.Payment`, details ≤ 1000 chars, and NO motivo field.

#### Scenario: Refund is audited

- GIVEN an admin refund succeeds
- WHEN the transaction commits
- THEN an audit entry with `RefundPurchase` is written with no motivo

### Requirement: APR-008: No money movement, email, or motivo

The admin refund MUST NOT call Mercado Pago or any external refund API, MUST NOT send a refund email, and MUST NOT accept or store a motivo. The existing auto-refund path (`PaymentService.InitiateRefundAsync`) MUST remain untouched.

#### Scenario: Refund has no external side effects

- GIVEN an admin refund succeeds
- WHEN it completes
- THEN no MP refund call and no refund email are issued

### Requirement: APR-009: Purchase-to-ticket linking

Tickets created after this change MUST carry their `ReservationId`. Legacy tickets MUST be linked best-effort by (EventId, TicketTypeId, PurchaserDNI, PurchaserEmail) ordered by CreatedAt, chunked by reservation quantity; unmatched tickets SHALL keep NULL `ReservationId`.

#### Scenario: New tickets linked precisely

- GIVEN a checkout after the migration
- WHEN `CreateTicketsAsync` creates tickets
- THEN every ticket is linked to its reservation

#### Scenario: Ambiguous legacy backfill

- GIVEN legacy tickets matching multiple candidate reservations
- WHEN backfill runs
- THEN ambiguous tickets stay NULL and the listing shows the purchase's tickets unverified

### Requirement: APR-010: Admin UI

`AdminPanel` MUST provide a "Compras" action per event navigating to `/admin/events/:id/purchases`, guarded by `ProtectedRoute` + `RoleGuard` (Admin). The page MUST list purchases with per-purchase rows showing "X de Y reembolsadas" (error badge when fully refunded, warning when partial), per-event `totalRefunded`, and a "Reembolsar" confirm dialog with a quantity selector (1..active remaining) and live amount preview. The mutation MUST post `{ quantity }`; on success it MUST invalidate the purchases query; on failure it MUST show the error without mutating state; the refund button MUST be disabled when the purchase is fully refunded.

#### Scenario: Non-admin blocked from route

- GIVEN a non-admin user
- WHEN they open the purchases route
- THEN they are redirected or denied access

#### Scenario: Partial refund via quantity selector

- GIVEN a purchase with active remaining tickets
- WHEN the admin selects quantity=K and confirms
- THEN the dialog previews K × unit price, the mutation posts `{ quantity: K }`, and the row updates to "K de N reembolsadas"

#### Scenario: Refund failure shows error

- GIVEN the backend returns an error (e.g., used ticket)
- WHEN the admin confirms the refund
- THEN the page shows the error and the list is unchanged

#### Scenario: Fully refunded row disables refund

- GIVEN a purchase where RefundedQuantity >= Quantity
- WHEN the page renders
- THEN the refund button is disabled and the badge uses the error variant

### Requirement: APR-011: Test coverage

Backend MUST follow strict TDD (Red→Green): replace the binary-refund tests (`AdminPurchaseServiceTests.cs`: RefundPurchaseAsync_HappyPath_MarksTicketsRefundedAndFlipsTransaction, RefundPurchaseAsync_AlreadyRefunded_ThrowsAndChangesNothing, GetPurchasesAsync_HappyPath_ReturnsRawBuyerDataAndFlagsRefunded, GetPurchasesAsync_TotalRefunded_SumOfRefundedTransactionAmounts; `AdminControllerPurchaseTests.cs`: 9-arg `AdminPurchaseRow` construction) and add tests for: partial happy path, cumulative second refund, quantity > active blocked, quantity ≤ 0 blocked, flip only at 0 active, scan race with partial state, Refunds row recorded (TicketIds/Amount), legacy backfill, and controller body validation. Frontend Vitest MUST update the mock shape (`refundedQuantity`, `refundedAmount`) and cover the quantity-selector post body and badge variants.

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` and `npx vitest run` run
- THEN the replaced and new tests pass and unrelated tests are unaffected

### Requirement: APR-012: Cumulative refund operation record

Each refund operation MUST insert exactly one `Refunds` row recording ReservationId, `TicketIds[]`, Quantity, Amount (= unit price × K), AdminId, and CreatedAt. Per-event `TotalRefunded` SHALL equal Σ `Refunds.Amount`. Each `AdminPurchaseRow` MUST expose `RefundedQuantity` (count of `IsRefunded` tickets) and `RefundedAmount` (Σ Refunds for the reservation); `Refunded` SHALL be derived as fully refunded (`RefundedQuantity >= Quantity`).

#### Scenario: Partial refund records one operation row

- GIVEN a confirmed reservation with 4 tickets and one Approved transaction
- WHEN an admin refunds K=2
- THEN one Refunds row is inserted with the 2 selected TicketIds, Quantity=2, Amount = 2 × unit price, and the admin id
- AND the row shows RefundedQuantity=2, RefundedAmount = 2 × unit price

#### Scenario: Cumulative second refund appends

- GIVEN the same purchase after K=2 was refunded
- WHEN the admin refunds another K=2
- THEN a second Refunds row is inserted and TotalRefunded = Σ both rows

### Requirement: APR-013: Deterministic ticket selection

The tickets marked refunded MUST be exactly the K oldest non-refunded, non-used tickets, selected under row lock so the choice is stable across concurrent operations.

#### Scenario: Oldest tickets refunded first

- GIVEN a purchase whose tickets have different CreatedAt values, none used or refunded
- WHEN an admin refunds K=2
- THEN the two tickets with the earliest CreatedAt are marked `IsRefunded`

### Requirement: APR-014: Legacy refund backfill

Migration `AddRefundsTable` MUST backfill one `Refunds` row per pre-existing Refunded Transaction using pure SQL (`INSERT…SELECT` with `array_agg` over that transaction's refunded tickets), with AdminId NULL. Backfilled rows SHALL count toward `TotalRefunded`; it MUST NOT regress for legacy refunds.

#### Scenario: Legacy refund keeps counting

- GIVEN a Refunded transaction created before this change with no Refunds rows
- WHEN the migration applies
- THEN one backfilled Refunds row exists (AdminId null) and TotalRefunded includes its Amount

### Requirement: APR-015: Non-goals as negative requirements

This change MUST NOT call Mercado Pago or any external refund API, MUST NOT accept or store a motivo, MUST NOT provide per-ticket UI selection (selection by quantity only), MUST NOT change Reservation status, MUST NOT alter `PaymentService.InitiateRefundAsync`, and MUST NOT allow editing or reverting a refund operation.

#### Scenario: Partial refund stays local and irreversible

- GIVEN an admin submits a partial refund
- WHEN it completes
- THEN no MP call, no motivo, no Reservation status change, and no refund-editing capability exist
