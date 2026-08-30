# Tasks: remove-organizer-delete-metrics

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~800 code (≈350 frontend deletions incl. OrganizerEventMetrics.*, ≈120 dashboard/test edits, ≈250 backend test inversions/additions, ≈60 new WAF tests, ≈30 backend code) + ~1,100 SDD docs (final commit) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR, 5 work-unit commits (backend-first) |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: High

> Code-only changes (~800) exceed the default 400-line budget. Delivery strategy is `single-pr`, which requires explicit `size:exception` (or acceptance of the provisioned 2,000-line review budget) before `sdd-apply`.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Admin-only delete guard (RED-first) + role switches | PR (single) | `dotnet test` from `backend/` | RED→GREEN cycle on new 403 tests | Revert guard+tests commit; other endpoints untouched |
| 2 | Metrics endpoint keep-alive WAF coverage | PR (single) | `dotnet test --filter MetricsControllerTests` | WAF: owner+admin GET /metrics/events/{id} = 200 | Drop test-only commit; no prod change |
| 3 | Dashboard delete-flow removal + kebab narrowing | PR (single) | `npx vitest run src/pages/OrganizerDashboard.test.jsx` + AdminPanel suite | Vitest+Testing Library organizer/admin render | Revert dashboard commit; dialog + AdminPanel untouched |
| 4 | Metrics page + route deletion | PR (single) | `npm test` (full vitest) + dead-reference grep | Navigate `/organizer/events/:id/metrics` → NotFound | Revert deletion commit (restores files) |
| 5 | docs(openspec): SDD artifacts | PR (single) | N/A — docs only | N/A — no runtime boundary (docs commit) | Revert docs commit independently |

## Phase 1: Backend guard — feat(api)!: restrict event deletion to Admin (ED-001)

- [x] 1.1 RED: `backend/Tests/EventServiceTests.cs` — invert `DeleteEventAsync_ByOwner_DeletesEvent` (L651): organizer-owner on `Pending` seed → throws `UnauthorizedAccessException`, row still present. Add `DeleteEventAsync_OrganizerRejected_NoImageCleanup` asserting image storage `Times.Never`. [ED-001 draft-403 + rejected-no-side-effects]
- [x] 1.2 RED: new `EventServiceTests` case — organizer + `Approved`/future event → `UnauthorizedAccessException`; new `EventControllerTests.DeleteEvent_OrganizerActiveEvent_403` (WAF + organizer cookie). [ED-001 active-403]
- [x] 1.3 RED: invert `EventServiceImmutabilityTests.DeleteEventAsync_PastEvent_...` (L179) — organizer → `UnauthorizedAccessException` (403, NOT 409); new `EventControllerTests.DeleteEvent_OrganizerPastEvent_403_Not409`. [ED-001 past-403-not-409; role-access cannot-mutate-past delete half]
- [x] 1.4 LANDMINE: `backend/Tests/EventManagementPropertyTests.cs` ~L802 asserts `"permission"` substring in the delete exception message; the new guard message `"Only administrators can delete events"` will fail it. At RED time: update the assertion to match the new message (preferred) or adjust the message — decide explicitly, do not silently skip. (DECIDED: assertion updated to `"administrator"`; RED run confirmed the landmine was live — old message failed the new assertion.)
- [x] 1.5 GREEN: `backend/Services/EventService.cs` — swap guard at ~L614-620 with design snippet (Admin-only, before `EventFinalizedGuard`, log warning); update impl doc L597-601; update `IEventService.DeleteEventAsync` XML docs L63-73 → Admin-only (ED-001). Run `dotnet test` — all new RED tests green.
- [x] 1.6 Role switches to Admin (ED-002, green-today): `EventServiceTests.ByAdmin` + 3 image tests → Admin; `ImageStoragePropertyTests` role switches ×5 (L454/535/599/650/706; random adminId, no user row — established pattern); `EventServiceImmutabilityTests` new admin past-event variant → `EventFinalizedException`; `EventControllerTests.DeleteEvent_PastEvent_409_EventFinalized` (L515) login → Admin. [ED-002 admin-active-204 + admin-past-409]
- [x] 1.7 `backend/Controllers/EventController.cs` — verify zero behavior change (audit branch kept, `UnauthorizedAccessException`→`Forbid()`); add only the clarifying comment on audit branch (L189). [ED-002 unchanged contract]
- [x] 1.8 Regression-guard run (unchanged behaviors): `ByNonOwner` 403, `NonExistent` 404-first, `EventManagementPropertyTests` L1026, controller audit tests — all green via `dotnet test`. (Focused run: 127/127 across the 5 touched suites; full suite 684/689 — the 5 failures pre-exist on clean 88c8fdb, proven via stash run: webhook/CSRF, S3 upload params, email retry; not delete-related.)

