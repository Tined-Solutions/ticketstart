# Delta for admin-ticket-stock

## Notes

Archive-time Purpose/Non-Goals update: "editing existing `TicketType` price/name" is no longer a global non-goal — it is now allowed for eligible pre-approval events via `admin-ticket-availability-edit` (ATE). Add-only behavior remains for `Approved` events.

## MODIFIED Requirements

### Requirement: ATS-001: Admin-only authorization

All ticket-type operations (increment stock, create type, and full replacement via `PUT /api/admin/events/{id}/ticket-types`) MUST enforce the `RequireAdminRole` policy.
(Previously: only the two add-only operations were covered.)

#### Scenario: Non-admin rejected

- GIVEN a non-admin user (e.g., organizer)
- WHEN they call any ticket-type mutation endpoint (stock, create, or replace)
- THEN the system returns 403

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

### Requirement: ATS-009: Test coverage

Backend MUST follow strict TDD: controller tests (403/404/400/409, audit-verify), service tests (validation, persistence, concurrency where the provider supports it, and atomic rollback for the replacement). Frontend MUST get Vitest coverage for `AddTicketsModal`, `EditTicketsModal`, and the `AdminPanel` actions.
(Previously: frontend coverage was SHOULD-level and omitted the full-edit modal.)

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` runs
- THEN new tests pass and existing tests are unaffected
