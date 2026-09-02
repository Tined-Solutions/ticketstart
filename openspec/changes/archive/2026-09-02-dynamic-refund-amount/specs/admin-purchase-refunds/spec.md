# Delta for admin-purchase-refunds

Change: dynamic-refund-amount. All other requirements (APR-001, APR-002, APR-004…APR-009, APR-013…APR-015) and the Non-Goals paragraph are UNCHANGED.

## MODIFIED Purpose

Admins MUST be able to list an event's confirmed purchases and refund K of N tickets of a purchase — partially or fully, cumulatively, for an admin-defined decimal amount (0 < amount ≤ unit price × K), recording each operation in a `Refunds` ledger. Refunding MUST mark the refunded tickets as refunded (not deleted), flip the Approved Transaction to `Refunded` only when an operation leaves zero active tickets, exclude refunded tickets from every sold-count computation, block refunded QRs at scan, and write an audit entry — without moving money via Mercado Pago, sending email, or recording a motivo.

(Previously: the ledger amount was always unit price × K; no admin-defined amount existed.)

## MODIFIED Requirements

### Requirement: APR-003: Atomic quantity-based refund

The system MUST provide `POST /api/admin/events/{eventId}/purchases/{reservationId}/refund` accepting body `{ "quantity": K, "amount": A }` (`RefundPurchaseRequest`, decimal A, K validated > 0). In one atomic transaction it MUST: require an Approved transaction; block when ANY ticket `IsUsed` (APR-004); block when K ≤ 0 or K > active non-refunded tickets; then block when A ≤ 0, A > `TicketType.Price × K`, or A carries more than 2 decimal places (rejected, never rounded) — quantity guards MUST fire before amount guards; select the K oldest non-refunded/non-used tickets (APR-013); mark exactly those K tickets `IsRefunded`/`RefundedAt` (never deleted); insert one `Refunds` row storing A verbatim (APR-012); and flip the Approved Transaction to `Refunded` ONLY when the operation leaves 0 active tickets — never inserting a second transaction row. Partial operations leave the transaction `Approved`. All amount validation MUST run inside the transaction against the locked reservation's TicketType and fail as 409 via `InvalidOperationException`, matching quantity-guard semantics. Controller semantics stay 200/404/409/500; audit runs after commit (APR-007) including the amount in the details, with no motivo (APR-008).
(Previously: body was `{ "quantity": K }` and the ledger amount was always `TicketType.Price × K`.)

#### Scenario: Partial refund happy path

- GIVEN a confirmed reservation with 4 unused tickets and an Approved transaction
- WHEN an admin posts quantity=2 with amount = 2 × unit price
- THEN exactly 2 tickets are marked refunded, one Refunds row stores that amount verbatim, and the transaction stays Approved

#### Scenario: Custom partial amount stored verbatim

- GIVEN a confirmed reservation with 4 unused tickets of unit price 100
- WHEN an admin posts quantity=2 with amount=50.5
- THEN the 2 oldest tickets are marked refunded and the Refunds row stores Amount=50.5 exactly, with the transaction staying Approved

#### Scenario: Full refund flips transaction only at zero active

- GIVEN the same reservation with 2 active tickets remaining
- WHEN an admin posts quantity=2 with a valid amount
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

#### Scenario: Quantity guard fires before amount guard

- GIVEN a reservation with 2 active non-refunded tickets
- WHEN an admin posts quantity=3 with a valid amount
- THEN the failure reports the quantity violation and no amount validation runs

#### Scenario: Amount above cap is blocked

- GIVEN a reservation with unit price 100 and 4 active tickets
- WHEN an admin posts quantity=2 with amount=200.01
- THEN the refund fails with 409 and no ticket, Refunds, or transaction change

#### Scenario: Amount zero or negative is blocked

- GIVEN a confirmed reservation
- WHEN an admin posts amount=0 or a negative amount
- THEN the refund fails with 409 and no state change

#### Scenario: More than two decimals rejected, not rounded

- GIVEN a reservation with unit price 100
- WHEN an admin posts quantity=1 with amount=33.333
- THEN the refund fails with 409, no Refunds row exists, and the amount is never rounded

#### Scenario: Concurrent partial refunds serialize

- GIVEN two concurrent partial refund requests for the same purchase
- WHEN both run under lock
- THEN each selects from the non-refunded tickets observed under lock and no ticket is refunded twice

### Requirement: APR-010: Admin UI

`AdminPanel` MUST provide a "Compras" action per event navigating to `/admin/events/:id/purchases`, guarded by `ProtectedRoute` + `RoleGuard` (Admin). The page MUST list purchases with per-purchase rows showing "X de Y reembolsadas" (error badge when fully refunded, warning when partial), per-event `totalRefunded`, and a "Reembolsar" confirm dialog with a quantity selector (1..active remaining), an amount input (`step 0.01`) prefilled to K × unit price for the selected quantity, and a live amount preview. A percent helper MUST convert client-side to the amount using integer-cents math (half-up); its exact form (typed input or quick buttons) is a design decision, and the backend MUST never receive a percent. The dialog MUST block submit with an inline error when amount ≤ 0 or amount > K × unit price. The mutation MUST post `{ quantity, amount }`; on success it MUST invalidate the purchases query; on failure it MUST show the error without mutating state; the refund button MUST be disabled when the purchase is fully refunded.
(Previously: the dialog had only a quantity selector, the preview was unit price × K, and the mutation posted `{ quantity }`.)

