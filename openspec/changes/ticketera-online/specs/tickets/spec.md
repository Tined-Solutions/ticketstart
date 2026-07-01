# spec.md — Tickets domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirements 6 (QR Code Ticket Generation), 7 (Email Ticket Delivery), 8 (Ticket Lookup), and 9 (QR Code Scanning and Validation). No new content added.

## ADDED Requirements

### Requirement: QR Code Ticket Generation

The system SHALL generate unique, cryptographically signed QR codes for each confirmed ticket and verify their HMAC-SHA256 signatures on validation.

#### Scenario: Unique QR code generated for each confirmed ticket
- **GIVEN** a payment is confirmed and tickets are created
- **WHEN** the Backend generates ticket credentials
- **THEN** the Backend SHALL generate a unique QR_Code for each ticket
- Validates design property 18: QR Code Uniqueness

#### Scenario: QR code signed with HMAC-SHA256
- **GIVEN** a QR_Code is generated for a ticket
- **WHEN** the Backend signs the QR_Code
- **THEN** the Backend SHALL sign each QR_Code using HMAC-SHA256 with a secret key
- Validates design property 19: QR Code Signature Validity

#### Scenario: QR code encodes ticket identifier and signature in the documented format
- **GIVEN** a QR_Code has been generated
- **WHEN** the QR_Code payload is encoded
- **THEN** the QR_Code SHALL encode the ticket identifier and signature in the format `{ticketId}:{timestamp}:{signature}`
- Validates design property 20: QR Code Format Correctness

#### Scenario: Ticket record with QR code is stored in the database
- **GIVEN** a ticket has been generated with a QR_Code
- **WHEN** the Backend persists the ticket
- **THEN** the Backend SHALL store the ticket record with QR_Code data in the database

#### Scenario: Visual QR code image generated for each ticket
- **GIVEN** tickets have been created for a confirmed reservation
- **WHEN** the Backend prepares the ticket credentials
- **THEN** the Backend SHALL generate a visual QR code image for each ticket

#### Scenario: QR code signature is verified on validation
- **GIVEN** a QR_Code is presented for validation
- **WHEN** the Backend validates the QR_Code
- **THEN** the Backend SHALL verify the HMAC-SHA256 signature
- Validates design property 21: QR Code Signature Verification

#### Scenario: Invalid signature is rejected as fraudulent
- **GIVEN** a QR_Code with an invalid HMAC-SHA256 signature is presented
- **WHEN** the Backend validates the signature
- **THEN** the Backend SHALL reject the ticket as fraudulent
- Validates design property 21: QR Code Signature Verification

### Requirement: Email Ticket Delivery

The system SHALL deliver confirmed tickets by email via Resend, including QR codes, event details, and purchase confirmation, with retry on delivery failure.

#### Scenario: Ticket confirmation email is sent via Resend
- **GIVEN** tickets have been confirmed for a purchase
- **WHEN** the Backend notifies the purchaser
- **THEN** the Backend SHALL send an email to the purchaser via Email_Service

#### Scenario: Email includes all ticket QR codes
- **GIVEN** a ticket confirmation email is being composed
- **WHEN** the email is assembled for a purchase with multiple tickets
- **THEN** the Backend SHALL include all ticket QR codes in the email
- Validates design property 22: Email Contains All Ticket QR Codes

#### Scenario: Email includes event details
- **GIVEN** a ticket confirmation email is being composed
- **WHEN** the email is assembled
- **THEN** the Backend SHALL include event details (name, date, location) in the email
- Validates design property 23: Email Contains Event Details

#### Scenario: Email includes purchase confirmation details
- **GIVEN** a ticket confirmation email is being composed
- **WHEN** the email is assembled
- **THEN** the Backend SHALL include purchase confirmation details in the email
- Validates design property 24: Email Contains Purchase Confirmation

#### Scenario: Resend is used as the email delivery service
- **GIVEN** the Backend needs to send transactional emails
- **WHEN** the Backend delivers the email
- **THEN** the Email_Service SHALL use Resend for email delivery

#### Scenario: Failed email delivery is retried
- **GIVEN** an email delivery attempt fails
- **WHEN** the Backend handles the failure
- **THEN** the Backend SHALL log the error and retry delivery
- Validates design property 25: Email Delivery Retry on Failure

