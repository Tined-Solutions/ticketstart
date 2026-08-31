# Role Access — Preserved Access to Past Events

**Requirements covered**: EHE-006, EHE-007, EHE-008

## Purpose

Existing role-gated access paths MUST remain fully functional for past events AND for events in any approval status. Organizers MUST continue to VIEW their events — including `Pending` and `Rejected` ones — staff MUST continue scanning QR codes, and buyers MUST continue retrieving their purchased tickets, regardless of event expiry or approval status. Organizers MAY also scan QR codes on the same surface as staff (see EHE-007). Consultation of past events is preserved; MUTATION of past events is revoked for both Admin and Organizer (see `past-event-mutation-guard`).

## Requirements

### Requirement: EHE-006 — Organizer endpoints include past and unapproved events

Organizer endpoints (`OrganizerDashboard`, `OrganizerEventDetail`, `MetricsService.GetOrganizerMetricsAsync`) MUST NOT apply the expired-event filter and MUST NOT apply any approval-status filter. Organizers SHALL see their past events exactly as before (listing and metrics unchanged), and SHALL see their `Pending`/`Rejected` events in the dashboard so they can track moderation state. Organizers SHALL NOT mutate a past event: a past event is read-only (see `past-event-mutation-guard` PEM-002); consultation via the management variant remains available; deletion is additionally revoked for organizers at any age (403 — see `event-deletion` ED-001). The management variant from EHE-003 MUST be used for event detail retrieval in organizer context. The organizer dashboard Edit entry MUST be hidden for organizers (UI-only; backend `EventOwnership` edit authority unchanged). The organizer dashboard "Eliminar" and "Metricas" kebab entries MUST be removed for every row regardless of status; the "Ver" entry MUST remain. The per-event metrics page (`OrganizerEventMetrics`) and its `/organizer/events/:id/metrics` route MUST be removed from the frontend; `GET /metrics/events/{id}` MUST remain functional for owner and Admin. Removing the dashboard's delete flow MUST NOT break the shared load/retry error-feedback path.
(Previously: the organizer dashboard offered "Eliminar" and "Metricas" kebab entries and a per-event metrics page for every row; deletion authority and the metrics UI are now removed while consultation and aggregate metrics stay.)

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
- THEN non-delete mutations return 409 `event-finalized` and no change is persisted
- AND `DELETE /api/events/{id}` returns 403 instead (see `event-deletion` ED-001)

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

#### Scenario: Organizer dashboard hides Eliminar and Metricas entries

- GIVEN an organizer viewing their own dashboard with rows in any status (including past)
- WHEN a row's action menu renders
- THEN no "Eliminar" and no "Metricas" entry appears
- AND the "Ver" entry remains available

#### Scenario: Organizer per-event metrics route no longer resolves

- GIVEN this change's frontend build
- WHEN a user navigates to `/organizer/events/:id/metrics`
- THEN no metrics page renders (route no longer registered; `OrganizerEventMetrics` is removed)

#### Scenario: Per-event metrics endpoint still works for owner

- GIVEN an organizer who owns event E
- WHEN the owner calls `GET /metrics/events/{E}`
- THEN the response is 200 with metrics data (backend unchanged)

#### Scenario: Per-event metrics endpoint still works for admin

- GIVEN an Admin and any event
- WHEN the Admin calls `GET /metrics/events/{id}`
- THEN the response is 200 with metrics data

#### Scenario: Load-error feedback survives delete-flow removal

- GIVEN the organizer dashboard's data load fails
- WHEN the error state renders
- THEN the user sees the load-error feedback and can retry
- AND retry re-triggers the load (the shared feedback path is intact)

### Requirement: EHE-007 — Staff scan includes past events

Staff scan endpoint(s) used by `StaffScan.jsx` MUST NOT apply the expired-event filter. Staff SHALL scan QR codes for past events. The staff role-gated path MUST include expired events in its query scope. The scan surface — the scannable event list (`GET /api/events/manage`) and QR validation (`POST /api/tickets/validate`) — MUST admit Staff, Organizador and Admin (`RequireScanAccessRole`): organizers scan as staff, with the same scope as staff (any scannable event).

#### Scenario: Staff scans ticket for past event

- GIVEN a valid ticket QR for an event with `Date < DateTime.UtcNow`
- WHEN a staff member scans the QR via the staff scan endpoint
- THEN the response is 200 with ticket and attendee details

#### Scenario: Staff scan list includes past events

- GIVEN a staff member with access to multiple events
- WHEN the staff scan page loads its event list
- THEN past events appear alongside active events (unfiltered)

#### Scenario: Organizer accesses the scan surface

- GIVEN an authenticated organizer
- WHEN the organizer loads the staff scan page and posts a QR validation
- THEN the scannable event list returns 200 and the validate endpoint does not return 403

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
| EHE-006 | organizer-dashboard-lists-past, organizer-consults-past, organizer-cannot-mutate-past, organizer-metrics-include-past, dashboard-lists-pending-rejected, opens-pending-detail, dashboard-hides-edit, dashboard-hides-eliminar-metricas, metrics-route-unresolved, per-event-metrics-owner-200, per-event-metrics-admin-200, load-error-feedback-survives |
| EHE-007 | staff-scan-past-event-ticket, staff-scan-list-includes-past, organizer-accesses-scan-surface |
| EHE-008 | buyer-ticket-lookup-past-event, my-tickets-lists-past, qr-valid-past-event |
