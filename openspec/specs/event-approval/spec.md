# Event Approval Specification

**Requirements covered**: EA-001 … EA-010

## Purpose

Organizer-created events MUST NOT reach the public catalog until an Admin approves them. Every new event starts in `Pending`; Admins approve or reject; only `Approved` events are buyer-visible (see catalog-filtering delta). Backend edit paths stay intact — approval is a moderation gate, not an ownership change. Approve/reject MUST be blocked on past events: an Admin SHALL NOT approve or reject an event whose `Date` has passed, returning 409 `event-finalized` before any status or audit mutation (see `past-event-mutation-guard` PEM-002).

## Non-Goals

Email rejection notice (handled externally), mandatory rejection reason, automated re-submit workflow, revoking organizer edit authority at the API level (`EventOwnership` unchanged — UI-only hide).

## Requirements

### Requirement: EA-001: EventStatus enum and Event.Status column

The system SHALL introduce `EventStatus` (`Pending | Approved | Rejected`) following the `ReservationStatus`/`TransactionStatus` pattern, and SHALL add `Event.Status` mapped in `OnModelCreating` with a DB default of `Pending`. The column MUST be nullable-safe only for legacy rows (pre-backfill); all new writes MUST set an explicit value.

#### Scenario: Enum exists with exactly three members

- GIVEN the domain model
- WHEN `EventStatus` is inspected
- THEN it exposes `Pending`, `Approved`, and `Rejected`

#### Scenario: Schema change is a manual EF migration

- GIVEN the codebase convention of manual migrations
- WHEN a new migration is generated
- THEN it adds `Status` with default `Pending` and does not auto-migrate at startup

### Requirement: EA-002: New events created as Pending

`CreateEventAsync` MUST set `Status = EventStatus.Pending` on every new event, regardless of the requesting organizer's role. No client input MAY override the initial status.

#### Scenario: Organizer creates event

- GIVEN an organizer submitting a valid `CreateEventRequest`
- WHEN `CreateEventAsync` persists the event
- THEN the returned event has `Status == Pending`

#### Scenario: Status not client-settable

- GIVEN a request payload that attempts to include a status
- WHEN the event is created
- THEN the persisted status is `Pending` (the supplied value is ignored)

### Requirement: EA-003: Admin approve endpoint

The system MUST provide `POST /api/admin/events/{eventId}/approve`, protected by `RequireAdminRole` (inherited class-level on `AdminController`). It MUST set `Status = Approved` and write an audit entry via `TryLogAuditAsync` with a new `AuditActionType.ApproveEvent` member and `AuditResourceType.Event`. It MUST reject a past event (`Date < now`) with 409 `event-finalized` BEFORE any status or audit mutation. Success returns 200 with the updated event summary.
(Previously: no date guard — approving a past event was allowed.)

#### Scenario: Admin approves pending event

- GIVEN an event with `Status == Pending` and a future `Date`
- WHEN an admin calls `POST /api/admin/events/{eventId}/approve`
- THEN the response is 200
- AND the event `Status` becomes `Approved`
- AND an audit entry with `ApproveEvent` is written

#### Scenario: Approve past event rejected

- GIVEN an event with `Date < now`
- WHEN an admin calls the approve endpoint
- THEN the response is 409 with `type: "event-finalized"`
- AND the `Status` is unchanged and no audit entry is written

#### Scenario: Non-admin rejected

- GIVEN a Staff or Organizer user
- WHEN they call the approve endpoint
- THEN the response is 403 Forbidden

#### Scenario: Unknown event

- GIVEN an `eventId` that does not exist
- WHEN an admin calls the approve endpoint
- THEN the response is 404 and no audit entry is written

### Requirement: EA-004: Admin reject endpoint

The system MUST provide `POST /api/admin/events/{eventId}/reject`, protected by `RequireAdminRole`. Rejection reason is OPTIONAL (MAY be `null`/omitted) and MUST NOT be mandatory. It MUST set `Status = Rejected` and write an audit entry via `TryLogAuditAsync` with a new `AuditActionType.RejectEvent` member. It MUST reject a past event (`Date < now`) with 409 `event-finalized` BEFORE any status or audit mutation. Success returns 200 with the updated event summary.
(Previously: no date guard — rejecting a past event was allowed.)

#### Scenario: Admin rejects with optional reason

- GIVEN an event with `Status == Pending`, a future `Date`, and an optional reason string
- WHEN an admin calls `POST /api/admin/events/{eventId}/reject`
- THEN the response is 200
- AND the event `Status` becomes `Rejected`
- AND the audit entry includes the reason (truncated to ≤ 1000 chars)

#### Scenario: Reject past event rejected

- GIVEN an event with `Date < now`
- WHEN an admin calls the reject endpoint
- THEN the response is 409 with `type: "event-finalized"`
- AND the `Status` is unchanged and no audit entry is written

#### Scenario: Admin rejects without reason

- GIVEN an event with `Status == Pending`, a future `Date`, and no reason supplied
- WHEN an admin calls the reject endpoint
- THEN the response is 200 and the event `Status` becomes `Rejected`

#### Scenario: Non-admin rejected

- GIVEN a Staff or Organizer user
- WHEN they call the reject endpoint
- THEN the response is 403 Forbidden

### Requirement: EA-005: Admin may flip any status

