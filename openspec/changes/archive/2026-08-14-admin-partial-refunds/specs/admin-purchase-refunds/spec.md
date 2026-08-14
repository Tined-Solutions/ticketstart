# Delta for Admin Purchase Refunds

> Change: `admin-partial-refunds`. Modifies the canonical spec `openspec/specs/admin-purchase-refunds/spec.md` (archived 2026-08-10-admin-event-refunds). Strict TDD is active: every scenario maps to a backend xUnit test or frontend Vitest test planned in the tasks phase.

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: APR-002: List event purchases

The system MUST provide `GET /api/admin/events/{eventId}/purchases` listing confirmed Reservations with their Approved Transactions: raw buyer email/DNI, ticket type, quantity, amount, date, status, and refunded flag, plus a per-event `totalRefunded` equal to Σ `Refunds.Amount`. Events with no confirmed purchases SHALL return an empty list.
(Previously: `Refunded` was a plain boolean; `totalRefunded` summed Refunded transaction amounts.)

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
(Previously: binary refund — marked ALL tickets refunded and always flipped the transaction.)

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

### Requirement: APR-010: Admin UI

`AdminPanel` MUST provide a "Compras" action per event navigating to `/admin/events/:id/purchases`, guarded by `ProtectedRoute` + `RoleGuard` (Admin). The page MUST list purchases with per-purchase rows showing "X de Y reembolsadas" (error badge when fully refunded, warning when partial), per-event `totalRefunded`, and a "Reembolsar" confirm dialog with a quantity selector (1..active remaining) and live amount preview. The mutation MUST post `{ quantity }`; on success it MUST invalidate the purchases query; on failure it MUST show the error without mutating state; the refund button MUST be disabled when the purchase is fully refunded.
(Previously: binary refund — no quantity selector, plain refunded badge, button disabled on `refunded`.)

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
(Previously: tests asserted full-refund semantics only; frontend mock used a boolean `refunded`.)

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` and `npx vitest run` run
- THEN the replaced and new tests pass and unrelated tests are unaffected
