# Catalog Filtering — Expired Event Exclusion

**Requirements covered**: EHE-001, EHE-002, EHE-003

## Purpose

Buyers MUST NOT see expired events in the public catalog. The system applies a single `IsExpired` predicate at the DB query level so that `GET /api/events` and `GET /api/events/{id}` (public) never surface past events, while a management variant preserves organizer/admin access.

## ADDED Requirements

### Requirement: EHE-001 — IsExpired domain predicate

`Event` SHALL expose `bool IsExpired(DateTime asOf)` returning `Date < asOf`. The predicate MUST be a pure function with no side effects, unit-testable in isolation.

#### Scenario: Event starting in the future is not expired

- GIVEN an event with `Date = 2026-09-01T20:00:00Z`
- WHEN `IsExpired(2026-08-12T12:00:00Z)` is called
- THEN the result is `false`

#### Scenario: Event whose start has passed is expired

- GIVEN an event with `Date = 2026-08-10T14:00:00Z`
- WHEN `IsExpired(2026-08-12T12:00:00Z)` is called
- THEN the result is `true`

#### Scenario: Event at exact start instant is **not** expired (strict less-than: `Date == asOf` → `false`)

- GIVEN an event with `Date = 2026-08-12T14:00:00Z`
- WHEN `IsExpired(2026-08-12T14:00:00Z)` is called
- THEN the result is `false` (the predicate uses strict `<`, not `<=`)

### Requirement: EHE-002 — Public event list excludes expired events

`GetAllPublishedEventsAsync` MUST apply a `Where(e => !e.IsExpired(DateTime.UtcNow))` filter at the DB query level so expired events never appear in `GET /api/events`. The filter MUST be order-independent and MUST NOT affect non-expired events.

#### Scenario: Expired event absent from public list

- GIVEN two events: A (Date = past) and B (Date = future)
- WHEN `GET /api/events` is called
- THEN the response contains B and does NOT contain A

#### Scenario: All events expired → empty list

- GIVEN all published events have `Date < DateTime.UtcNow`
- WHEN `GET /api/events` is called
- THEN the response is an empty array with 200 OK

#### Scenario: Mix of expired and active is order-independent

- GIVEN 5 events with dates interleaved past and future
- WHEN `GET /api/events` is called
- THEN only future-dated events are returned regardless of insertion order

### Requirement: EHE-003 — Public event detail returns 404 for expired events

`GetEventByIdAsync` invoked from the public endpoint `GET /api/events/{id}` MUST return null for expired events, producing a 404 response. A role-gated management variant (separate method or `includeExpired` parameter) MUST exist for organizer/admin use and MUST return the event regardless of expiry.

#### Scenario: Expired event returns 404 on public detail

- GIVEN an event with `Date < DateTime.UtcNow`
- WHEN `GET /api/events/{id}` is called (unauthenticated or buyer role)
- THEN the response is 404 Not Found

#### Scenario: Active event returns 200 on public detail

- GIVEN an event with `Date > DateTime.UtcNow`
- WHEN `GET /api/events/{id}` is called
- THEN the response is 200 with full event detail

#### Scenario: Same-day event after start time returns 404

- GIVEN an event with `Date = 2026-08-12T14:00:00Z` and current time is `2026-08-12T23:00:00Z`
- WHEN `GET /api/events/{id}` is called
- THEN the response is 404 Not Found

#### Scenario: Management variant returns expired event for organizer

- GIVEN an expired event owned by an organizer
- WHEN the organizer calls the management variant of `GetEventByIdAsync`
- THEN the response is 200 with full event detail including expiry status

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-001 | future-not-expired, past-is-expired, exact-instant-not-expired |
| EHE-002 | expired-absent-from-list, all-expired-empty-list, mix-order-independent |
| EHE-003 | expired-404-public-detail, active-200-public-detail, same-day-after-start-404, management-variant-returns-expired |
