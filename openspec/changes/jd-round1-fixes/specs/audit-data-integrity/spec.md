# Audit & Data Integrity Specification

## Purpose

Consolidate metrics queries, add audit log pagination and referential integrity, redact PII from logs, harden reservation tokens, capture IP/User-Agent, and fix authorization handler issues.

## JD Findings Covered

JD-W4, JD-W5, JD-W8, JD-W17, JD-W19, JD-W20, JD-W24, JD-W26, JD-W29, JD-W33, JD-SG6

## Requirements

### REQ-1: Metrics Single GroupBy Query

The system MUST consolidate the N×5 metrics queries into a single `GroupBy(eventId)` query.

**JD-W4** — File: `backend/Services/MetricsService.cs`

#### Scenario: Metrics fetched in one round-trip

- GIVEN an organizer with 50 events
- WHEN `GetOrganizerMetricsAsync` is called
- THEN a single `GroupBy` query retrieves all metrics
- AND no per-event iteration with separate queries occurs

**Tests**: Integration test verifying query count (1 query, not N×5).

---

### REQ-2: Audit Log Pagination

The system MUST paginate `GetAllLogsAsync` with `page` and `pageSize` parameters.

**JD-W5** — File: `backend/Services/AdminService.cs`

#### Scenario: Paginated audit logs returned

- GIVEN 100 audit log entries exist
- WHEN `GetAllLogsAsync(page: 2, pageSize: 20)` is called
- THEN entries 21-40 are returned

#### Scenario: Default page size applied

- GIVEN no `pageSize` specified
- WHEN `GetAllLogsAsync` is called
- THEN a sensible default (e.g., 50) is applied

**Tests**: Unit test for pagination math; integration test for correct skip/take.

---

### REQ-3: Audit Log Foreign Key

The system MUST enforce a foreign key from `AuditLog.UserId` to `Users` with `OnDelete(Restrict)`.

**JD-W19** — Files: `backend/Data/ApplicationDbContext.cs`, EF Core migration

#### Scenario: Audit log with invalid UserId rejected

- GIVEN an `AuditLog` with a `UserId` not in the `Users` table
- WHEN `SaveChanges` is called
- THEN a FK constraint violation is raised

#### Scenario: User deletion blocked by audit logs

- GIVEN a user with associated audit log entries
- WHEN deletion of that user is attempted
- THEN the operation is blocked by `Restrict`

**Tests**: Integration test for FK constraint and restrict behavior.

---

### REQ-4: Out-of-Band Audit Log Failures

The system MUST NOT let audit log write failures break the primary operation.

**JD-W17** — File: `backend/Services/AuditLogService.cs`

#### Scenario: Audit write failure does not affect primary operation

- GIVEN the audit log database write throws an exception
- WHEN the primary operation completes successfully
- THEN the primary operation's result is returned to the caller
- AND the audit failure is logged separately

**Tests**: Unit test with mocked failing audit service.

---

### REQ-5: TryGetUserRole Returns False on Parse Failure

The system MUST return `false` from `TryGetUserRole` when `Enum.TryParse` fails, not silently fall back to a default role.

**JD-W20** — File: `backend/Controllers/EventController.cs`

#### Scenario: Invalid role claim returns false

- GIVEN a user whose role claim contains an invalid value
- WHEN `TryGetUserRole` is called
- THEN it returns `false` and the caller handles 403 Forbidden

#### Scenario: Valid role claim returns true with parsed role

- GIVEN a user with role claim `"Admin"`
- WHEN `TryGetUserRole` is called
- THEN it returns `true` with `role = Role.Admin`

**Tests**: Unit test for valid and invalid role claims.

---

### REQ-6: Webhook Audit Log Uses System Identifier

The system MUST log webhook audit entries with a "System" identifier instead of `Guid.Empty`.

**JD-W24** — File: `backend/Controllers/PaymentController.cs`

#### Scenario: Webhook audit entry has System user

- GIVEN a Mercado Pago webhook is processed
- WHEN an audit log entry is created
- THEN the `UserIdentifier` is "System" (not `Guid.Empty`)

**Tests**: Unit test verifying audit log user identifier.

---

### REQ-7: Reservation Token Hardening

The system MUST include a nonce, timestamp, and expiry in reservation tokens and validate them on use.

**JD-W26** — File: `backend/Services/ReservationService.cs`

#### Scenario: Token with expired timestamp rejected

- GIVEN a reservation token with timestamp older than the reservation expiry window
- WHEN the token is validated
- THEN validation fails and the reservation is rejected

#### Scenario: Token with valid nonce and timestamp accepted

- GIVEN a fresh token with valid nonce within the expiry window
- WHEN the token is validated
- THEN validation passes

**Tests**: Unit test for expired token, valid token, and tampered nonce.

---

### REQ-8: PII Redaction in Logs

The system MUST redact email and DNI values in `TicketService` logs using `LogRedactor.HashIdentifier`.

**JD-W29** — File: `backend/Services/TicketService.cs`

#### Scenario: Email redacted in log output

- GIVEN a ticket operation logs a message containing an email
- WHEN the log entry is written
- THEN the email is replaced with its hashed representation

#### Scenario: DNI redacted in log output

- GIVEN a ticket operation logs a message containing a DNI
- WHEN the log entry is written
- THEN the DNI is replaced with its hashed representation

**Tests**: Unit test verifying log output does not contain raw PII.

---

### REQ-9: IP and User-Agent Capture

The system MUST persist the client IP address and User-Agent on guest reservation creation and include them in audit log entries.

**JD-W33, JD-SG6** — Files: `backend/Controllers/ReservationController.cs`, `backend/Models/AuditLog.cs`

#### Scenario: Guest reservation stores IP and User-Agent

- GIVEN a guest creates a reservation
- WHEN the reservation is persisted
- THEN `ClientIp` and `UserAgent` fields are populated from the request

#### Scenario: AuditLog includes IP and User-Agent

- GIVEN an auditable action occurs
- WHEN the audit log entry is created
- THEN `IpAddress` and `UserAgent` are stored

**Tests**: Integration test verifying fields are populated from HttpContext.

---

### REQ-10: EventOwnershipHandler Parameter Name

The system MUST read the route parameter name from the requirement, not hardcode `id`.

**JD-W8** — File: `backend/Authorization/EventOwnershipHandler.cs`

#### Scenario: Handler works with eventId parameter

- GIVEN a route using `{eventId}` instead of `{id}`
- WHEN the ownership handler processes the requirement
- THEN it correctly reads the event ID from the route parameter specified by the requirement

**Tests**: Unit test with both `id` and `eventId` parameter names.
