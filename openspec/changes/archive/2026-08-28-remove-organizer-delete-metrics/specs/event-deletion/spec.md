# Event Deletion

**Requirements covered**: ED-001 … ED-003

## Purpose

Deleting an event (`DELETE /api/events/{id}`) is an **Admin-only** authority. Organizers MUST NOT be able to delete an event — theirs or anyone else's — regardless of status or age. The revocation is a real capability removal (UI + backend), enforced at the backend by a service-level Admin-only guard in `EventService.DeleteEventAsync` (the same convention as `UpdateEventAsync`), not merely a hidden UI entry. The organizer/admin asymmetry is explicit and normative: Admin keeps deletion exactly as today, including the 409 `event-finalized` past-event guard (`past-event-mutation-guard` PEM-002).

## Requirements

### Requirement: ED-001: Organizer delete is rejected for any status

The system MUST reject `DELETE /api/events/{id}` with **403 Forbidden** when the requester is an organizer, regardless of the event's status (`Pending`, `Approved`, `Rejected`), age, or ownership. The Admin-only authority guard MUST live at the service level (`EventService.DeleteEventAsync`) and MUST run before the finalized-event guard, so an organizer NEVER receives 409 `event-finalized` from delete — authorization is decided first. The rejection MUST occur before any side-effect (no entity removal, no image cleanup). The `EventOwnership` policy and its other endpoints MUST remain unchanged.

#### Scenario: Organizer deletes a draft event

- GIVEN an organizer owns an event with `Status == Pending`
- WHEN the organizer calls `DELETE /api/events/{id}`
- THEN the response is 403 and the event still exists

#### Scenario: Organizer deletes an active approved event

- GIVEN an organizer owns an event with `Status == Approved` and `Date` in the future
- WHEN the organizer calls `DELETE /api/events/{id}`
- THEN the response is 403 and the event still exists

#### Scenario: Organizer deletes a past event — 403, not 409

- GIVEN an organizer owns an event with `Date < DateTime.UtcNow`
- WHEN the organizer calls `DELETE /api/events/{id}`
- THEN the response is 403 (the Admin-only guard precedes PEM-002's finalized guard)
- AND the event still exists

#### Scenario: Rejected delete has no side effects

- GIVEN an organizer calls `DELETE /api/events/{id}` on any event
- WHEN the 403 is returned
- THEN no event row, audit entry, or image-storage deletion occurred

### Requirement: ED-002: Admin delete authority unchanged (explicit asymmetry)

Admin SHALL retain deletion exactly as today: an active event deletes successfully (204 No Content, audit entry, image cleanup), and a past event is rejected with 409 `event-finalized` per `past-event-mutation-guard` PEM-002. The asymmetry is normative: for a past event, organizer → 403, Admin → 409.

#### Scenario: Admin deletes an active event

- GIVEN an Admin and an event with `Date` in the future
- WHEN the Admin calls `DELETE /api/events/{id}`
- THEN the response is 204 No Content and the event is deleted (unchanged contract)

#### Scenario: Admin deletes a past event

- GIVEN an Admin and an event with `Date < DateTime.UtcNow`
- WHEN the Admin calls `DELETE /api/events/{id}`
- THEN the response is 409 `event-finalized` (RFC 7807) and the event is not deleted (unchanged)

### Requirement: ED-003: Admin delete UI regression guard

The AdminPanel delete flow and the shared `DeleteConfirmationDialog` component MUST remain functional and untouched; only the organizer dashboard usage of the dialog is removed.

#### Scenario: AdminPanel delete flow survives

- GIVEN an Admin viewing the AdminPanel
- WHEN the Admin deletes an event through the panel
- THEN the shared `DeleteConfirmationDialog` opens, confirmation deletes the event, and the list refreshes

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| ED-001 | organizer-delete-draft-403, organizer-delete-active-403, organizer-delete-past-403, rejected-delete-no-side-effects |
| ED-002 | admin-delete-active, admin-delete-past-409 |
| ED-003 | adminpanel-delete-flow-survives |
