# Admin Ticket Availability Edit Specification

**Requirements covered**: ATE-001 … ATE-011

## Purpose

An Admin MUST be able to fully replace the complete ticket-type list (names, prices, quantities, additions, deletions) of a pre-approval event in ONE atomic call. Full edit SHALL be allowed for `Pending` and for `Rejected` without commercial history; `Approved` MUST keep the add-only flow (`admin-ticket-stock`). The replacement MUST NOT change `Event.Status`; public visibility stays intact (EA-001/EA-005, EHE-002/EHE-003). Out of scope: schema migration, organizer `EventForm`, hardening the add-only methods, editing past events.

## Requirements

### Requirement: ATE-001: Atomic full-replacement endpoint

The system MUST expose `PUT /api/admin/events/{eventId}/ticket-types` (Admin-only). The body MUST be the COMPLETE desired list `{ "ticketTypes": [ { "id"?, "name", "price", "quantity" } ] }`. Types present on the event but absent from the payload MUST be deleted; items with a supplied `id` MUST be updated; items without `id` MUST be inserted. The replacement MUST be all-or-nothing: no partial writes MAY persist on validation or database failure.

#### Scenario: Replace with mixed add/edit/delete

- GIVEN a `Pending` event with types A and B
- WHEN an admin PUTs A (renamed/re-priced), a new type C, and omits B
- THEN the response is 200; A is updated, C is created, B is deleted

#### Scenario: Validation failure rolls back everything

- GIVEN a payload where the last item has an invalid quantity
- WHEN the PUT runs
- THEN the response is 400 and NO row is inserted, updated, or deleted

#### Scenario: Database failure rolls back everything

- GIVEN a mid-transaction insert/update failure
- WHEN the PUT runs
- THEN the transaction rolls back and the prior list is unchanged

#### Scenario: Empty list rejected

- GIVEN `ticketTypes` is empty
- WHEN the PUT runs
- THEN the response is 400 and no mutation occurs

### Requirement: ATE-002: Eligibility and guard order

Before any mutation the system MUST evaluate, in this order: (1) event exists; (2) `EventFinalizedGuard.EnsureMutable` (PEM-001); (3) eligibility (ATE-003) — status MUST be `Pending` or `Rejected` AND the event MUST have NO commercial history; (4) payload validation (ATE-004); (5) `id` reference validation (ATE-005). The finalized guard MUST run BEFORE the eligibility check so that EVERY past event returns `event-finalized` (PEM-002), including past `Approved` and past history-bearing events. `Approved` MUST fail eligibility first with `ticket-types-not-editable` (even when it has history); `Pending` or `Rejected` WITH history MUST fail with `ticket-types-referenced`.

#### Scenario: Approved event is not eligible

- GIVEN a future `Approved` event (with or without commercial history)
- WHEN the PUT runs
- THEN the response is 409 `type: "ticket-types-not-editable"` and no mutation or audit occurs

#### Scenario: Past event always returned as finalized

- GIVEN any past event (any status)
- WHEN the PUT runs
- THEN the response is 409 `type: "event-finalized"` and no mutation or audit occurs

#### Scenario: Payload is validated before references

- GIVEN a payload with both an invalid name and an unknown `id`
- WHEN the PUT runs
- THEN the response is 400 for payload validation, without attempting reference checks

### Requirement: ATE-003: Commercial-history predicate

Commercial history SHALL mean ANY `Ticket` row OR ANY `Reservation` row whose `TicketTypeId` belongs to the event, regardless of ticket/reservation state. Rationale: `Ticket.TicketTypeId` and `Reservation.TicketTypeId` are `OnDelete(Restrict)`, so the FK protects rows in every state (refunded/used/cancelled/expired included). The predicate is UNIVERSAL across non-`Approved` statuses: a `Pending` OR `Rejected` event WITH history MUST return 409 `type: "ticket-types-referenced"` — `Approved → Pending` is reachable (EA-005), so `Pending`-with-history has the identical FK hazard. Deleting any referenced type MUST be rejected the same way, independent of status. The add-only flow (ATS) MUST remain available for any blocked event. Only an event that is `Pending`/`Rejected` AND history-free is eligible for full edit.

#### Scenario: Rejected with a refunded ticket is blocked

- GIVEN a `Rejected` event with one refunded `Ticket`
- WHEN the PUT runs
- THEN the response is 409 `type: "ticket-types-referenced"` and no row is deleted

#### Scenario: Pending with history is blocked

- GIVEN a `Pending` event with one `Ticket` or `Reservation` row
- WHEN the PUT runs
- THEN the response is 409 `type: "ticket-types-referenced"`
- AND no mutation occurs and no audit entry is written

#### Scenario: Blocked event keeps add-only available

- GIVEN a `Pending` or `Rejected` event blocked by commercial history
- WHEN the admin calls `POST /admin/events/{id}/ticket-types/{ttId}/stock` or `POST /admin/events/{id}/ticket-types`
- THEN the add-only flow remains available

#### Scenario: Non-approved without history is editable

- GIVEN a `Pending` or `Rejected` event with zero `Ticket` and zero `Reservation` rows
- WHEN the PUT runs
- THEN the replacement succeeds with 200

#### Scenario: No FK violation

- GIVEN any event whose ticket type is referenced by a `Ticket` or `Reservation` row
- WHEN the replacement would delete that type
- THEN the operation is rejected and no database FK error surfaces as 500

### Requirement: ATE-004: Payload validation

The payload MUST contain ≥ 1 type. Each item MUST have: non-empty trimmed name ≤ 100 chars; `price >= 0` (pinned canonical, parity with `CreateEventAsync`); integer `quantity > 0`; per-type TOTAL quantity ≤ 1000 (`MaxTicketQuantityPerOperation`). Legacy caveat: add-stock increments MAY have accumulated a total > 1000; re-submitting such a total MUST be rejected (explicit behavior) and the admin MAY lower it.

