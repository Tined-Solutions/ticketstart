# Ticket Lookup Specification

## Purpose

Restrict the public ticket lookup to information-only responses (no QR, no print, no download), add a rate-limited resend endpoint, validate QR timestamps, and remove the unnecessary anonymous reservation detail endpoint.

## JD Findings Covered

JD-C2, JD-C3, JD-C6, JD-W27

## Requirements

### REQ-1: Info-Only Public Lookup

The system MUST return only basic event and ticket information from the public lookup endpoint — no QR codes, no print, no download.

**JD-C2, JD-C6** — Files: `backend/Controllers/TicketController.cs`, `frontend/src/pages/TicketLookup.jsx`

#### Scenario: Lookup returns info without QR

- GIVEN a valid email + DNI combination
- WHEN `GET /api/tickets/lookup` is called
- THEN the response includes event name, date, location, ticket type, quantity, and status
- AND the response does NOT include `qrCodeData`, `qrSrc`, or any QR representation

#### Scenario: Frontend displays info-only card

- GIVEN the lookup results are loaded
- WHEN the user views the results
- THEN only event info and status are shown
- AND no print, download, or QR display buttons exist

**Tests**: Integration test verifying response shape excludes QR fields; frontend test for absent buttons.

---

### REQ-2: Rate-Limited Ticket Resend

The system MUST provide `POST /api/tickets/resend` with rate limiting, CAPTCHA placeholder, and a generic response to prevent email enumeration.

**JD-C2** — Files: `backend/Controllers/TicketController.cs`, `frontend/src/pages/TicketLookup.jsx`

#### Scenario: Resend request within rate limit

- GIVEN an email that has not exceeded 3 resend requests in the last hour
- WHEN `POST /api/tickets/resend` is called with `{ email, captchaToken }`
- THEN the system queues an email resend if matching tickets exist
- AND returns a generic response: "Si hay entradas asociadas, recibiras un email"

#### Scenario: Resend rate limit exceeded

- GIVEN an email that has already received 3 resend requests in the last hour
- WHEN another `POST /api/tickets/resend` is called
- THEN the server returns 429 Too Many Requests

#### Scenario: Generic response regardless of email existence

- GIVEN an email with no associated tickets
- WHEN `POST /api/tickets/resend` is called
- THEN the same generic response is returned (no enumeration possible)

**Tests**: Integration test for rate limiting; unit test for generic response on both existing and non-existing emails.

---

### REQ-3: Anonymous Reservation Endpoint Removed

The system MUST NOT expose `GET /api/reservations/{id}` as an anonymous endpoint.

**JD-C3** — File: `backend/Controllers/ReservationController.cs`

#### Scenario: GET /api/reservations/{id} returns 404

- GIVEN the application is running
- WHEN an unauthenticated client requests `GET /api/reservations/{id}`
- THEN the server returns 404 Not Found (endpoint removed)

**Tests**: Integration test confirming endpoint removal.

---

### REQ-4: QR Timestamp Window Validation

The system MUST validate that the QR timestamp falls within an acceptable window: from purchase time until 24 hours after the event ends.

**JD-W27** — Files: `backend/Services/TicketService.cs`, `backend/Helpers/HmacHelper.cs`

#### Scenario: QR within valid window accepted

- GIVEN a QR with timestamp between purchase date and 24h post-event
- WHEN the QR is scanned and validated
- THEN the signature and timestamp check pass

#### Scenario: QR after event + 24h rejected

- GIVEN a QR with timestamp older than 24h after the event end date
- WHEN the QR is scanned
- THEN validation fails and the ticket is rejected

#### Scenario: QR with future timestamp rejected

- GIVEN a QR with a timestamp in the future (after current time)
- WHEN the QR is scanned
- THEN validation fails

**Tests**: Unit test for window boundaries (before purchase, valid, after event+24h).
