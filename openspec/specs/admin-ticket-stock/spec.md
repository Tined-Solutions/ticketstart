# Admin Ticket Stock Specification

## Purpose

Admins MUST be able to add ticket capacity to an existing event at any lifecycle stage: increment an existing `TicketType`'s `Quantity`, or create a new `TicketType` (different zone/price). Both operations are admin-only, concurrency-safe, audited, and reflected automatically in buyer availability. Both operations MUST be blocked on past events: an Admin SHALL NOT add stock or a ticket type to an event whose `Date` has passed, returning 409 `event-finalized` before any capacity mutation (see `past-event-mutation-guard` PEM-002). Full replacement of the complete ticket-type list for eligible pre-approval events is specified by `admin-ticket-availability-edit` (ATE); the add-only flow remains required for `Approved` events and for any event blocked by commercial history.

## Non-Goals

Seat entities/seat maps, per-seat QR, buyer "new seats" notification, decreasing `Quantity`, and organizer-triggered stock changes. Editing an existing `TicketType`'s price/name is no longer a global non-goal: it is allowed for eligible pre-approval events via `admin-ticket-availability-edit` (ATE); the add-only flow remains for `Approved` events.

## Requirements

### Requirement: ATS-001: Admin-only authorization

All ticket-type operations (increment stock, create type, and full replacement via `PUT /api/admin/events/{id}/ticket-types`) MUST enforce the `RequireAdminRole` policy.
(Previously: only the two add-only operations were covered.)

#### Scenario: Non-admin rejected

- GIVEN a non-admin user (e.g., organizer)
- WHEN they call any ticket-type mutation endpoint (stock, create, or replace)
- THEN the system returns 403

### Requirement: ATS-002: Increment existing ticket type stock

The system MUST provide `POST /api/admin/events/{eventId}/ticket-types/{ticketTypeId}/stock` with body `{ "additionalQuantity": int }`. It MUST validate: integer > 0 and ≤ 1000; event exists; ticket type exists and its `EventId` matches. It MUST reject a past event (`Date < now`) with 409 `event-finalized` BEFORE any `Quantity` mutation. Success returns 200 with `{ id, name, price, quantity, available }`.
(Previously: no date guard — incrementing stock on a past event was allowed.)

#### Scenario: Happy path increment

- GIVEN an event with a future `Date` and TicketType "General" (Quantity=100)
- WHEN an admin posts `{ "additionalQuantity": 50 }`
- THEN the system returns 200 with quantity=150 and recomputed available

#### Scenario: Increment past event rejected

- GIVEN an event with `Date < now` and an existing TicketType
- WHEN an admin posts the increment
- THEN the system returns 409 with `type: "event-finalized"`
- AND `Quantity` is unchanged

#### Scenario: Unknown event or mismatched ticket type

- GIVEN no event for `eventId`, or a ticket type whose `EventId` differs
- WHEN an admin posts the increment
- THEN the system returns 404 and quantity is unchanged

#### Scenario: Invalid additional quantity

- GIVEN `additionalQuantity` of 0, negative, or above 1000
- WHEN an admin posts the increment
- THEN the system returns 400 and quantity is unchanged

### Requirement: ATS-003: Concurrent increment serialization

The increment MUST take the same `SELECT ... FOR UPDATE` row lock on the `TicketType` used by `ReservationService.CreateReservationTransactionalAsync` (provider fallbacks allowed for SQLite/InMemory). The system MUST NOT mutate `Quantity` outside that lock.

#### Scenario: Concurrent increment and reservation serialize

- GIVEN Quantity=10, a concurrent increment of 5, and a reservation of 8
- WHEN both execute against the same row
- THEN they serialize on the lock: no lost update, no oversell

### Requirement: ATS-004: Create new ticket type

The system MUST provide `POST /api/admin/events/{eventId}/ticket-types` with body `{ name, price, quantity }`. It MUST validate: name non-empty and ≤ 100 chars; price ≥ 0; quantity > 0 and ≤ 1000; event exists. It MUST reject a past event (`Date < now`) with 409 `event-finalized` BEFORE any insert. Success returns 201 with `{ id, name, price, quantity, available }`.
(Previously: no date guard — creating a ticket type on a past event was allowed.)

#### Scenario: Happy path new type

- GIVEN an existing event with a future `Date`
- WHEN an admin posts `{ "name": "VIP", "price": 150, "quantity": 20 }`
- THEN the system returns 201 with the new type
- AND it appears in the buyer catalog

#### Scenario: New type on past event rejected

- GIVEN an event with `Date < now`
- WHEN an admin posts the new type
- THEN the system returns 409 with `type: "event-finalized"`
- AND no row is created

#### Scenario: Invalid payload

- GIVEN empty name, negative price, or quantity above 1000
- WHEN an admin posts the new type
- THEN the system returns 400 and creates no row

### Requirement: ATS-005: Audit logging

Every operation MUST write an audit entry with new `AuditActionType` members (`AddTicketStock`, `AddTicketType`, `EditTicketTypes`) and `AuditResourceType.Event`. `Details` MUST be truncated to ≤ 1000 chars. Adding members MUST NOT require a migration (ActionType stored as string).
(Previously: only `AddTicketStock`/`AddTicketType` were listed.)

#### Scenario: Successful operation is audited

- GIVEN any of the three operations succeeds
- WHEN it completes
- THEN an audit entry is written with the matching action type
- AND details stay within the column limit

### Requirement: ATS-006: Availability recalculates automatically

Availability MUST be derived mathematically (no stock counter), so `GET /api/events/{id}` and `GET /api/events/{id}/manage` reflect all three operations without extra writes.

#### Scenario: Availability reflected after invalidation

- GIVEN an admin completes any ticket-type operation
- WHEN the frontend invalidates `managementEvent(id)`, `event(id)`, and `events`
- THEN the management view and buyer `EventDetail`/catalog show the updated "X disponibles de Y"

### Requirement: ATS-007: Admin UI operations

`AdminPanel` MUST provide a per-event "add tickets" modal for `Approved` events (increment existing type or create a new one) AND a full-edit modal (`EditTicketsModal`) for eligible `Pending`/`Rejected` events. Both MUST invalidate `managementEvent(id)`, `event(id)`, and `events` on success; on failure each MUST show an error without mutating local state.

#### Scenario: Success invalidates queries

- GIVEN an admin completes an operation in either modal
- WHEN the modal confirms success
- THEN `managementEvent(id)`, `event(id)`, and `events` are invalidated

#### Scenario: Failure shows error

- GIVEN the backend returns 400, 404, or 409
- WHEN the modal receives the error
- THEN the admin sees an error and local state is unchanged

### Requirement: ATS-008: EventForm trap addressed before ship

The feature MUST NOT ship while `EventForm` edit mode silently ignores ticket-type quantity edits. The mitigation (disable/hide fields, or explicit warning) is a design decision; no silent no-op MAY remain.

#### Scenario: No silent no-op remains

- GIVEN an admin opens an event in edit mode after ship
- WHEN they interact with ticket-type quantity fields
- THEN the fields are disabled/hidden or warn explicitly, never silently ignored

### Requirement: ATS-009: Test coverage

Backend MUST follow strict TDD: controller tests (403/404/400/409, audit-verify), service tests (validation, persistence, concurrency where the provider supports it, and atomic rollback for the replacement). Frontend MUST get Vitest coverage for `AddTicketsModal`, `EditTicketsModal`, and the `AdminPanel` actions.
(Previously: frontend coverage was SHOULD-level and omitted the full-edit modal.)

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` runs
- THEN new tests pass and existing tests are unaffected
