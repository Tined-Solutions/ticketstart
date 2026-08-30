# Apply Progress: remove-organizer-delete-metrics

**Branch**: `feat/frontend-brand-polish` (base 88c8fdb, clean)
**Delivery**: single-pr, 5 work-unit commits, backend-first, docs last
**Mode**: Strict TDD (backend RED→GREEN→REFACTOR); RED-first ordering honored on frontend

## Result

**status: success** — all 21 tasks across 6 phases implemented and checked.

## Work-Unit Commits

| # | Hash | Message | Scope |
|---|------|---------|-------|
| 1 | `4f99efd` | `feat(api)!: restringir eliminación de eventos a Admin (ED-001)` | Guard swap + 7 RED tests turned green + role switches + XML docs + audit comment |
| 2 | `63b1bc8` | `test(api): cobertura WAF del endpoint de métricas por evento` | MetricsEndpointWafTests: owner/admin 200 via real EventOwnership pipeline |
| 3 | `acb9ab9` | `feat(frontend): eliminar flujo de borrado del panel organizador y acotar menú de acciones` | Dashboard: no delete flow, kebab admin-only = Editar, Ver kept |
| 4 | `e9b3f20` | `feat(frontend): eliminar página y ruta de métricas por evento` | OrganizerEventMetrics.* deleted, route unregistered (falls through to `*` → NotFound) |
| 5 | (this commit) | `docs(openspec): artefactos SDD de remove-organizer-delete-metrics` | Proposal/specs/design/tasks + archive note + this progress file |

Each commit independently revertible; PR revert = full rollback.

## Per-Task Evidence

### Phase 1 — Backend guard (ED-001/ED-002)
- **1.1** `DeleteEventAsync_ByOwnerOrganizer_Pending_ThrowsUnauthorizedAccessException_RowPresent` (inverted owner test, Pending seed, row survives) + `DeleteEventAsync_OrganizerRejected_NoImageCleanup` (S3 `DeleteObjectAsync` `Times.Never`). RED confirmed: "No exception was thrown".
- **1.2** `DeleteEventAsync_Organizer_ApprovedFutureEvent_ThrowsUnauthorizedAccessException` (service) + `EventControllerTests.DeleteEvent_OrganizerActiveEvent_403` (WAF: organizer cookie → 403, row survives via /manage 200). RED: expected Forbidden, got NoContent.
- **1.3** Immutability test inverted: organizer + past → `UnauthorizedAccessException` (NOT 409); new admin variant `DeleteEventAsync_PastEvent_Admin_ThrowsEventFinalized_EventStillPresent`. WAF `DeleteEvent_OrganizerPastEvent_403_Not409`: 403 + no `application/problem+json` body (bare Forbid — not the PEM-002 409). RED: expected Forbidden, got Conflict — proving today's 409-first ordering.
- **1.4 LANDMINE decision**: `EventManagementPropertyTests.NonOwnerDeletion_IsRejected_ForOrganizadorRole` asserted `"permission"`. New message `"Only administrators can delete events"` lacks it. **Decision (preferred option): assertion updated to `"administrator"`** with explanatory comment. RED run proved the landmine live ("administrator" not found in old message).
- **1.5 GREEN**: guard swapped in `EventService.DeleteEventAsync` exactly per design snippet (Admin-only, before `EventFinalizedGuard`, warning log, `userId` kept for logging); impl doc + `IEventService.DeleteEventAsync` XML docs rewritten Admin-only (ED-001).
- **1.6 Role switches**: `EventServiceTests` 3 image tests → `Guid.NewGuid(), UserRole.Admin`; `ImageStoragePropertyTests` ×5 (single/multiple/key-deletion call sites) → Admin (random adminId, no user row — established pattern); `DeleteEvent_PastEvent_409_EventFinalized` WAF login → Admin. ByNonOwner organizer test deliberately kept organizer (403 regression guard).
- **1.7** `EventController.DeleteEvent`: zero behavior change; clarifying comment on the audit branch (success now implies Admin; defensive condition kept).
- **1.8** Regression guards green: `ByNonOwner` 403, `NonExistent` 404-first (guard after FindAsync), `AdminDeletion_IsAllowed_ForAnyEvent`, controller audit tests.

### Phase 2 — Metrics keep-alive
- **2.1/2.2** New `[Collection("EnvConfigTests")]` class `MetricsEndpointWafTests` in MetricsControllerTests.cs: owner 200 (asserts EventId + EventName) and admin 200 via `EventCatalogApiFactory` — real auth + real `EventOwnership` policy (existing mocked unit tests bypass it).

### Phase 3 — Dashboard
- **3.1 RED** `shows no Acciones kebab for organizers...` (no trigger, no Eliminar/Metricas, Ver enabled); admin test extended: kebab = Editar only. RED run: 3 failed | 17 passed (exactly the updated contracts).
- **3.2 RED** past-events test rewritten: Editar disabled + readonly title kept; Eliminar/Metricas asserted absent (PEC-004 metricas-absent-past-row).
- **3.3 GREEN** OrganizerDashboard.jsx: DeleteConfirmationDialog import, `deleteTarget`/`deleting`/`feedback` states, 3 handlers, feedback banner, Metricas item, Eliminar item, dialog render all removed; kebab gated `{canEdit && (...)}` with Editar-only items; Ver + load/retry `error` path untouched. (Z-index kebab test switched to admin role — the kebab now only renders there.)
- **3.4** OrganizerDashboard (20) + AdminPanel (48) = 68/68 green. `DeleteConfirmationDialog.jsx` + `AdminPanel.jsx` untouched (ED-003); AdminPanel still renders the shared dialog (verified at AdminPanel.jsx L729).

