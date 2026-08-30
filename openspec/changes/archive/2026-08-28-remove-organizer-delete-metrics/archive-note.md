# Archive-Time Note (remove-organizer-delete-metrics)

## PEM-002 one-line clarification delta required at archive

When syncing the `past-event-mutation-guard` delta into the main spec corpus
(`openspec/specs/.../past-event-mutation-guard/spec.md`), apply a one-line
clarification:

- **Where**: the PEM-002 requirement text/scenario listing `DELETE /api/events/{id}`
  among the 409 `event-finalized` endpoints (main spec.md L35 area), and the
  scenario wording "valid requester (owner or Admin)" (L39 area).
- **Clarification**: DELETE's valid-requester set has narrowed to **Admin-only**
  per `event-deletion` ED-001 (this change). An organizer deleting any event —
  past events included — now receives **403 Forbidden** (from the Admin-only
  service guard in `EventService.DeleteEventAsync`, which runs BEFORE the
  finalized guard), never 409. Admin + past event keeps the 409
  `event-finalized` contract unchanged (ED-002).
- **Source of truth**: `specs/event-deletion/spec.md` (ED-001/ED-002) and
  `specs/role-access/spec.md` (EHE-006 "cannot mutate a past event" scenario,
  delete half).

Non-delete mutations (PUT, image upload, stock/type, approve/reject) are
unaffected and still return 409 for organizers on past events.