An Admin SHALL be able to transition any event between any two statuses (`Pending ↔ Approved ↔ Rejected`) without a mandatory reason and without an automated re-submit workflow. Approving a `Rejected` event MUST return it to the public catalog; rejecting an `Approved` event MUST hide it.

#### Scenario: Approve after reject re-publishes

- GIVEN an event with `Status == Rejected`
- WHEN an admin calls approve
- THEN the event `Status` becomes `Approved`

#### Scenario: Reject after approve hides event

- GIVEN an event with `Status == Approved`
- WHEN an admin calls reject
- THEN the event `Status` becomes `Rejected`

#### Scenario: No transition is blocked by workflow

- GIVEN any event in any status
- WHEN an admin issues approve or reject
- THEN the call succeeds (no state-machine rejection) unless the event does not exist

### Requirement: EA-006: Backfill existing events to Approved

The migration `Up()` MUST backfill ALL pre-existing events (including expired ones) to `Status = Approved` using the repo pattern `ApplicationDbContextFactory().CreateDbContext(null)` with a best-effort `try/catch`. A backfill failure MUST NOT fail the migration silently halting it; the process SHALL log the error and continue.

#### Scenario: Existing events become Approved

- GIVEN a database with events created before this migration
- WHEN the migration `Up()` runs
- THEN every pre-existing event has `Status == Approved`

#### Scenario: Backfill failure is best-effort

- GIVEN the backfill context creation or save throws
- WHEN the migration `Up()` runs
- THEN the migration continues (error logged) and does not abort

### Requirement: EA-007: Status exposed in DTOs

`EventSummary` (admin), `EventMetrics` (organizer dashboard), and `EventWithAvailability` (public + create response) SHALL include the event `Status` so consumers can render badges and moderation state without extra queries.

#### Scenario: Admin event summary includes status

- GIVEN `GetAllEventsAsync` returns `EventSummary` items
- WHEN the admin events list is loaded
- THEN each item includes its `Status` value

#### Scenario: Organizer metrics include status

- GIVEN `GetOrganizerMetricsAsync` returns `EventMetrics` items
- WHEN the organizer dashboard is loaded
- THEN each item includes its `Status` value

### Requirement: EA-008: Admin UI — pending count and actions

`AdminPanel` MUST show a pending-count badge, a status `Badge` per event (`pending=warning`, `approved=success`, `rejected=error`), and per-row Approve/Reject actions for non-`Approved`/non-`Rejected` events respectively. Success MUST invalidate the admin events query; failure MUST show an error without mutating local state.

#### Scenario: Pending count shown and actions work

- GIVEN an admin viewing the events section with pending events
- WHEN the panel loads
- THEN the pending count badge is visible
- AND each pending row offers Approve and Reject actions

#### Scenario: Approve succeeds and refreshes list

- GIVEN an admin clicks Approve on a pending event
- WHEN the request succeeds
- THEN the events query is invalidated and the row shows the `Approved` badge

#### Scenario: Action failure shows error

- GIVEN the backend returns an error on approve/reject
- WHEN the admin panel receives it
- THEN the admin sees an error and local state is unchanged

### Requirement: EA-009: Organizer dashboard status badge and post-create copy

`OrganizerDashboard` MUST render a status `Badge` per event using the same variant mapping. `EventForm` (create mode) MUST show "pendiente de aprobación" copy after successful creation. The Edit entry in the organizer dashboard MUST be hidden for organizers (admin keeps it); this is UI-only — backend `EventOwnership` is unchanged.

#### Scenario: Dashboard shows status badge

- GIVEN an organizer with events in various statuses
- WHEN the dashboard renders
- THEN each event row shows the matching status badge

#### Scenario: Create shows pending-approval copy

- GIVEN an organizer submits a valid create form
- WHEN the create request succeeds
- THEN the UI communicates the event is pending approval

#### Scenario: Edit entry hidden for organizers

- GIVEN an organizer viewing their own dashboard
- WHEN the dashboard renders
- THEN no Edit entry appears for the organizer role

### Requirement: EA-010: Test coverage

Backend MUST follow strict TDD: `EventServiceTests` (create → Pending, DTO status), `EventControllerTests` (public detail 404 for non-approved), `AdminControllerTests` (approve/reject 200/403/404 + audit), `MetricsControllerTests` (status in `EventMetrics`). Frontend MUST cover `OrganizerDashboard`, `AdminPanel`, and `EventForm` (badge variants, pending count, approve/reject actions, pending copy).

#### Scenario: Suite stays green

- GIVEN the implemented change
- WHEN `dotnet test` (backend) and `npx vitest run` (frontend) run
- THEN new tests pass and existing tests are unaffected

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EA-001 | enum-three-members, manual-migration |
| EA-002 | organizer-creates-pending, status-not-client-settable |
| EA-003 | admin-approves, approve-past-rejected, non-admin-rejected, unknown-event |
| EA-004 | reject-with-reason, reject-past-rejected, reject-without-reason, non-admin-rejected |
| EA-005 | approve-after-reject, reject-after-approve, no-blocked-transition |
| EA-006 | existing-become-approved, backfill-best-effort |
| EA-007 | admin-summary-status, organizer-metrics-status |
| EA-008 | pending-count-actions, approve-refreshes, action-failure |
| EA-009 | dashboard-badge, pending-copy, edit-hidden-organizer |
| EA-010 | suite-green |