### Phase 4 — Metrics page removal
- **4.1** `git rm OrganizerEventMetrics.jsx` + `.test.jsx` (409 lines deleted).
- **4.2** App.jsx: import + `/organizer/events/:id/metrics` route removed; navigation falls through to `*` → NotFound. App.test.jsx had no metrics refs (no edit needed, as designed).
- **4.3** Dead-reference grep `OrganizerEventMetrics|/organizer/events/.*/metrics` over `src/`: **zero hits**.

### Phase 5 — Docs
- **5.1** This docs commit (untracked openspec artifacts committed last, per orchestrator directive overriding design.md's first-position listing).
- **5.2** `archive-note.md` records the PEM-002 one-line clarification delta (DELETE valid-requester set narrows to Admin-only; organizer+past → 403 before 409).

### Phase 6 — Cross-cutting verification
- **6.1 Regression guards (all green, no new tests)**: past detail via `/manage` 200 (`GetEventById_ManagementIncludeExpired_200`, `Organizer_ManagementEvent_Expired_200`); non-delete past-mutation 409 (ImmutabilityTests update/replace-image/stock/type + WAF update/image/stock/type/approve/reject); pending/rejected listing + badges (`GetOrganizerMetrics_EachItemCarriesStatus` 3-status + dashboard badge test); organizer aggregate metrics (`MetricsPropertyTests` + `MetricsConsolidationTests`, included in the 27/27 metrics run).
- **6.2 Accepted no-coverage dispositions (unchanged, no tests manufactured)**: metrics past-date inclusion — all `CreateEvent` seeds are future-dated (+30d); pending event detail — `SeedEvent` defaults Approved and Ver-nav exercises an Approved row. Both behaviors untouched by this change.
- **6.3 Full verification** — see below.

## Verification Results (exact)

| Command | Result |
|---------|--------|
| `dotnet test` (backend/, full) | **684 passed / 5 failed / 689 total** |
| `dotnet test --filter` 5 touched suites (EventServiceTests, EventServiceImmutabilityTests, EventControllerTests, EventManagementPropertyTests, ImageStoragePropertyTests) | **127/127 passed** |
| `dotnet test --filter` metrics suites (MetricsControllerTests + WAF + MetricsPropertyTests + MetricsConsolidationTests) | **27/27 passed** |
| `npx vitest run src/pages/OrganizerDashboard.test.jsx src/pages/AdminPanel.test.jsx` | **68/68 passed** |
| `npx vitest run` (frontend/, full) | **452 passed / 3 failed / 455 total** |

**Backend failures — proven pre-existing**: `git stash` + identical filtered run on clean 88c8fdb failed the same tests (6/6 at filter level; flake band 5–6 between full runs). Set: `CsrfMiddleware_AllowsWebhook_WithoutHeader`, `UploadEventImageAsync_PassesCorrectParametersToS3Client`, `EnqueueAsync_ReturnsImmediately` (flake), `Webhook_InvalidSignature_ReturnsUnauthorized`, `Property17_InvalidSignature_ReturnsUnauthorized`, `RetryPendingEmailsAsync_Exhaustion_MarksExhausted`. None touch delete/metrics paths; none are the orchestrator-named VerifyDatabaseSchema flaky. Beyond this change's scope.

**Frontend failures — orchestrator-excluded pre-existing debt**: Checkout.test.jsx ×2 (Editar datos / PATCH), identityValidation.test.js ×1 (DNI with letters). Stable identity across runs.

## Files Changed

- `backend/Services/EventService.cs` — Admin-only guard + impl doc
- `backend/Services/IEventService.cs` — XML docs
- `backend/Controllers/EventController.cs` — audit-branch comment only
- `backend/Tests/EventServiceTests.cs` — 1 inversion + 2 new + 3 role switches
- `backend/Tests/EventControllerTests.cs` — 2 new WAF 403 tests + 409 login switch
- `backend/Tests/EventServiceImmutabilityTests.cs` — 1 inversion + 1 admin variant
- `backend/Tests/EventManagementPropertyTests.cs` — landmine assertion update
- `backend/Tests/ImageStoragePropertyTests.cs` — 5 role switches
- `backend/Tests/MetricsControllerTests.cs` — MetricsEndpointWafTests (2 tests)
- `frontend/src/pages/OrganizerDashboard.jsx` — delete flow removed, kebab narrowed
- `frontend/src/pages/OrganizerDashboard.test.jsx` — contracts updated, dead tests removed
- `frontend/src/App.jsx` — metrics route + import removed
- `frontend/src/pages/OrganizerEventMetrics.jsx` + `.test.jsx` — deleted
- `openspec/changes/remove-organizer-delete-metrics/*` — docs + archive note + this file
