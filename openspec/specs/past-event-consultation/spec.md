# Past Event Consultation

**Requirements covered**: PEC-001 … PEC-004

## Purpose

Admin and Organizer MUST be able to **consult** (read-only) a past event: view its detail and ticket types. Today no admin/organizer read-only consultation view exists (`EventDetail` is public-only). This capability surfaces a "Ver" view for past events with no mutation affordances, while keeping purchases and metrics consultation available.

## Requirements

### Requirement: PEC-001: Read-only consultation view

The system MUST provide a read-only "Ver" consultation view accessible to both Admin and Organizer roles for any event (past or future). The view MUST render event detail and ticket types using the management fetch (`GET /events/{id}/manage`, includeExpired) and MUST NOT expose any mutation affordance.

#### Scenario: Admin and Organizer open "Ver" on a past event

- GIVEN a past event and a requester who is Admin or the owner
- WHEN the requester opens the "Ver" consultation view
- THEN the event detail and ticket types render read-only

#### Scenario: No mutation affordances

- GIVEN the consultation view is open
- WHEN it renders
- THEN inputs are disabled, and no submit or image-upload control is present

### Requirement: PEC-002: Management fetch reused

The consultation view MUST retrieve data through the management variant so past (`Pending`/`Rejected`/`Approved`) and expired events return 200. It MUST NOT use the public `GET /events/{id}` path.

#### Scenario: Past unapproved event loads

- GIVEN a past event with `Status != Approved`
- WHEN the consultation view fetches it via the management variant
- THEN the response is 200 with full event data

#### Scenario: Non-authorized requester denied

- GIVEN a user who is neither Admin nor the event owner
- WHEN they open the consultation view
- THEN access is denied (role/ownership policy applies)

### Requirement: PEC-003: Consultation does not mutate

The consultation view MUST NOT perform any mutation, save, audit, or notification. It is strictly read-only.

#### Scenario: Viewing causes no side-effects

- GIVEN a past event opened in the consultation view
- WHEN the view loads and is closed
- THEN no event, audit, or notification change occurs

### Requirement: PEC-004: Purchases and metrics consultation preserved

On past-event rows, the purchases ("Compras") entry (Admin) MUST remain enabled and functional. The organizer metrics ("Metricas") entry MUST NOT appear on any organizer dashboard row (it was removed change-wide — see `role-access` EHE-006); organizer per-event metrics remain available only through the backend `GET /metrics/events/{id}` for owner/Admin.
(Previously: both the Admin "Compras" entry and the organizer "Metricas" entry were required to remain enabled and functional on past rows; the organizer Metricas entry no longer exists on any row.)

#### Scenario: Compras stays enabled on past row

- GIVEN a past event row in AdminPanel
- WHEN the row renders
- THEN the "Compras" action is enabled and navigates to purchases

#### Scenario: Metricas entry no longer present on past row

- GIVEN a past event row in OrganizerDashboard
- WHEN the row renders
- THEN no "Metricas" action is present (and no dead navigation target remains)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| PEC-001 | roles-open-ver, no-mutation-affordances |
| PEC-002 | past-unapproved-loads, non-authorized-denied |
| PEC-003 | no-side-effects |
| PEC-004 | compras-enabled, metricas-entry-absent |
