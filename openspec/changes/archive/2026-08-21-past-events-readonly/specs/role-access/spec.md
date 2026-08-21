# Role Access — Preserved Access to Past Events

**Requirements covered**: EHE-006 (MODIFIED)

## Purpose

Existing role-gated access paths MUST remain fully functional for past events AND for events in any approval status. Organizers MUST continue to VIEW their events — including `Pending` and `Rejected` ones — staff MUST continue scanning QR codes, and buyers MUST continue retrieving their purchased tickets, regardless of event expiry or approval status. Consultation of past events is preserved; MUTATION of past events is revoked for both Admin and Organizer (see `past-event-mutation-guard`).

## MODIFIED Requirements

### Requirement: EHE-006 — Organizer endpoints include past and unapproved events

Organizer endpoints (`OrganizerDashboard`, `OrganizerEventDetail`, `MetricsService.GetOrganizerMetricsAsync`) MUST NOT apply the expired-event filter and MUST NOT apply any approval-status filter. Organizers SHALL see their past events exactly as before (listing and metrics unchanged), and SHALL see their `Pending`/`Rejected` events in the dashboard so they can track moderation state. Organizers SHALL NOT mutate a past event: a past event is read-only (see `past-event-mutation-guard` PEM-002); consultation via the management variant remains available. The management variant from EHE-003 MUST be used for event detail retrieval in organizer context. The organizer dashboard Edit entry MUST be hidden for organizers (UI-only; backend `EventOwnership` edit authority unchanged).
(Previously: organizers could "see AND edit their past events exactly as before"; the edit authority on past events is now revoked, consultation preserved.)

#### Scenario: Organizer dashboard lists past events

- GIVEN an organizer with 2 past events and 1 future event
- WHEN the organizer calls the dashboard endpoint
- THEN all 3 events are returned (unfiltered)

#### Scenario: Organizer consults a past event via management variant

- GIVEN an organizer owns an event with `Date < DateTime.UtcNow`
- WHEN the organizer opens the event detail via the management variant
- THEN the response is 200 with full event data

#### Scenario: Organizer cannot mutate a past event

- GIVEN an organizer owns an event with `Date < DateTime.UtcNow`
- WHEN the organizer attempts any mutation endpoint on it
- THEN the response is 409 `event-finalized` and no change is persisted

#### Scenario: Organizer metrics include past events

- GIVEN an organizer with past events that have sales data
- WHEN `GetOrganizerMetricsAsync` is called
- THEN metrics include all events regardless of date

#### Scenario: Organizer dashboard lists pending and rejected events

- GIVEN an organizer with one `Pending`, one `Rejected`, and one `Approved` event
- WHEN the organizer calls the dashboard endpoint
- THEN all 3 events are returned
- AND each includes its `Status` for badge rendering

#### Scenario: Organizer opens a pending event detail

- GIVEN an organizer owns a `Pending` event
- WHEN the organizer opens the event detail via the management variant
- THEN the response is 200 with full event data and `Status == Pending`

#### Scenario: Organizer dashboard hides Edit entry

- GIVEN an organizer viewing their own dashboard
- WHEN the dashboard renders event rows
- THEN no Edit entry appears for the organizer role (admin keeps Edit)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-006 | organizer-dashboard-lists-past, organizer-consults-past, organizer-cannot-mutate-past, organizer-metrics-include-past, dashboard-lists-pending-rejected, opens-pending-detail, dashboard-hides-edit |
