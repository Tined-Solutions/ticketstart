# Design: remove-organizer-delete-metrics

## Technical Approach

Two independent removals shipped as one PR: (1) a service-level Admin-only guard in `EventService.DeleteEventAsync` (Option 1 confirmed — matches `UpdateEventAsync`'s service-owns-authorization convention; `EventOwnership` policy untouched for its 5 other endpoints), and (2) frontend removal of the organizer delete flow and the redundant per-event metrics page. Backend metrics endpoint stays (UI-only removal).

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | Backend guard | Replace the owner-or-admin condition (EventService.cs:615) with `userRole != UserRole.Admin` → `UnauthorizedAccessException("Only administrators can delete events")` | `[Authorize(Policy="RequireAdminRole")]` endpoint swap | Single-point consistency with the existing service-guard pattern; keeps `EventOwnership` identity for the other endpoints; controller already maps `UnauthorizedAccessException` → `Forbid()` (403, EventController.cs:200) — zero controller code change |
| 2 | Guard ordering | Guard stays in the ownership-check slot (line ~615), i.e. BEFORE `EventFinalizedGuard.EnsureMutable` (line 624). **No reordering needed** — verified current order is already 404 → 403 → 409 | Moving role check before FindAsync | ED-001 precedence (organizer never sees 409 from delete) is satisfied in-place; 404-for-missing-event semantics preserved |
| 3 | Audit branch & docs | KEEP controller audit branch `if (userRole == UserRole.Admin)` unchanged (success now implies Admin; today's organizer deletes were already unaudited — audit coverage strictly improves) + one clarifying comment. UPDATE XML docs on `IEventService.DeleteEventAsync` (summary + `<exception>`) and impl doc to "Admin-only (ED-001)" | Removing the now-always-true audit condition | Defensive branch survives a future owner-delete restoration; docs must not lie about authority |
| 4 | Empty kebab | Hide the entire `DropdownMenu` for organizers (`{canEdit && (...)}`); admin kebab = `[Editar]` only | Rendering an empty "Acciones" menu for organizers | Dead trigger opening an empty panel is broken UX; spec requires Ver to remain (it's a standalone button, untouched) |
| 5 | Metrics keep-alive test | ADD one `WebApplicationFactory` integration test (owner 200 + admin 200) in `MetricsControllerTests.cs` | Defer | Exploration's "no controller coverage" was imprecise: mocked unit tests exist (200/404/401/500) but bypass the `EventOwnership` pipeline. EHE-006 scenarios demand owner/admin-200 proof; endpoint is now UI-less, so pipeline coverage is the only bit-rot guard |

Guard snippet (replaces lines 614-620):

```csharp
// ED-001: deletion is Admin-only — organizers lose delete authority for ANY
// event (any status/age). Runs BEFORE the finalized guard so an organizer
// never receives 409 from delete; no side effects can occur past this point.
if (userRole != UserRole.Admin)
{
    _logger.LogWarning("User {UserId} (role {UserRole}) denied delete of event {EventId} — Admin-only (ED-001)", userId, userRole, eventId);
    throw new UnauthorizedAccessException("Only administrators can delete events");
}
```

`userId` remains in the signature (logging only; interface unchanged).

## Data Flow

    Organizer ──DELETE──▶ EventController (EventOwnership) ─▶ EventService: 404? → Admin? ─403──▶ Forbid()
                                                                        │ Admin
                                                                        ▼
                                                          EventFinalizedGuard ─409─▶ ProblemDetails
                                                                        │ future
                                                                        ▼ Remove + Save + R2 cleanup → 204

Frontend: dashboard rows keep `GET /metrics/organizer`; `/organizer/events/:id/metrics` route unregistered → falls through to existing `*` → `NotFound` (no new UX).

## File Changes

| File | Action | Change |
|------|--------|--------|
| `backend/Services/EventService.cs` | Modify | Guard swap (~L615) + impl doc (L597-601) |
| `backend/Services/IEventService.cs` | Modify | XML docs for `DeleteEventAsync` (L63-73) |
| `backend/Controllers/EventController.cs` | Modify | Comment only on audit branch (L189) |
| `frontend/src/pages/OrganizerDashboard.jsx` | Modify | Remove L15 import, L36-38 states (`deleteTarget`, `deleting`, `feedback` — verified delete-only; load/retry uses separate `error` state), L69-101 handlers, L130-141 feedback banner, L252-256 Metricas item, L270-277 Eliminar item, L289-296 dialog render; kebab gated on `canEdit` |
| `frontend/src/App.jsx` | Modify | Remove L20 import + L74-83 metrics route |
| `frontend/src/pages/OrganizerEventMetrics.jsx` + `.test.jsx` | Delete | Redundant page + test |
| `frontend/src/pages/OrganizerDashboard.test.jsx` | Modify | See testing strategy |
| Backend tests (5 files: `EventServiceTests`, `EventControllerTests`, `EventServiceImmutabilityTests`, `ImageStoragePropertyTests`, `MetricsControllerTests`) | Modify | See testing strategy |
| `DeleteConfirmationDialog.jsx`, `AdminPanel.jsx`/`.test.jsx` | Unchanged | Shared dialog survives (ED-003) |

## Testing Strategy

Strict TDD backend — RED first for every organizer-403 behavior.

| Spec scenario | Test target |
|---------------|-------------|
| ED-001 draft-403, no side effects | `EventServiceTests`: invert `DeleteEventAsync_ByOwner_DeletesEvent` (L651) → organizer-owner throws `UnauthorizedAccessException`, row present (Pending seed); new `..._OrganizerRejected_NoImageCleanup` asserts S3 `Times.Never` (RED) |
| ED-001 active-403 | New service test (Approved+future) + new `EventControllerTests.DeleteEvent_OrganizerActiveEvent_403` integration (RED) |
| ED-001 past-403-not-409 | Invert `EventServiceImmutabilityTests.DeleteEventAsync_PastEvent_...` (L179) organizer→`UnauthorizedAccessException` (RED); new `EventControllerTests.DeleteEvent_OrganizerPastEvent_403_Not409` (RED) |
| ED-002 admin-active / admin-past-409 | Existing `ByAdmin` + 3 image tests role-switched to Admin (green today); ImmutabilityTests new admin variant → `EventFinalizedException`; `DeleteEvent_PastEvent_409_EventFinalized` (L515) login switched to Admin (green today) |
| ED-003 admin panel | `AdminPanel.test.jsx` unchanged, must stay green |
| EHE-006 hides Eliminar/Metricas; PEC-004 metricas-absent | New organizer test: no "Acciones" trigger, no Eliminar/Metricas items, Ver present; updated past-events test (L418) drops Eliminar/Metricas assertions; admin kebab = Editar only (RED) |
| EHE-006 route-unresolved | By construction (route deleted); `App.test.jsx` has no metrics refs (verified) |
| EHE-006 metrics owner/admin-200 | New integration tests + existing mocked `MetricsControllerTests` (Decision 5) |
| EHE-006 load-error feedback | Existing "error state and Reintentar" test unchanged (green) |
| Unchanged guards | `ByNonOwner` (still 403), `NonExistent` (404-first), `EventManagementPropertyTests` L802/L1026, controller audit tests, `ImageStoragePropertyTests` 5 calls (L454/535/599/650/706) switch to Admin (random adminId, no user row — established pattern) |
| Unchanged consultation behaviors (EHE-006 regression guard) | Past detail via `/manage` 200: `EventControllerTests` `GetEventById_ManagementIncludeExpired_200`/`Organizer_ManagementEvent_Expired_200` (L358/L379, WAF + organizer cookie). Non-delete past-mutation 409: `EventServiceImmutabilityTests` L117/L198/L218/L238 + `EventControllerTests` L476/L536/L557/L577 (delete half = RED row above). Pending/rejected listing + badges: `MetricsPropertyTests.GetOrganizerMetrics_EachEventCarriesItsStatus` (L644, asserts 3 statuses) + `OrganizerDashboard.test.jsx` badge test (L151). Organizer metrics: `MetricsPropertyTests` L48 + `MetricsConsolidationTests` L45 — every `CreateEvent` seed is future-dated (+30d), so past-date inclusion has no direct automated coverage today (accepted, unchanged by this change). Pending event detail: no automated coverage today (Ver-nav L231 exercises an Approved row; `SeedEvent` defaults Approved) — accepted, unchanged by this change |

Frontend runs via `npx vitest run`; backend via `dotnet test` from `backend/`. Pre-existing branch debt (Checkout, identityValidation) excluded.

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary. Standard ASP.NET authorization change within the app's own domain.

## Migration / Rollout

No migration, no flags. Single PR, backend-first commit order (review clarity; PR merges atomically):

1. `docs(specs)`: SDD artifacts (currently untracked)
2. `feat(api)!: restrict event deletion to Admin (ED-001)` — RED tests + guard + docs + role switches — `dotnet test`
3. `test(api)`: metrics pipeline coverage — `dotnet test`
4. `feat(frontend)`: dashboard delete-flow removal + kebab narrowing — vitest (OrganizerDashboard + AdminPanel)
5. `feat(frontend)`: metrics page/route deletion — full vitest + dead-reference grep

Each commit independently revertible; PR revert = full rollback.

**Archive-time note**: `past-event-mutation-guard` PEM-002 (spec.md L35 lists `DELETE /events/{id}` among 409 endpoints; L39 scenario says "valid requester (owner or Admin)") needs a one-line clarification delta at archive: DELETE's valid-requester set narrows to Admin-only per `event-deletion` ED-001; organizer+past → 403 before 409.

## Open Questions

None — all proposal open questions resolved (Decisions 1-5).
