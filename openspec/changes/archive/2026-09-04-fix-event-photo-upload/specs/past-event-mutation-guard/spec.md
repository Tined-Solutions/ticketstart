# Past Event Mutation Guard — Endpoint List Narrowed

**Requirements covered**: PEM-002 (MODIFIED)

## Purpose

The immutability guard keeps protecting every remaining event-mutation endpoint. `POST /events/{id}/image` is removed by `fix-event-photo-upload` EIM-006, so the guarded list drops from seven to six endpoints, and `PUT /events/{id}` — already listed and guarded (`EnsureMutable` before `SaveChanges`) — becomes the mutation that persists a replaced `imageUrl` uploaded via the new event-agnostic endpoint (EIM-002). The upload endpoint itself mutates no event, so the guard applies when its result is persisted, not at upload time. PEM-003 (no DB save/audit/notification on 409) is unchanged.

## MODIFIED Requirements

### Requirement: PEM-002: All six mutation endpoints reject past events

Each of the following MUST return 409 `event-finalized` when the target event is past, before any save/audit/notification: `PUT /events/{id}`; `DELETE /events/{id}`; `POST /admin/events/{id}/ticket-types/{ttId}/stock`; `POST /admin/events/{id}/ticket-types`; `POST /admin/events/{id}/approve`; `POST /admin/events/{id}/reject`.

(Archive-time clarification per `event-deletion` ED-001: the DELETE valid-requester set has narrowed to **Admin-only**. An organizer deleting any event — past events included — now receives **403 Forbidden** from the Admin-only service guard in `EventService.DeleteEventAsync`, which runs BEFORE the finalized guard — never 409. Admin + past event keeps the 409 `event-finalized` contract unchanged (ED-002).)

(Previously: seven endpoints — the list included `POST /events/{id}/image`, removed by `fix-event-photo-upload` EIM-006. `PUT /events/{id}` is now the mutation that persists a replaced `imageUrl`; the event-agnostic upload endpoint (EIM-002) is not itself guarded because it mutates no event.)

#### Scenario: Each mutation returns 409 on past event

- GIVEN a past event and a valid requester (owner or Admin; DELETE is Admin-only per `event-deletion` ED-001 — organizers receive 403 from the service guard before this 409)
- WHEN any of the six mutation endpoints is called
- THEN the response is 409 with `type: "event-finalized"` and title "Event has already finished"

#### Scenario: Response is RFC 7807 ProblemDetails

- GIVEN a rejected past-event mutation
- WHEN the response body is inspected
- THEN it is `application/problem+json` with `type`, `title`, `status: 409`, `detail`, and `instance`

#### Scenario: PUT persists a replaced imageUrl within the guard

- GIVEN a mutable (future) event and a PUT body carrying a new `imageUrl`
- WHEN `UpdateEventAsync` runs
- THEN `EnsureMutable` evaluates before `SaveChanges` and the new `imageUrl` is persisted with the other fields
- AND the previous image object is best-effort deleted after save (EIM-005)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| PEM-002 | each-mutation-409, rfc7807-problem-details, put-persists-replaced-image-url |