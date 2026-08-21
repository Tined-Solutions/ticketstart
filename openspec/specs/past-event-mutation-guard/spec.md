# Past Event Mutation Guard

**Requirements covered**: PEM-001 … PEM-005

## Purpose

An event whose `Date` has already passed (`Date < server UTC now`) MUST be immutable for both Admin and Organizer. All seven event-mutation endpoints MUST reject a mutation of a past event with **409 Conflict** (`RFC 7807` `type: "event-finalized"`, title "Event has already finished") **before any save, audit, or notification side-effect**. The rule is HARD — it applies regardless of the `HideExpiredEvents` flag. Consultation (GET) and payments-domain operations are carve-outs and MUST keep working.

## Requirements

### Requirement: PEM-001: Shared mutation guard

The system MUST evaluate immutability via `eventEntity.IsExpired(clock.GetUtcNow().UtcDateTime)` on the **materialized** entity (never inside an `IQueryable` predicate). When the entity is expired, the system SHALL throw before any write. The guard MUST apply to both Admin and Organizer roles.

#### Scenario: Expired entity throws

- GIVEN a materialized event with `Date < now` and a frozen clock
- WHEN the guard is evaluated
- THEN it throws and no mutation proceeds

#### Scenario: Active entity passes

- GIVEN a materialized event with `Date > now`
- WHEN the guard is evaluated
- THEN it returns without throwing

#### Scenario: Exact instant is mutable

- GIVEN an event with `Date == now` (strict `<`)
- WHEN the guard is evaluated
- THEN it returns without throwing

### Requirement: PEM-002: All seven mutation endpoints reject past events

Each of the following MUST return 409 `event-finalized` when the target event is past, before any save/audit/notification: `PUT /events/{id}`; `DELETE /events/{id}`; `POST /events/{id}/image`; `POST /admin/events/{id}/ticket-types/{ttId}/stock`; `POST /admin/events/{id}/ticket-types`; `POST /admin/events/{id}/approve`; `POST /admin/events/{id}/reject`.

#### Scenario: Each mutation returns 409 on past event

- GIVEN a past event and a valid requester (owner or Admin)
- WHEN any of the seven mutation endpoints is called
- THEN the response is 409 with `type: "event-finalized"` and title "Event has already finished"

#### Scenario: Response is RFC 7807 ProblemDetails

- GIVEN a rejected past-event mutation
- WHEN the response body is inspected
- THEN it is `application/problem+json` with `type`, `title`, `status: 409`, `detail`, and `instance`

### Requirement: PEM-003: No side-effects on rejection

On a past-event mutation the system MUST NOT perform any save, audit write, or notification (including date-change buyer emails). The guard MUST throw before any such side-effect.

#### Scenario: No save or audit or notification on reject

- GIVEN a past event and a frozen clock
- WHEN a mutation is attempted
- THEN no row changes, no audit entry, and no notification queue enqueue occur

#### Scenario: Future event still has side-effects

- GIVEN a future event
- WHEN a mutation succeeds
- THEN save, audit, and (on date change) notification proceed as before

### Requirement: PEM-004: Rule is flag-independent

The immutability guard MUST apply regardless of the `HideExpiredEvents` configuration value. The flag MUST NOT gate the guard.

#### Scenario: Guard active even when flag disabled

- GIVEN `HideExpiredEvents.Enabled == false` and a past event
- WHEN a mutation is attempted
- THEN it is still rejected with 409

### Requirement: PEM-005: Consultation and payments carve-out

The guard MUST apply to mutation only. `GET /events/{id}/manage` (includeExpired), purchases listing, and payments-domain operations (refunds) on past-event tickets MUST keep working and MUST NOT be blocked by the guard.

#### Scenario: Consultation GET on past event unaffected

- GIVEN a past event
- WHEN the management detail endpoint is called
- THEN the response is 200 with full event data

#### Scenario: Purchases and refunds on past event unaffected

- GIVEN a past event with purchases
- WHEN purchases are listed or a refund is issued
- THEN the operations succeed unchanged

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| PEM-001 | expired-throws, active-passes, exact-instant-mutable |
| PEM-002 | each-mutation-409, rfc7807-problem-details |
| PEM-003 | no-side-effects-on-reject, future-still-side-effects |
| PEM-004 | flag-independent |
| PEM-005 | consultation-ok, purchases-refunds-ok |
