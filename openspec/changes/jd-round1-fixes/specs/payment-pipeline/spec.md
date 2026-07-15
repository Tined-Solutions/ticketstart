# Payment Pipeline Specification

## Purpose

Implement the complete purchaser email flow, idempotent webhook processing, correct signature validation, and atomic payment confirmation to prevent double-charges, lost tickets, and email failures.

## JD Findings Covered

JD-C4, JD-C8, JD-S3, JD-S4

## Requirements

### REQ-1: PurchaserEmail End-to-End Flow

The system MUST collect, store, and use the purchaser's email from checkout through ticket creation and email delivery.

**JD-C4** — Files: `backend/Services/ReservationService.cs`, `backend/Services/PaymentService.cs`, `backend/Services/TicketService.cs`, `frontend/src/pages/Checkout.jsx`

#### Scenario: Email collected at checkout and stored

- GIVEN a guest buyer enters `buyer@example.com` in both email fields
- WHEN `POST /api/reservations` is called with `PurchaserEmail`
- THEN the email is stored on the reservation record

#### Scenario: Double email input with paste blocked

- GIVEN the checkout form with email and confirm-email fields
- WHEN the user pastes into the confirm-email field
- THEN the paste is blocked (`e.preventDefault()`) forcing manual re-entry

#### Scenario: Mismatched emails rejected

- GIVEN email and confirm-email fields with different values
- WHEN the reservation is submitted
- THEN the server returns 400 Bad Request

#### Scenario: Tickets created with purchaser email

- GIVEN a confirmed payment for a reservation with `PurchaserEmail`
- WHEN tickets are created
- THEN each ticket's `PurchaserEmail` equals the stored value (not `guest@ticketera.com`)

**Tests**: Integration test for full flow; unit test for email mismatch validation.

---

### REQ-2: Email Sent After Payment Confirmation

The system MUST send the ticket email after payment is confirmed and committed, and email failure MUST NOT revert the payment.

**JD-C4** — File: `backend/Services/PaymentService.cs`

#### Scenario: Email sent after successful commit

- GIVEN a payment is confirmed and the DB transaction is committed
- WHEN the email service is called
- THEN the ticket email is sent to `PurchaserEmail`

#### Scenario: Email failure does not revert payment

- GIVEN the email service throws an exception
- WHEN the payment has already been committed
- THEN the payment and tickets remain in the database
- AND the error is logged for manual retry

**Tests**: Unit test with mocked email service throwing; integration test verifying tickets persist after email failure.

---

### REQ-3: Idempotent Webhook Processing

The system MUST process each Mercado Pago webhook exactly once using a unique constraint on `Transaction.MercadoPagoId`.

**JD-C8** — Files: `backend/Services/PaymentService.cs`, `backend/Models/Transaction.cs`, `backend/Data/ApplicationDbContext.cs`

#### Scenario: First webhook creates transaction

- GIVEN a webhook with `MercadoPagoId = "mp-123"` not yet in the database
- WHEN the webhook is processed
- THEN a `Transaction` is inserted and the reservation is confirmed

#### Scenario: Duplicate webhook returns 200 without reprocessing

- GIVEN a `Transaction` with `MercadoPagoId = "mp-123"` already exists
- WHEN the same webhook is received again
- THEN the server returns 200 OK without modifying any data

#### Scenario: Concurrent duplicate webhook handled by unique constraint

- GIVEN two concurrent webhooks with the same `MercadoPagoId`
- WHEN both attempt to insert
- THEN one succeeds and the other catches `DbUpdateException` and returns 200 OK

**Tests**: Integration test for duplicate detection; concurrent webhook test.

---

### REQ-4: Raw-Bytes Webhook Signature Validation

The system MUST validate Mercado Pago webhook signatures against the raw request body bytes, not a re-serialized payload.

**JD-S3** — Files: `backend/Controllers/PaymentController.cs`, `backend/Services/PaymentService.cs`

#### Scenario: Valid signature accepted

- GIVEN a webhook with a valid HMAC-SHA256 signature over the raw body
- WHEN the controller receives it
- THEN signature validation passes and processing continues

#### Scenario: Tampered body rejected

- GIVEN a webhook where the body has been modified after signing
- WHEN signature validation runs against the raw bytes
- THEN validation fails and 401 Unauthorized is returned

**Tests**: Unit test with known HMAC vector; integration test for tampered payload.

---

### REQ-5: Atomic Payment Confirmation

The system MUST wrap reservation confirmation, ticket creation, and transaction insertion in a single database transaction.

**JD-S4** — File: `backend/Services/PaymentService.cs`

#### Scenario: All-or-nothing confirmation

- GIVEN a payment approval triggers confirmation + ticket creation + transaction insert
- WHEN any step fails
- THEN all changes are rolled back via `RollbackAsync`

#### Scenario: Successful commit persists all entities

- GIVEN all steps succeed
- WHEN `CommitAsync` is called
- THEN reservation, tickets, and transaction are all persisted atomically

**Tests**: Integration test with forced failure at step 2 verifying rollback; happy-path test.
