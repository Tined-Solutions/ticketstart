# Role Access — Preserved Access to Past Events

**Requirements covered**: EHE-006, EHE-007, EHE-008

## Purpose

Existing role-gated access paths MUST remain fully functional for past events AND for events in any approval status. Organizers MUST continue to view/edit their events — including `Pending` and `Rejected` ones — staff MUST continue scanning QR codes, and buyers MUST continue retrieving their purchased tickets, regardless of event expiry or approval status.

## Requirements

### Requirement: EHE-006 — Organizer endpoints include past and unapproved events

Organizer endpoints (`OrganizerDashboard`, `OrganizerEventDetail`, `MetricsService.GetOrganizerMetricsAsync`) MUST NOT apply the expired-event filter and MUST NOT apply any approval-status filter. Organizers SHALL see and edit their past events exactly as before, and SHALL see their `Pending`/`Rejected` events in the dashboard so they can track moderation state. The management variant from EHE-003 MUST be used for event detail retrieval in organizer context. The organizer dashboard Edit entry MUST be hidden for organizers (UI-only; backend `EventOwnership` edit authority unchanged).

#### Scenario: Organizer dashboard lists past events

- GIVEN an organizer with 2 past events and 1 future event
- WHEN the organizer calls the dashboard endpoint
- THEN all 3 events are returned (unfiltered)

#### Scenario: Organizer edits a past event

- GIVEN an organizer owns an event with `Date < DateTime.UtcNow`
- WHEN the organizer opens the event detail via the management variant
- THEN the response is 200 with full event data
- AND the organizer can submit edits successfully

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

### Requirement: EHE-007 — Staff scan includes past events

Staff scan endpoint(s) used by `StaffScan.jsx` MUST NOT apply the expired-event filter. Staff SHALL scan QR codes for past events. The staff role-gated path MUST include expired events in its query scope.

#### Scenario: Staff scans ticket for past event

- GIVEN a valid ticket QR for an event with `Date < DateTime.UtcNow`
- WHEN a staff member scans the QR via the staff scan endpoint
- THEN the response is 200 with ticket and attendee details

#### Scenario: Staff scan list includes past events

- GIVEN a staff member with access to multiple events
- WHEN the staff scan page loads its event list
- THEN past events appear alongside active events (unfiltered)

### Requirement: EHE-008 — Buyer ticket lookup unaffected for past events

`TicketLookup` and "My Tickets" buyer endpoints MUST remain unaffected by expiry filtering. A buyer who already purchased tickets to a now-expired event SHALL still retrieve their tickets and QR codes. These endpoints MUST NOT apply the `IsExpired` filter.

#### Scenario: Buyer retrieves tickets for past event

- GIVEN a buyer with a purchased ticket for an event with `Date < DateTime.UtcNow`
- WHEN the buyer calls TicketLookup with their reservation email
- THEN the response includes the ticket with valid QR data

#### Scenario: My Tickets lists past event tickets

- GIVEN a logged-in buyer with tickets for both past and future events
- WHEN the buyer calls "My Tickets"
- THEN all tickets are returned regardless of event date

#### Scenario: QR code remains valid for past event entry

- GIVEN a ticket QR generated for a past event
- WHEN the QR payload is decoded
- THEN it contains valid HMAC-signed data (QR validity is independent of event date)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EHE-006 | organizer-dashboard-lists-past, organizer-edits-past-event, organizer-metrics-include-past, dashboard-lists-pending-rejected, opens-pending-detail, dashboard-hides-edit |
| EHE-007 | staff-scan-past-event-ticket, staff-scan-list-includes-past |
| EHE-008 | buyer-ticket-lookup-past-event, my-tickets-lists-past, qr-valid-past-event |
