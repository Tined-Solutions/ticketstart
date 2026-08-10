# Admin Purchase Refunds Specification

## Purpose

Admins MUST be able to list an event's confirmed purchases and mark an unused full purchase as refunded. Refunding MUST mark the purchase's tickets as refunded (not deleted), flip the Approved Transaction to Refunded, exclude refunded tickets from every sold-count computation, block refunded QRs at scan, and write an audit entry — without moving money via Mercado Pago, sending email, or recording a motivo.

## Non-Goals

Partial/per-ticket refund (modeled at ticket level, not built), Mercado Pago money movement or any external refund call, refund email/buyer notification, motivo/refund-reason field, organizer-facing refund view, and editing or reverting a refund once applied.

## Requirements

### Requirement: APR-001: Admin-only authorization

Both purchase endpoints MUST enforce the `RequireAdminRole` policy.

#### Scenario: Non-admin rejected

- GIVEN a non-admin user (e.g., organizer)
- WHEN they call either purchase endpoint
- THEN the system returns 403

### Requirement: APR-002: List event purchases

The system MUST provide `GET /api/admin/events/{eventId}/purchases` listing confirmed Reservations with their Approved Transactions: raw buyer email/DNI (Admin-only surface — the admin must identify the buyer when refunding), ticket type, quantity, amount, date, status, and refunded flag, plus a per-event `totalRefunded`. Events with no confirmed purchases SHALL return an empty list.

#### Scenario: Happy path listing

- GIVEN an event with confirmed purchases, one of them refunded
- WHEN an admin requests the listing
- THEN each purchase shows raw buyer email/DNI, ticket type, quantity, amount, date, status, and refunded flag
- AND `totalRefunded` sums refunded purchase amounts

#### Scenario: Event not found

- GIVEN no event exists for the eventId
- WHEN an admin requests the listing
- THEN the system returns 404

### Requirement: APR-003: Atomic full-purchase refund

The system MUST provide `POST /api/admin/events/{eventId}/purchases/{reservationId}/refund`. It MUST, in one atomic transaction: mark all tickets of the reservation refunded (`IsRefunded`/`RefundedAt`, never deleted) and flip the Approved Transaction to `Refunded` (updating the existing row — never inserting a second row — preserving the unique `MercadoPagoId` index).

#### Scenario: Happy path refund

- GIVEN a confirmed reservation with an Approved transaction and unused tickets
- WHEN an admin posts the refund
- THEN all tickets are marked refunded and the transaction status becomes Refunded
- AND exactly one transaction row remains for that MercadoPagoId

#### Scenario: No approved transaction

- GIVEN a reservation with no Approved transaction
- WHEN an admin posts the refund
- THEN the refund fails with no state change

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

`AdminPanel` MUST provide a "Compras" action per event navigating to `/admin/events/:id/purchases`, guarded by `ProtectedRoute` + `RoleGuard` (Admin). The page MUST list purchases with per-purchase rows, per-event `totalRefunded`, and a "Reembolsar" confirm dialog; on success it MUST invalidate the purchases query; on failure it MUST show the error without mutating state.

#### Scenario: Non-admin blocked from route

- GIVEN a non-admin user
- WHEN they open the purchases route
- THEN they are redirected or denied access

#### Scenario: Refund failure shows error

- GIVEN the backend returns an error (e.g., used ticket)
- WHEN the admin confirms the refund
- THEN the page shows the error and the list is unchanged

### Requirement: APR-011: Test coverage

Backend MUST follow strict TDD: controller tests (403/404, audit), service tests for the atomic refund, transaction flip (single row), race protection, and the four sold-count sites. Frontend SHOULD get Vitest coverage where feasible.

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` runs
- THEN new tests pass and existing tests are unaffected
