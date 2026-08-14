# Catalog Filtering — Expired Event Exclusion

**Requirements covered**: EHE-002 (MODIFIED), EHE-003 (MODIFIED)

## Purpose

Buyers MUST NOT see expired OR unapproved events in the public catalog. The system applies a single `IsExpired` predicate AND a `Status == Approved` filter at the DB query level so that `GET /api/events` and `GET /api/events/{id}` (public) never surface past or pending/rejected events, while a management variant preserves organizer/admin access regardless of status (EHE-006).

## MODIFIED Requirements

### Requirement: EHE-002 — Public event list excludes expired and unapproved events

`GetAllPublishedEventsAsync` MUST apply a `Where(e => !e.IsExpired(DateTime.UtcNow))` filter at the DB query level so expired events never appear in `GET /api/events`. It MUST additionally apply `Where(e => e.Status == EventStatus.Approved)` so pending and rejected events never appear either. Both filters MUST be order-independent and MUST NOT affect approved, non-expired events.
(Previously: filtered only expired events; no status filter existed.)

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

#### Scenario: Pending event absent from public list

- GIVEN an event with `Status == Pending` and a future date
- WHEN `GET /api/events` is called
- THEN the response does NOT contain the pending event

#### Scenario: Rejected event absent from public list

- GIVEN an event with `Status == Rejected` and a future date
- WHEN `GET /api/events` is called
- THEN the response does NOT contain the rejected event

### Requirement: EHE-003 — Public event detail returns 404 for expired or unapproved events

`GetEventByIdAsync` invoked from the public endpoint `GET /api/events/{id}` MUST return null for expired events, producing a 404 response. The public endpoint MUST also produce 404 for events with `Status != Approved` (pending or rejected). A role-gated management variant (separate method or `includeExpired`/`includeNonApproved` parameter) MUST exist for organizer/admin use and MUST return the event regardless of expiry and regardless of status.
(Previously: 404 only for expired events; no status check existed.)

#### Scenario: Expired event returns 404 on public detail

- GIVEN an event with `Date < DateTime.UtcNow`
- WHEN `GET /api/events/{id}` is called (unauthenticated or buyer role)
- THEN the response is 404 Not Found

#### Scenario: Active event returns 200 on public detail

- GIVEN an event with `Date > DateTime.UtcNow` and `Status == Approved`
- WHEN `GET /api/events/{id}` is called
- THEN the response is 200 with full event detail

#### Scenario: Same-day event after start time returns 404

- GIVEN an event with `Date = 2026-08-12T14:00:00Z` and current time is `2026-08-12T23:00:00Z`
- WHEN `GET /api/events/{id}` is called
- THEN the response is 404 Not Found

#### Scenario: Pending event returns 404 on public detail

- GIVEN an event with `Status == Pending` and a future date
- WHEN `GET /api/events/{id}` is called (unauthenticated or buyer role)
- THEN the response is 404 Not Found

#### Scenario: Rejected event returns 404 on public detail

- GIVEN an event with `Status == Rejected` and a future date
- WHEN `GET /api/events/{id}` is called (unauthenticated or buyer role)
- THEN the response is 404 Not Found

#### Scenario: Management variant returns unapproved event for organizer

- GIVEN a pending or rejected event owned by an organizer
- WHEN the organizer calls the management variant of `GetEventByIdAsync`
- THEN the response is 200 with full event detail including its status

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-002 | expired-absent-from-list, all-expired-empty-list, mix-order-independent, pending-absent, rejected-absent |
| EHE-003 | expired-404-public-detail, active-200-public-detail, same-day-after-start-404, pending-404, rejected-404, management-variant-returns-unapproved |