## Phase 2: Metrics keep-alive — test(api): metrics pipeline coverage

- [x] 2.1 `backend/Tests/MetricsControllerTests.cs` — new WAF integration test: owner calls `GET /metrics/events/{id}` → 200 with metrics data. [role-access per-event-metrics-owner-200]
- [x] 2.2 New WAF integration test: Admin calls `GET /metrics/events/{id}` → 200. Run `dotnet test`. [role-access per-event-metrics-admin-200]

## Phase 3: Dashboard — feat(frontend): delete-flow removal + kebab narrowing

- [x] 3.1 RED: `frontend/src/pages/OrganizerDashboard.test.jsx` — organizer rows: no "Acciones" DropdownMenu trigger, no Eliminar/Metricas entries, "Ver" present; admin kebab = Editar only. [hides-eliminar-metricas + hides-edit; PEC-004 metricas-absent]
- [x] 3.2 RED: update past-events test (L418) — drop Eliminar/Metricas assertions. [dashboard-lists-past + PEC-004 metricas-absent-past-row]
- [x] 3.3 GREEN: `frontend/src/pages/OrganizerDashboard.jsx` — remove L15 import, `deleteTarget`/`deleting`/`feedback` states (L36-38), handlers L69-101, feedback banner L130-141, Metricas item L252-256, Eliminar item L270-277, dialog render L289-296; gate kebab on `canEdit`; keep "Ver" button and load/retry `error` path. Existing "error state and Reintentar" test stays green. [load-error-feedback-survives]
- [x] 3.4 Run OrganizerDashboard + AdminPanel vitest suites — `AdminPanel.test.jsx` and `DeleteConfirmationDialog.jsx` untouched and green. [ED-003 adminpanel-flow-survives; PEC-004 compras-enabled (AdminPanel unchanged)]

## Phase 4: Metrics page removal — feat(frontend): page/route deletion

- [x] 4.1 Delete `frontend/src/pages/OrganizerEventMetrics.jsx` + `OrganizerEventMetrics.test.jsx`. [metrics-route-unresolved; PEC-004 no dead navigation target]
- [x] 4.2 `frontend/src/App.jsx` — remove L20 import + L74-83 route; navigation falls through to `*` → `NotFound`. `App.test.jsx` has no metrics refs (verified — no edit).
- [x] 4.3 Full `npm test` + dead-reference grep (`OrganizerEventMetrics`, `/organizer/events/.*/metrics`) — zero hits.

## Phase 5: Docs — docs(openspec): SDD artifacts (final work unit)

- [x] 5.1 Commit untracked `openspec/changes/remove-organizer-delete-metrics/*` (proposal, specs, design, tasks). Note: orchestrator directive places the docs commit last; design.md listed it first — final position wins.
- [x] 5.2 Record archive-time note in the change folder: `past-event-mutation-guard` PEM-002 needs a one-line clarification delta at archive (DELETE valid-requester set narrows to Admin-only per ED-001; organizer+past → 403 before 409).

## Phase 6: Cross-cutting verification (before PR)

- [x] 6.1 Regression-guard verification, no new tests (design L73): management-variant past detail 200 (`EventControllerTests` L358/L379), non-delete past-mutation 409 (`EventServiceImmutabilityTests` L117/L198/L218/L238; `EventControllerTests` L476/L536/L557/L577), pending/rejected listing + badges (`MetricsPropertyTests` L644 3-status assertion + `OrganizerDashboard.test.jsx` badge test L151), organizer aggregate metrics (`MetricsPropertyTests` L48 + `MetricsConsolidationTests` L45).
- [x] 6.2 Accepted no-coverage dispositions — do NOT manufacture tests: metrics past-date inclusion (organizer-metrics-include-past; all seeds future-dated +30d) and pending event detail (opens-pending-detail; `SeedEvent` defaults Approved). Explicitly record both as unchanged by this change.
- [x] 6.3 Full verification: `dotnet test` from `backend/` and `npm test` from `frontend/` — record exact results as PR evidence (per work-unit checklist: focused command, runtime harness, rollback boundary already tabled above).
