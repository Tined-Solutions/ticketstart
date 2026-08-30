# Proposal: remove-organizer-delete-metrics

## Intent

Organizers must not be able to delete events, regardless of status — this is a real capability removal (UI + backend), not a cosmetic guard. Separately, the organizer per-event metrics view is redundant: exploration verified field-by-field that dashboard rows render a strict superset of `OrganizerEventMetrics` (which even drops status/location).

## Scope

### In Scope
- Remove "Eliminar" kebab item + delete flow (`deleteTarget`, `deleting`, delete feedback usage, `DeleteConfirmationDialog` usage) from `OrganizerDashboard.jsx` — without breaking the shared load/retry `feedback` path. Shared dialog component survives (Admin uses it).
- Block organizer `DELETE /api/events/{id}` at backend (option decision below). Explicit organizer/admin asymmetry: Admin keeps delete exactly as today, including the 409 past-event guard.
- Remove "Metricas" kebab item, `/organizer/events/:id/metrics` route, `OrganizerEventMetrics.jsx` + its test. **UI-only**: backend `GET /metrics/events/{id}`, `MetricsController`, `GetEventMetricsAsync`, `CalculateMetricsAsync` and metrics tests stay.
- Test updates per exploration: ~7 `OrganizerDashboard.test.jsx` cases, `OrganizerEventMetrics.test.jsx` deletion, `EventServiceTests.ByOwner` inversion, 5 `ImageStoragePropertyTests` role switches, Immutability/Controller 409 tests to Admin-only. `AdminPanel.test.jsx` delete tests pass unchanged (regression guard). Backend metrics tests unchanged.

### Out of Scope
- Retiring the per-event metrics endpoint/service (explicit non-goal per product decision).
- Any Admin delete change; `EventFinalizedGuard` behavior changes (Admin 409 on past events stays).
- Pre-existing branch test debt (`Checkout.test.jsx`, `identityValidation.test.js`).

## Capabilities

### New Capabilities
- `event-deletion`: Admin-only event deletion authority; organizer delete rejected (403) for any status.

### Modified Capabilities
- `role-access`: EHE-006 organizer dashboard loses Eliminar and Metricas entries (Ver stays); consultation paths unchanged.

## Approach

Backend delete-block options (from exploration):
1. **Service-level Admin-only guard** (recommended): `EventService.DeleteEventAsync` requires `UserRole.Admin`, throws `UnauthorizedAccessException` → 403. Smallest blast radius; `EventOwnership` policy untouched for its 5 other endpoints; matches the existing pattern where the service owns authorization validation; consistent with `EventFinalizedGuard` being service-level.
2. `[Authorize(Policy = "RequireAdminRole")]` swap on the DELETE endpoint: explicit at the gate, but changes the endpoint's auth identity and forces a rebuild if owner-scoped delete ever returns.

**Recommendation: Option 1** — service-level guard, per skill convention (aspnet-api-design: services own authorization validation) and single-point consistency with the past-events service guard.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/pages/OrganizerDashboard.jsx` | Modified | Remove delete flow + Metricas item; keep feedback load path |
| `frontend/src/pages/OrganizerEventMetrics.jsx` (+ test) | Removed | Redundant page |
| `frontend/src/App.jsx` | Modified | Drop metrics route |
| `frontend/src/pages/OrganizerDashboard.test.jsx` | Modified | ~7 cases updated/removed |
| `backend/Services/EventService.cs` (+ `IEventService.cs` doc) | Modified | Admin-only guard in `DeleteEventAsync` |
| Backend tests (`EventServiceTests`, `ImageStoragePropertyTests`, `EventServiceImmutabilityTests`, `EventControllerTests`) | Modified | Role inversions per exploration |
| `frontend/src/components/DeleteConfirmationDialog.jsx`, `AdminPanel.jsx` | Unchanged | Shared dialog survives |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Deleting shared `DeleteConfirmationDialog` breaks AdminPanel | Low | Explicit non-goal; AdminPanel tests as regression guard |
| Feedback state cleanup breaks load/retry feedback | Low | Only remove delete-driven usage |
| Dead endpoint: `GetEventMetricsAsync` has no controller test coverage | Accepted | Documented; API surface kept by product decision |
| "Regardless of status" misread as applying to Admin | Medium | Spec states organizer/admin asymmetry explicitly |

## Review Workload Forecast

Single PR; estimated ~450–650 changed lines (majority: deletions + test updates). Budget 2000 — comfortable headroom.

## Rollback Plan

Revert the single PR. Backend guard and UI removal are one commit; no data migrations, no config changes.

## Dependencies

- None external. Prior Panel redesign already committed (88c8fdb).

## Success Criteria

- [ ] Organizer sees no Eliminar/Metricas entries for any status; DELETE as organizer returns 403
- [ ] Admin delete flow unchanged (including 409 on past events)
- [ ] `GET /metrics/events/{id}` still returns 200 for owner/admin; organizer dashboard loads with intact load-error feedback
- [ ] All targeted frontend + backend suites green (pre-existing branch failures excluded)

## Open Questions for Design

- Option 1 vs 2 confirmation (recommendation: Option 1).
- Whether `EventController.DeleteEvent`'s audit-log branch or `IEventService` doc comments need a note clarifying the new Admin-only semantics.
