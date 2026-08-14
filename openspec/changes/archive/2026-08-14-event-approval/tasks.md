# Tasks: Event Approval

## Review Workload Forecast

estimated_changed_lines: ~800–950
400-line budget risk: Low
Chained PRs recommended: No
Chain strategy: stacked-to-main
Decision needed before apply: No
delivery_strategy_hint: auto-chain — single PR with work-unit commits; if diff exceeds 1500, split backend-core → admin-endpoints → frontend

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test | Runtime harness | Rollback |
|------|------|-----------|--------------|-----------------|----------|
| 1 | Model+migration+backfill | PR 1 | `dotnet test --filter EventApprovalBackfillTests` | Dev `dotnet ef database update`; all→Approved | Revert migration+Event.Status |
| 2 | DTOs+EventService | PR 1 | `dotnet test --filter "EventServiceTests|MetricsControllerTests"` | POST /events → 201 Pending, absent from public list | Revert EventService/DTOs |
| 3 | AdminService+Controller+audit | PR 1 | `dotnet test --filter AdminControllerTests` | curl approve/reject (admin) → 200; audit row | Revert AdminService/Controller/AuditLog |
| 4 | Public-detail 404 | PR 1 | `dotnet test --filter "EventControllerTests|AdminPropertyTests"` | curl public GET /events/{id} → 404 pending / 200 approved | Revert EventController.GetEvent |
| 5 | Frontend utils+AdminPanel | PR 1 | `npx vitest run src/lib/__tests__/eventStatus.test.js src/pages/AdminPanel.test.jsx` | Manual: pending badge; approve → Approved | Revert eventStatus.js+AdminPanel |
| 6 | OrganizerDashboard | PR 1 | `npx vitest run src/pages/OrganizerDashboard.test.jsx` | Manual: Badge; Editar hidden (Organizador) | Revert OrganizerDashboard.jsx |
| 7 | EventForm copy | PR 1 | `npx vitest run src/components/EventForm.test.jsx` | Manual: create → pending copy | Revert EventForm.jsx |
| 8 | Verify | PR 1 | `dotnet test && npx vitest run` | Migration apply + manual smoke | N/A (verification) |

## Phase 1: Backend RED Tests (strict TDD)

- [x] 1.1 EA-T1 Create `backend/Tests/EventApprovalBackfillTests.cs` (InMemory): ALL existing → Approved; empty no-op (EA-006).
- [x] 1.2 EA-T2 Modify `backend/Tests/EventServiceTests.cs`: create → Pending; list excludes Pending/Rejected (EHE-002); manage returns Pending (EHE-006); mapper Status (EA-007).
- [x] 1.3 EA-T3 Modify `backend/Tests/EventControllerTests.cs`: public GetEvent 404 Pending/Rejected, 200 Approved (EHE-003).
- [x] 1.4 EA-T4 Modify `backend/Tests/AdminControllerTests.cs`: approve/reject 200 + audit; reject reason truncated ≤1000; unknown 404 no audit; non-admin 403 (EA-003/004).
- [x] 1.5 EA-T5 Modify `backend/Tests/MetricsControllerTests.cs`: metrics carry Status (EA-007).
- [x] 1.6 EA-T6 Modify `backend/Tests/AdminPropertyTests.cs` (FsCheck): ∀ status approve→Approved / reject→Rejected succeed (EA-005); GetPendingEvents only Pending.

## Phase 2: Backend Foundation (GREEN)

- [x] 2.1 EA-T7 Create `backend/Models/EventStatus.cs` (enum + `[JsonStringEnumConverter]`); add `Event.Status` in `backend/Models/Event.cs`; `.IsRequired()` in `ApplicationDbContext.OnModelCreating` (D-1/D-3).
- [x] 2.2 EA-T8 `dotnet ef migrations add AddEventApproval`; hand-wire backfill `try/catch` in `Up()` + `DropColumn` in `Down()`; create `backend/Data/EventApprovalBackfill.cs` (EA-001/006, D-9).

## Phase 3: Backend Core (GREEN)

- [x] 3.1 EA-T9 Modify `IEventService`/`EventService.cs`: `EventWithAvailability.Status`; create forces `Pending`; `GetAllPublishedEventsAsync` += `Where(Status==Approved)`; mapper Status (EA-002/007, EHE-002).
- [x] 3.2 EA-T10 Modify `IAdminService`/`AdminService.cs`: `ApproveEventAsync`/`RejectEventAsync(Guid,string?)` (KeyNotFound on missing)/`GetPendingEventsAsync`; `EventSummary.Status` (EA-003/005/007).
- [x] 3.3 EA-T11 Modify `AuditLog.cs` += `ApproveEvent`/`RejectEvent`; `AdminController.cs`: both `[HttpPost]` endpoints + `RejectEventRequest` + audit (EA-003/004).
- [x] 3.4 EA-T12 Modify `EventController.GetEvent`: post-read `Status != Approved` → 404; `GetEventByIdAsync` untouched (EHE-003, D-2).
- [x] 3.5 EA-T13 Modify `IMetricsService`/`MetricsService.cs`: `EventMetrics.Status` (EA-007, EHE-006).

## Phase 4: Frontend RED Tests

- [x] 4.1 EA-T14 Create `frontend/src/lib/__tests__/eventStatus.test.js`: variant/label mapping.
- [x] 4.2 EA-T15 Modify `frontend/src/pages/AdminPanel.test.jsx`: pending badge; Approve/Reject per status; success refetch; failure error+state unchanged (EA-008).
- [x] 4.3 EA-T16 Modify `frontend/src/pages/OrganizerDashboard.test.jsx`: Badge per row; Editar hidden Organizador / shown Admin (EA-009).
- [x] 4.4 EA-T17 Modify `frontend/src/components/EventForm.test.jsx`: create pending copy; edit unchanged (EA-009).

## Phase 5: Frontend Implementation (GREEN)

- [x] 5.1 EA-T18 Create `frontend/src/lib/eventStatus.js`; add `adminEvents` key to `frontend/src/lib/queryKeys.js` (D-6).
- [x] 5.2 EA-T19 Modify `AdminPanel.jsx`: Estado Badge; `Pendientes: N` badge; Approve/Reject; success → invalidate+loadData; failure → feedback, no mutation (EA-008).
- [x] 5.3 EA-T20 Modify `OrganizerDashboard.jsx`: Estado Badge; Editar only when `role==='Admin'` (EA-009, D-8).
- [x] 5.4 EA-T21 Modify `EventForm.jsx`: create success copy "…pendiente de aprobacion." (EA-009).

## Phase 6: Verification

- [x] 6.1 EA-T22 `dotnet test` + `npx vitest run` green (EA-010); apply migration in dev; manual: pending absent, approve → visible, direct URL 404.
