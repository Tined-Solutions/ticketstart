# Tasks: Past Events Read-Only (Event Immutability)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~800–1000 |
| 400-line budget risk | N/A (budget is 2000; risk Low) |
| Chained PRs recommended | No |
| Suggested split | Single PR (4 work-unit commits) |
| Delivery strategy | single-pr |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

### Suggested Work Units (commits inside single PR)

| Unit | Goal | Commit | Focused test | Runtime harness | Rollback |
|------|------|--------|--------------|-----------------|----------|
| 1 | Guard foundation: exception + helper + tests | `feat(events): add EventFinalizedGuard + exception` | `dotnet test --filter EventFinalizedGuardTests` | N/A — pure logic proven by unit tests | Revert 2 new backend files + test file |
| 2 | Service guards: EventService (5) + AdminService (2 + TimeProvider) + tests | `feat(events): guard service mutations on past events` | `dotnet test --filter EventServiceImmutabilityTests\|AdminServiceTests` | `dotnet run`; curl PUT/approve on seeded past event → 409, no row change | Revert guard calls + AdminService ctor + 2 test files |
| 3 | Controllers + handler: 7 catches + GlobalExceptionHandler + tests | `feat(events): map past-event mutations to 409` | `dotnet test --filter EventControllerTests\|AdminControllerTests\|ErrorHandlingPropertyTests` | `dotnet run`; curl all 7 endpoints → `type:"event-finalized"` | Revert controller catches + handler case + 3 test files |
| 4 | Frontend: readOnly prop, Ver view + route, dashboards | `feat(web): read-only Ver view for past events` | `npm run build && npm run lint` | Manual: past row → Finalizado badge, Ver disabled form, Compras/Metricas navigate | Revert 5 frontend files |

## Phase 1: Backend Guard Foundation (TDD)

- [x] 1.1 RED: Create `backend/Tests/EventFinalizedGuardTests.cs` (FakeTimeProvider): expired → throws; active → no-op; exact-instant → no-op (PEM-001).
- [x] 1.2 GREEN: Create `backend/Models/EventFinalizedException.cs` (`base("Event has already finished")`, D-1).
- [x] 1.3 GREEN: Create `backend/Services/Guards/EventFinalizedGuard.cs` — static `EnsureMutable(Event, TimeProvider)` throwing on `IsExpired(clock.GetUtcNow().UtcDateTime)` (D-3); materialized entity only (ADR-2).

## Phase 2: Backend Service Guards (TDD)

- [x] 2.1 RED: Create `backend/Tests/EventServiceImmutabilityTests.cs` (InMemory + FakeTimeProvider frozen T, seed T-2d): Update/Delete/ReplaceImage/AddTicketStock/AddTicketType throw `EventFinalizedException`, DB unchanged, `Verify(EnqueueAsync, Never)` for Update (PEM-002/003).
- [x] 2.2 RED: Create `backend/Tests/AdminServiceTests.cs`: Approve/Reject throw on past + no status flip; future still flips (EA-003/004).
- [x] 2.3 GREEN: `backend/Services/EventService.cs` — call guard after ownership/before write: UpdateEventAsync(:458), DeleteEventAsync(:585), ReplaceEventImageAsync(:727), AddTicketStockAsync(:290, inside FOR UPDATE txn), AddTicketTypeAsync(:368).
- [x] 2.4 GREEN: `backend/Services/AdminService.cs` — add `TimeProvider` ctor param (D-8); guard in ApproveEventAsync(:104), RejectEventAsync(:121) after load. Program.cs unchanged.

## Phase 3: Controllers + Middleware (TDD)

- [ ] 3.1 RED: `backend/Tests/EventControllerTests.cs` — integration (EventCatalogApiFactory frozen Clock + cookie): all 7 endpoints → 409 `type:"event-finalized"` on seeded past event; `GET /events/{id}/manage` → 200 (PEM-005, EHE-006); keep both expired-GET-200 tests green.
- [ ] 3.2 RED: `backend/Tests/AdminControllerTests.cs` — mock `.ThrowsAsync(new EventFinalizedException())` → `Problem(409, "event-finalized")` for Approve/Reject/AddTicketStock/AddTicketType; audit `Verify(LogActionAsync, Never)`.
- [ ] 3.3 RED: `backend/Tests/ErrorHandlingPropertyTests.cs` — handler payload test mirroring :206.
- [ ] 3.4 GREEN: `backend/Controllers/EventController.cs` — add `catch (EventFinalizedException) → Problem(409, "event-finalized")` above generic catch: Update(:146), Delete(:186), UploadEventImage(:232).
- [ ] 3.5 GREEN: `backend/Controllers/AdminController.cs` — same catch: AddTicketStock(:198), AddTicketType(:222), ApproveEvent(:328), RejectEvent(:366).
- [ ] 3.6 GREEN: `backend/Middleware/GlobalExceptionHandler.cs` — MapException case `EVENT_FINALIZED` + TryHandleAsync `type:"event-finalized"` special-case.

## Phase 4: Frontend (manual verification)

- [ ] 4.1 `frontend/src/components/EventForm.jsx` — `readOnly` prop: disable inputs, hide submit + file input, keep image preview (D-6).
- [ ] 4.2 Create `frontend/src/pages/EventReadOnlyView.jsx` — `useManagementEvent(id)` + `<EventForm mode="edit" readOnly />` + Volver (D-5); loading/error mirror OrganizerEventDetail.jsx:31-52.
- [ ] 4.3 `frontend/src/App.jsx` — route `/organizer/events/:id/view`, RoleGuard `['Organizador','Admin']`.
- [ ] 4.4 `frontend/src/pages/AdminPanel.jsx` — `isPast` per row; Ver → navigate; disable Aprobar/Rechazar/Agregar entradas/Editar/Eliminar + tooltip; Finalizado badge; keep Compras; row not grayed (D-7).
- [ ] 4.5 `frontend/src/pages/OrganizerDashboard.jsx` — `isPast`; Ver; disable Editar/Eliminar + tooltip; Finalizado badge; keep Metricas.

## Phase 5: Verification & Cleanup

- [ ] 5.1 Full `dotnet test` from `backend/` — green, zero regressions (PEM-005 carve-outs, future-date tests intact).
- [ ] 5.2 `dotnet format` from `backend/` — clean.
- [ ] 5.3 Manual smoke (frontend): Ver renders read-only, mutation buttons disabled + tooltip, Compras/Metricas navigate, EventForm no submit/upload.
- [ ] 5.4 Update tasks.md checkboxes + apply-progress via sdd-apply.