#### Scenario: Non-admin blocked from route

- GIVEN a non-admin user
- WHEN they open the purchases route
- THEN they are redirected or denied access

#### Scenario: Partial refund via quantity selector

- GIVEN a purchase with active remaining tickets
- WHEN the admin selects quantity=K and confirms with the prefilled amount
- THEN the mutation posts `{ quantity: K, amount: K × unit price }` and the row updates to "K de N reembolsadas"

#### Scenario: Amount input prefilled to K × unit price

- GIVEN a purchase with unit price 100
- WHEN the admin selects quantity=3 in the dialog
- THEN the amount input shows 300 (prefill recomputes with quantity) and confirming without edits posts amount=300

#### Scenario: Percent helper converts client-side

- GIVEN the dialog open with quantity=2 and unit price 100
- WHEN the admin applies the 25% helper
- THEN the amount input shows 50 via integer-cents math and the post body still carries `{ quantity, amount }`, never a percent

#### Scenario: Invalid amount blocks submit

- GIVEN the dialog open for any quantity
- WHEN the amount is 0, negative, or above K × unit price
- THEN an inline error is shown and no mutation is sent

#### Scenario: Refund failure shows error

- GIVEN the backend returns an error (e.g., used ticket)
- WHEN the admin confirms the refund
- THEN the page shows the error and the list is unchanged

#### Scenario: Fully refunded row disables refund

- GIVEN a purchase where RefundedQuantity >= Quantity
- WHEN the page renders
- THEN the refund button is disabled and the badge uses the error variant

### Requirement: APR-011: Test coverage

Backend MUST follow strict TDD (Red→Green): replace the binary-refund tests (`AdminPurchaseServiceTests.cs`: RefundPurchaseAsync_HappyPath_MarksTicketsRefundedAndFlipsTransaction, RefundPurchaseAsync_AlreadyRefunded_ThrowsAndChangesNothing, GetPurchasesAsync_HappyPath_ReturnsRawBuyerDataAndFlagsRefunded, GetPurchasesAsync_TotalRefunded_SumOfRefundedTransactionAmounts; `AdminControllerPurchaseTests.cs`: 9-arg `AdminPurchaseRow` construction), update the mechanical signatures (4-arg `RefundPurchaseAsync` call/mock sites, 2-arg `RefundPurchaseRequest` constructor sites), and add tests for: partial happy path, cumulative second refund, quantity > active blocked, quantity ≤ 0 blocked, flip only at 0 active, scan race with partial state, Refunds row recorded (TicketIds/Amount), legacy backfill, and controller body validation, PLUS: amount == full-price parity, custom amount stored verbatim, amount ≤ 0 blocked, amount > cap blocked, >2 decimals rejected as 409 (never rounded), quantity guard ordering before amount guard, and an FsCheck property suite proving for arbitrary valid (K, amount): ledger Amount == amount, exactly K tickets marked, Σ reservation refunds ≤ tx.Amount, and flip iff 0 active. Controller tests MUST cover the `{ quantity, amount }` body and the audit string carrying the amount with no motivo. Frontend Vitest MUST update the mock shape (`refundedQuantity`, `refundedAmount`) and cover the `{ quantity, amount }` post body, prefill = K × unit price, percent→amount conversion, invalid amount blocking submit, cap re-validation on quantity change, and badge variants.
(Previously: coverage listed only quantity-based behavior with a `{ quantity }` body.)

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` and `npx vitest run` run
- THEN the replaced and new tests pass and unrelated tests are unaffected

### Requirement: APR-012: Cumulative refund operation record

Each refund operation MUST insert exactly one `Refunds` row recording ReservationId, `TicketIds[]`, Quantity, Amount (the admin-defined amount stored verbatim, 0 < amount ≤ unit price × K — not necessarily unit price × K), AdminId, and CreatedAt. Per-event `TotalRefunded` SHALL equal Σ `Refunds.Amount`. Each `AdminPurchaseRow` MUST expose `RefundedQuantity` (count of `IsRefunded` tickets) and `RefundedAmount` (Σ Refunds for the reservation); `Refunded` SHALL be derived as fully refunded (`RefundedQuantity >= Quantity`). A "custom refund" is derived contextually (`Amount ≠ unit price × K`) and MUST NOT be stored.
(Previously: Amount was defined as unit price × K.)

#### Scenario: Partial refund records one operation row

- GIVEN a confirmed reservation with 4 tickets, unit price 100, and one Approved transaction
- WHEN an admin refunds K=2 with amount=150
- THEN one Refunds row is inserted with the 2 selected TicketIds, Quantity=2, Amount=150 verbatim, and the admin id
- AND the row shows RefundedQuantity=2, RefundedAmount=150

#### Scenario: Full-price amount preserves today's ledger semantics

- GIVEN the same reservation
- WHEN an admin refunds K=2 with amount = 2 × unit price
- THEN the Refunds row stores Amount = 2 × unit price, identical to pre-change behavior

#### Scenario: Cumulative second refund appends

- GIVEN the same purchase after K=2 was refunded
- WHEN the admin refunds another K=2
- THEN a second Refunds row is inserted and TotalRefunded = Σ both rows

#### Scenario: Cumulative custom amounts never exceed total paid

- GIVEN a purchase whose total paid equals the Approved transaction amount
- WHEN successive partial refunds with custom amounts are applied
- THEN Σ Refunds.Amount for the reservation is ≤ the transaction amount after every operation