#### Scenario: price = 0 is accepted

- GIVEN an eligible event and an item with `price: 0`
- WHEN the PUT runs
- THEN the response is 200

#### Scenario: quantity above cap rejected

- GIVEN an item with `quantity: 1001`
- WHEN the PUT runs
- THEN the response is 400 and no mutation occurs

#### Scenario: Legacy total above cap is rejected explicitly

- GIVEN an existing type with Quantity 1500 (accumulated via add-stock)
- WHEN the admin re-submits that total
- THEN the response is 400 and the admin can lower the quantity to proceed

### Requirement: ATE-005: Id reference validation

A supplied `id` MUST belong to a ticket type of the target event. An `id` that is unknown or belongs to another event MUST yield 400 and roll back; items without `id` MUST create new types.

#### Scenario: Foreign id rejected

- GIVEN a `ticketTypes` item whose `id` belongs to a different event
- WHEN the PUT runs
- THEN the response is 400 and no mutation occurs

### Requirement: ATE-006: Response contract

Success MUST return 200 with the recomputed `TicketTypeWithAvailability[]` (`{ id, name, price, quantity, available }`), with `available = max(0, quantity - sold - reserved)`. No stock counter MAY be stored.

#### Scenario: Availability recomputed

- GIVEN a successful replacement
- WHEN the response is inspected
- THEN each item carries recomputed `available`

### Requirement: ATE-007: Error mapping

The endpoint MUST map: 404 event not found; 400 payload/reference validation; 409 `ticket-types-not-editable` (Approved), `ticket-types-referenced` (history), `event-finalized` (past); 500 unexpected (with rollback). All 409 bodies MUST be RFC 7807 `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`.

#### Scenario: 409 bodies are ProblemDetails

- GIVEN any 409 outcome
- WHEN the response body is inspected
- THEN it is `application/problem+json` with the standard fields

#### Scenario: Unknown event

- GIVEN a non-existent `eventId`
- WHEN the PUT runs
- THEN the response is 404 with no audit entry

### Requirement: ATE-008: Audit logging

On success the system MUST write exactly one audit entry via `TryLogAuditAsync` with the new `AuditActionType.EditTicketTypes` (varchar-backed, no migration) and `AuditResourceType.Event`, `ResourceId = eventId`, `Details` truncated to ≤ 1000 chars. Failures MUST NOT write an audit entry.

#### Scenario: Success is audited once

- GIVEN a successful replacement
- WHEN the audit store is inspected
- THEN one `EditTicketTypes` entry exists for the event

#### Scenario: Failure is not audited

- GIVEN a 400/409/500 outcome
- WHEN the audit store is inspected
- THEN no `EditTicketTypes` entry was added

### Requirement: ATE-009: EditTicketsModal frontend contract

`EditTicketsModal` MUST load via `useManagementEvent(id)` (NOT `useEvent`, which 404s for non-`Approved` events), MUST support add/edit/delete of rows, and MUST submit the complete list in one PUT. Validation MUST mirror ATE-004 (name ≤ 100, `price >= 0`, integer quantity > 0 and ≤ 1000). A11y MUST follow `useDialog` (`role="dialog"`, `aria-modal="true"`, `aria-labelledby`, focus first error, `role="alert"` for form errors). On success it MUST invalidate `managementEvent(id)`, `event(id)`, and `events`.

#### Scenario: Loads non-approved event

- GIVEN a `Pending` or `Rejected` event
- WHEN the modal opens
- THEN ticket types come from `managementEvent(id)` and render

#### Scenario: Success invalidates the three queries

- GIVEN a successful submit
- WHEN the mutation settles
- THEN `managementEvent(id)`, `event(id)`, and `events` are invalidated

#### Scenario: `$0` price accepted in the modal

- GIVEN the admin enters price 0
- WHEN the form is validated
- THEN no price error is shown (mirrors `price >= 0`)

#### Scenario: Errors announced

- GIVEN an invalid field
- WHEN submit is attempted
- THEN focus moves to the first invalid field and the error is a `role="alert"`

### Requirement: ATE-010: AdminPanel status-aware action

For `Pending` and `Rejected` events `AdminPanel` MUST render a full-edit action with a label and icon DISTINCT from the existing "Editar" (organizer navigation) — e.g. "Editar entradas" with a ticket/pencil icon. For `Approved` events it MUST keep "Agregar entradas". The blocked-Rejected path (409 on PUT) MUST surface an error explaining that the event has sales and only add-only remains. Past events MUST disable the action.

#### Scenario: Pending row offers edit-entradas

- GIVEN a `Pending` event row
- WHEN it renders
- THEN an "Editar entradas" action distinct from "Editar" is present

#### Scenario: Approved row keeps add-only

- GIVEN an `Approved` event row
- WHEN it renders
- THEN "Agregar entradas" is present and no full-edit action is offered

#### Scenario: Blocked Rejected explains add-only

- GIVEN a `Rejected`-with-history PUT returns 409 `ticket-types-referenced`
- WHEN the modal shows the error
- THEN the message states full edit is blocked and add-only remains available

### Requirement: ATE-011: Test coverage

Backend MUST follow strict TDD: controller tests (403/404/400/409 + audit-verify) and service tests (validation, mixed add/edit/delete persistence, atomic rollback via failure injection). Frontend MUST add Vitest coverage for `EditTicketsModal` and the `AdminPanel` action.

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` and `npm test` run
- THEN new tests pass and existing tests are unaffected
