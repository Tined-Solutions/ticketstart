# spec.md — Payments domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirements 5 (Payment Processing) and 12 (Automatic Refunds on Stock Failure). No new content added.

## ADDED Requirements

### Requirement: Payment Processing

The system SHALL process ticket payments through Mercado Pago, validate webhook signatures, and convert reservations into confirmed tickets on success or release reservations on failure.

#### Scenario: Checkout creates a Mercado Pago payment preference
- **GIVEN** a user initiates checkout with a valid reservation
- **WHEN** the Backend creates the payment preference
- **THEN** the Backend SHALL create a Mercado Pago payment preference

#### Scenario: Payment preference contains complete reservation data
- **GIVEN** a reservation exists with ticket quantities and total amount
- **WHEN** the Backend builds the payment preference
- **THEN** the Backend SHALL include reservation details, ticket quantities, and total amount in the payment preference
- Validates design property 14: Payment Preference Contains Complete Data

#### Scenario: Mercado Pago checkout URL is returned to the Frontend
- **GIVEN** a payment preference has been created
- **WHEN** the Backend responds to the checkout request
- **THEN** the Backend SHALL return the Mercado Pago checkout URL to the Frontend

#### Scenario: Frontend redirects the user to Mercado Pago checkout
- **GIVEN** the Frontend has received a Mercado Pago checkout URL
- **WHEN** the user proceeds to payment
- **THEN** the Frontend SHALL redirect the user to the Mercado Pago checkout URL

#### Scenario: Successful payment webhook converts the reservation to tickets
- **GIVEN** Mercado Pago has processed a payment successfully
- **WHEN** the Payment_Gateway sends a successful webhook notification to the Backend
- **THEN** the Backend SHALL convert the reservation to confirmed tickets
- Validates design property 15: Successful Payment Creates Tickets

#### Scenario: Failed payment webhook releases the reservation
- **GIVEN** a payment has failed at Mercado Pago
- **WHEN** the Payment_Gateway sends a failed webhook notification to the Backend
- **THEN** the Backend SHALL release the reservation and restore inventory
- Validates design property 16: Failed Payment Releases Reservation

#### Scenario: Webhook signatures are validated for authenticity
- **GIVEN** the Backend receives a webhook
- **WHEN** the Backend authenticates the webhook
- **THEN** the Backend SHALL validate webhook signatures to ensure authenticity and reject invalid signatures with 401 Unauthorized
- Validates design property 17: Webhook Signature Validation

### Requirement: Automatic Refunds on Stock Failure

The system SHALL issue automatic refunds via Mercado Pago when ticket inventory is insufficient after payment, logging the failure, notifying the user, releasing reservations, and handling concurrent purchases.

#### Scenario: Inventory availability is verified after payment confirmation
- **GIVEN** a payment has just been confirmed
- **WHEN** the Backend processes the confirmation
- **THEN** the Backend SHALL verify ticket inventory availability

#### Scenario: Insufficient inventory triggers a refund via Mercado Pago
- **GIVEN** a payment is confirmed but ticket inventory is insufficient
- **WHEN** the Backend detects the stock failure
- **THEN** the Backend SHALL initiate a refund via Payment_Gateway
- Validates design property 38: Stock Failure Triggers Refund

#### Scenario: Stock failure and refund are logged
- **GIVEN** a refund has been triggered due to a stock failure
- **WHEN** the Backend records the refund
- **THEN** the Backend SHALL log the stock failure and refund transaction
- Validates design property 39: Refund Logging

#### Scenario: User is notified by email about the refund
- **GIVEN** a refund has been initiated for a user
- **WHEN** the Backend notifies the purchaser
- **THEN** the Backend SHALL send an email notification to the user explaining the refund
- Validates design property 40: Refund Notification Email

#### Scenario: Associated reservations are released on stock failure
- **GIVEN** a stock failure occurs during confirmation
- **WHEN** the Backend processes the failure
- **THEN** the Backend SHALL release any associated reservations

#### Scenario: Concurrent purchases of the last tickets do not oversell
- **GIVEN** multiple users purchase the last available tickets simultaneously
- **WHEN** the purchases are processed concurrently
- **THEN** the Backend SHALL handle race conditions so that total confirmed tickets never exceed ticket type quantity
- Validates design property 41: Concurrent Purchase Prevention (No Overselling)