#### Scenario: Frontend displays the email delivery status
- **GIVEN** a purchase has been completed
- **WHEN** the Frontend shows the confirmation
- **THEN** the Frontend SHALL display a confirmation message indicating email delivery status

### Requirement: Ticket Lookup

The system SHALL allow users to retrieve their tickets by email and DNI, returning all matching tickets with QR codes or an empty result.

#### Scenario: Frontend provides a ticket lookup form accepting email and DNI
- **GIVEN** a user wants to retrieve previously purchased tickets
- **WHEN** the user accesses the ticket lookup page
- **THEN** the Frontend SHALL provide a ticket lookup form accepting email and DNI

#### Scenario: Backend queries tickets matching both email and DNI
- **GIVEN** a user submits the lookup form with email and DNI
- **WHEN** the Backend processes the lookup
- **THEN** the Backend SHALL query tickets matching both email and DNI

#### Scenario: Backend returns all matching tickets with QR codes
- **GIVEN** the Backend found tickets matching the lookup criteria
- **WHEN** the Backend responds to the lookup request
- **THEN** the Backend SHALL return all matching tickets with QR codes
- Validates design property 26: Ticket Lookup Returns Correct Matches

#### Scenario: Frontend displays retrieved tickets with downloadable QR codes
- **GIVEN** tickets have been retrieved by lookup
- **WHEN** the Frontend renders the results
- **THEN** the Frontend SHALL display the retrieved tickets with downloadable QR codes

#### Scenario: Empty result returned when no tickets match
- **GIVEN** no tickets match the lookup criteria
- **WHEN** the Backend queries the database
- **THEN** the Backend SHALL return an empty result
- Validates design property 26: Ticket Lookup Returns Correct Matches

#### Scenario: Frontend shows a message when no tickets are found
- **GIVEN** a lookup returned no tickets
- **WHEN** the Frontend renders the result
- **THEN** the Frontend SHALL display a message when no tickets are found

### Requirement: QR Code Scanning and Validation

The system SHALL allow staff to scan ticket QR codes at event entrances, validating signature, usage status, and event association, with visual and audio feedback.

#### Scenario: Frontend provides a web-based QR scanner for staff
- **GIVEN** an authenticated Staff user accesses the scanner
- **WHEN** the user opens the scanner functionality
- **THEN** the Frontend SHALL provide a web-based QR scanner interface for Staff users

#### Scenario: Scanned QR code is sent to the Backend for validation
- **GIVEN** a staff member scans a QR_Code
- **WHEN** the Frontend captures the scan
- **THEN** the Frontend SHALL send the code to the Backend for validation

#### Scenario: Backend verifies the QR code HMAC-SHA256 signature
- **GIVEN** a scanned QR_Code reaches the Backend
- **WHEN** the Backend validates the code
- **THEN** the Backend SHALL verify the QR_Code HMAC-SHA256 signature

#### Scenario: Backend rejects already-used tickets
- **GIVEN** a ticket has already been used (IsUsed = true)
- **WHEN** the same ticket is scanned again
- **THEN** the Backend SHALL check ticket usage status and reject the duplicate scan
- Validates design property 27: Double-Scan Prevention

#### Scenario: Backend validates the ticket belongs to the scanned event
- **GIVEN** a ticket is scanned at an event
- **WHEN** the Backend validates the scanned QR_Code
- **THEN** the Backend SHALL check that the ticket belongs to the scanned event
- Validates design property 28: Event-Specific Ticket Validation

#### Scenario: Valid unused ticket is marked as used and returns success
- **GIVEN** a valid, unused ticket is scanned at the correct event
- **WHEN** the Backend validates and confirms the ticket
- **THEN** the Backend SHALL mark the ticket as used and return success
- Validates design property 29: Valid Ticket Marked as Used

#### Scenario: Invalid, already-used, or wrong-event ticket returns an error
- **GIVEN** the scanned ticket is invalid, already used, or for a different event
- **WHEN** the Backend validates the ticket
- **THEN** the Backend SHALL return an error

#### Scenario: Frontend displays validation results to staff
- **GIVEN** the Backend returned a validation result for a scan
- **WHEN** the Frontend renders the result
- **THEN** the Frontend SHALL display validation results (success or error reason) to staff

#### Scenario: Frontend provides visual and audio feedback for scan results
- **GIVEN** a scan result has been received by the Frontend
- **WHEN** the Frontend presents the result
- **THEN** the Frontend SHALL provide visual and audio feedback for scan results