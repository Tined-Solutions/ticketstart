# Event Approval Specification

**Requirements covered**: EA-003 (MODIFIED), EA-004 (MODIFIED)

## Purpose

Organizer-created events MUST NOT reach the public catalog until an Admin approves them. Approve/reject MUST be blocked on past events: an Admin SHALL NOT approve or reject an event whose `Date` has passed, returning 409 `event-finalized` before any status or audit mutation (see `past-event-mutation-guard` PEM-002).

## MODIFIED Requirements

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

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EA-003 | admin-approves, approve-past-rejected, non-admin-rejected, unknown-event |
| EA-004 | reject-with-reason, reject-past-rejected, reject-without-reason, non-admin-rejected |
