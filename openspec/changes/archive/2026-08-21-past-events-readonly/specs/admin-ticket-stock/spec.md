# Admin Ticket Stock Specification

**Requirements covered**: ATS-002 (MODIFIED), ATS-004 (MODIFIED)

## Purpose

Admins MUST be able to add ticket capacity to an existing event at any lifecycle stage: increment an existing `TicketType`'s `Quantity`, or create a new `TicketType` (different zone/price). Both operations MUST be blocked on past events: an Admin SHALL NOT add stock or a ticket type to an event whose `Date` has passed, returning 409 `event-finalized` before any capacity mutation (see `past-event-mutation-guard` PEM-002).

## MODIFIED Requirements

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

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| ATS-002 | happy-path-increment, increment-past-rejected, unknown-event-mismatch, invalid-quantity |
| ATS-004 | happy-path-new-type, new-type-past-rejected, invalid-payload |
