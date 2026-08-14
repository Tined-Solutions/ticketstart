# Design: Event Approval

## Overview & Goals

Add a moderation gate so organizer-created events MUST be Admin-approved before they reach
the buyer-facing catalog. New events start `Pending`; only `Approved` events surface in
`GET /api/events` and public `GET /api/events/{id}`; `Rejected`/`Pending` 404/are absent.
Admins approve/reject (free flips, optional reason) via two `AdminController` endpoints,
audited with new `ApproveEvent`/`RejectEvent` action types. A migration backfills ALL
existing events → `Approved`. Organizer dashboard + admin panel render status `Badge`s;
the organizer `Edit` entry is hidden UI-only. Covers EA-001..010 + EHE-002/003/006 deltas.

## Technical Approach

`EventStatus` (`Pending|Approved|Rejected`) mirrors `TransactionStatus` for **DB storage**
(simple enum, `.IsRequired()`, stored as `int`) and `UserRole` for **JSON** (per-enum
`[JsonStringEnumConverter]` → the API emits `"Pending"`/`"Approved"`/`"Rejected"`, the
only frontend-consumed-enum convention). Filtering lives in two places only:
`GetAllPublishedEventsAsync` (list `.Where(Status==Approved)`) and a **post-read 404** in
the public `EventController.GetEvent`; `GetEventByIdAsync` is never touched (POST-201 create
+ `…/manage` depend on it unfiltered). `CreateEventAsync` forces `Pending`; `CreateEventRequest`
has no `Status` field so no client override is possible. `AdminService` gains
`Approve`/`Reject` + `GetPendingEvents`; `AdminController` exposes them (class-level
`RequireAdminRole` → 403). `Status` piggybacks onto the three existing projections
(`EventSummary`, `EventWithAvailability`, `EventMetrics`). Migration `AddEventApproval`:
`AddColumn<int>(Status, Events, NOT NULL, default 0)` then best-effort
`ApplicationDbContextFactory().CreateDbContext(null)` + `EventApprovalBackfill.RunAsync`
inside `try/catch` (mirrors `AddTicketReservationAndRefund`).

## Architecture Decisions

| # | Decision | Choice | Alternatives | Rationale |
|---|----------|--------|--------------|----------|
| D-1 | Enum storage vs JSON | `int` in DB (no `HasConversion`, matches `ReservationStatus`/`TransactionStatus`); per-enum `[JsonStringEnumConverter]` for JSON (matches `UserRole`, the only frontend-consumed enum) → `"Pending"/"Approved"/"Rejected"` | Global `JsonStringEnumConverter` in `Program.cs`; frontend maps int→variant | Global converter would silently change serialization of `ReservationStatus`/`TransactionStatus`/`AuditActionType` (breaking clients + tests). int→variant frontend mapping is error-prone and inconsistent with how `user.role` already arrives as a string (AdminPanel `roleBadgeVariant` switches on `'Admin'`). |
| D-2 | Where the public-detail status filter lives | Post-read `404` in `EventController.GetEvent` (public route) + `.Where(Status==Approved)` in `GetAllPublishedEventsAsync`; `GetEventByIdAsync` untouched | Add `includeNonApproved` param to `GetEventByIdAsync`; filter inside `GetEventByIdAsync` default | Closed decision: `GetEventByIdAsync` is shared by `CreateEvent` POST-201 (Pending just-created) and `GetEventForManagement` (own Pending/Rejected) — both MUST return the event. `GetEventForManager` already uses `includeExpired:true` with no status filter ⇒ it is the EHE-003 management variant + satisfies EHE-006 unmodified. |
| D-3 | DB default of Pending | `OnModelCreating`: `.Property(e=>e.Status).IsRequired();` only. EF scaffolds `defaultValue:0` (=Pending) for the NOT-NULL `int` (mirrors `IsRefunded` `defaultValue:false`) | `.HasDefaultValue(EventStatus.Pending)`; no migration default | `HasDefaultValue` makes the explicit `Status=Pending` set a sentinel no-op and adds `ValueGeneratedOnAdd` ambiguity in InMemory tests. No default breaks a NOT-NULL add on a populated table. EF diffs current-model↔`ModelSnapshot` (not history) ⇒ the migration's `defaultValue:0` never causes a phantom future diff. Matches the named `ReservationStatus`/`TransactionStatus` pattern exactly. |
| D-4 | Approve/reject service home | New `IAdminService.ApproveEventAsync`/`RejectEventAsync`/`GetPendingEventsAsync` | Put in `EventService` | Approve/reject is admin moderation, not organizer ownership-gated CRUD. `AdminController` already injects `IAdminService` + `IAuditLogService` + the `TryLogAuditAsync(adminId, …)` overload ⇒ no new injection. Admin flips any status freely (EA-005, no state machine). |
| D-5 | Audit | New `AuditActionType.ApproveEvent`/`RejectEvent` (stored varchar(100) via `HasConversion<string>()` ⇒ **no migration**, mirrors `AddTicketStock`); `AuditResourceType.Event`; audit AFTER service success (unknown-event 404 writes NONE — EA-003); optional reason in `Details` truncated ≤1000 (EA-004) | Store reason on `Event`; mandatory reason | `Event` has no reason column (out of scope). Reuses `TryLogAuditAsync` (best-effort, never breaks the response). Closed decisions: no mandatory reason, no email. |
| D-6 | Frontend invalidation | AdminPanel stays manual-fetch; approve/reject success → `queryClient.invalidateQueries(['events'])` + `['event', id]` (catalog/detail reflect status change) + `loadData()` refetch; failure → `setFeedback`, **no state mutation** (EA-008) | Migrate AdminPanel to `useQuery(['admin-events'])` | Same stance as `admin-add-ticket-stock` D-3: a query-table migration is a larger refactor with its own stale-state risks; minimal blast radius. Mirrors the existing `AddTicketsModal.onSuccess → loadData` pattern. |
| D-7 | New API calls | Inline `apiClient.post('/admin/events/{id}/approve'\|'/reject', {reason})` in `AdminPanel`. `api/client.js` NOT modified | Create a central `api/events.js` functions file | No such convention exists — `EventForm`/`OrganizerDashboard`/`AdminPanel` all call `apiClient` inline. `client.js` already auto-injects `X-CSRF-PROTECT` on POST (skill `backend-security`). Faithful to the established inline-axios pattern. |
| D-8 | Organizer Edit-hide source | `useAuth()` from `context/auth.js` → `user.role` string; render `Editar` only when `role==='Admin'` | A dedicated `/auth/me` fetch | Established pattern (`Navbar`/`RoleGuard` use `useAuth`). UI-only hide; backend `EventOwnership` unchanged — accepted limitation (EA-009 / proposal risk). |
| D-9 | Backfill scope | ALL existing events → `Approved` via `EventApprovalBackfill.RunAsync` (`ApplicationDbContextFactory().CreateDbContext(null)` + `try/catch` inside `Up()`) | Raw SQL `UPDATE "Events" SET "Status"=1` | EA-006 mandates the factory + best-effort repo pattern. Load+set+`SaveChanges` mirrors `TicketReservationBackfill` and is InMemory-unit-testable; failure logs and continues (migration never aborts). |
| D-10 | DTO Status exposure | Add `Status` to `EventSummary`, `EventWithAvailability`, `EventMetrics`; each mapper/projection sets it | Separate status endpoint | EA-007: one field piggybacks existing projections — no N+1, badges render from already-loaded data. |

## Data Flow

```
 Organizer ──POST /events──▶ EventService.CreateEventAsync (forces Pending) ──201 EventWithAvailability{Status:"Pending"}
                                                                                          │
 Admin ──POST /admin/events/{id}/approve──▶ AdminService.ApproveEventAsync ──ApproveEvent audit──▶ Status=Approved
 Admin ──POST /admin/events/{id}/reject────▶ AdminService.RejectEventAsync  ──RejectEvent audit───▶ Status=Rejected
                                                                                          │
 Public GET /api/events        ▶ GetAllPublishedEventsAsync  (.Where(Status==Approved) && future)
 Public GET /api/events/{id}   ▶ GetEventByIdAsync (no status filter) ──▶ GetEvent: Status!=Approved? → 404
 Manage GET /events/{id}/manage▶ GetEventByIdAsync(includeExpired:true) ─▶ returns any status (owner/admin)

 Migration Up(): AddColumn(Status, default 0=Pending) ─▶ EventApprovalBackfill.RunAsync(ALL → Approved, best-effort)
```

## Backend Design

### `EventStatus` + `Event.Status` (D-1, D-3)

```csharp
// backend/Models/EventStatus.cs  — mirrors TransactionStatus.cs file shape
using System.Text.Json.Serialization;
namespace TicketeraOnline.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]   // like UserRole: API emits the member name
public enum EventStatus { Pending, Approved, Rejected }   // int storage: 0/1/2

// Event.cs: add  public EventStatus Status { get; set; } = EventStatus.Pending;
// OnModelCreating Event entity:  entity.Property(e => e.Status).IsRequired();   // no HasConversion, no HasDefaultValue
```

### Migration — exact command sequence

```bash
# 1. from backend/ (ApplicationDbContextFactory picks MigrationConnection:5432)
dotnet ef migrations add AddEventApproval --context TicketeraOnline.Api.Data.ApplicationDbContext
# 2. review *_AddEventApproval.cs: confirm  AddColumn<int>("Status","Events", nullable:false, defaultValue:0)
# 3. hand-wire the best-effort backfill at the END of Up(); add DropColumn in Down()
# 4. apply (runtime uses DefaultConnection:6543 pooler; migrations use MigrationConnection:5432)
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext
# rollback:  dotnet ef database update 20260811182101_AddEventNameToEventNotification ...
```

```csharp
// Up ScaffoldStringBuilder skeleton (model): AddColumn<int>("Status","Events", type:"integer", nullable:false, defaultValue:0);
// then, appended by hand (mirrors 20260810120000_AddTicketReservationAndRefund Up):
try {
    using var context = new ApplicationDbContextFactory().CreateDbContext(null);
    EventApprovalBackfill.RunAsync(context).GetAwaiter().GetResult();
} catch (Exception ex) {      // EA-006 best-effort — never abort the schema migration
    Console.Error.WriteLine("[AddEventApproval] Backfill skipped for {0}; existing events keep Status=Pending(0).", ex.Message);
}
// Down:  migrationBuilder.DropColumn("Status","Events");
```

```csharp
// backend/Data/EventApprovalBackfill.cs (new — mirrors TicketReservationBackfill; InMemory-testable)
public static class EventApprovalBackfill {
    public static async Task RunAsync(ApplicationDbContext context) {
        var events = await context.Events.ToListAsync();            // ALL rows incl. expired
        if (events.Count == 0) return;
        foreach (var e in events) e.Status = EventStatus.Approved;  // EA-006 backfill scope = all
        await context.SaveChangesAsync();
    }
}
```

### Service / controller deltas

| Area | Change |
|------|--------|
| `EventService.CreateEventAsync` (L86-97) | add `Status = EventStatus.Pending` to the `new Event { … }` initializer (EA-002; `CreateEventRequest` has no `Status` ⇒ no client override) |
| `EventService.GetAllPublishedEventsAsync` (L177-181) | after the expired `Where`, add `query = query.Where(e => e.Status == EventStatus.Approved);` (translatable predicate, EHE-002) |
| `EventService.GetEventByIdAsync` | **unchanged** (D-2) |
| `EventService.MapToEventWithAvailabilityAsync` (L855-867) | add `Status = eventEntity.Status` |
| `EventController.GetEvent` (L32-44) | post-read: `if (eventDetails.Status != EventStatus.Approved) return NotFound(new { error = "Event not found" });` before `return Ok(eventDetails);` (D-2, EHE-003) |
| `IEventService.EventWithAvailability` (L181) | add `public EventStatus Status { get; set; }` |
| `IAdminService` | add `Task<EventSummary> ApproveEventAsync(Guid eventId);`, `Task<EventSummary> RejectEventAsync(Guid eventId, string? reason);`, `Task<PagedResult<EventSummary>> GetPendingEventsAsync(int page, int pageSize);` |
| `AdminService.ApproveEventAsync/RejectEventAsync` | `_context.Events.FindAsync(eventId)` → null ⇒ `KeyNotFoundException`; set `Approved`/`Rejected`; `SaveChanges`; return `EventSummary{…,Status}`. Reason is audit-only (not stored). |
| `AdminService.GetAllEventsAsync` projection (L77-85) | add `Status = e.Status` |
| `AdminService.GetPendingEventsAsync` | `.Where(e=>e.Status==EventStatus.Pending).OrderBy(e=>e.CreatedAt)` paginated |
| `IMetricsService.EventMetrics` (L26) + `MetricsService.GetOrganizerMetricsAsync` projection (L132-142) | add `Status` (no status/expiry filter — EHE-006) |
| `Models/AuditLog.cs` | `AuditActionType`: add `ApproveEvent, RejectEvent` (varchar-stored ⇒ NO migration, EA-003/004) |
| `AdminController` | two endpoints (below). No new injection (`IAdminService`+`IAuditLogService`+`TryLogAuditAsync(adminId,…)` already present). Class-level `RequireAdminRole` ⇒ non-admin 403. |

```csharp
[HttpPost("events/{eventId:guid}/approve")]
public async Task<IActionResult> ApproveEvent(Guid eventId) {
    if (!TryGetUserId(out var adminId)) return Unauthorized();
    try {
        var summary = await _adminService.ApproveEventAsync(eventId);                 // KeyNotFound ⇒ NO audit (EA-003)
        await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.ApproveEvent,
            AuditResourceType.Event, eventId, Truncate($"Admin approved event {eventId}", 1000)));
        return Ok(summary);
    } catch (KeyNotFoundException) { return NotFound(new { error = "Event not found" }); }
    catch (Exception ex) { _logger.LogError(ex,"Error approving event {EventId}",eventId); return StatusCode(500,new {error="An error occurred while approving the event"}); }
}

[HttpPost("events/{eventId:guid}/reject")]
public async Task<IActionResult> RejectEvent(Guid eventId, [FromBody] RejectEventRequest? request) {
    if (!TryGetUserId(out var adminId)) return Unauthorized();
    try {
        var summary = await _adminService.RejectEventAsync(eventId, request?.Reason);  // reason optional (EA-004)
        var details = Truncate($"Admin rejected event {eventId}{(request?.Reason is {Length:>0} r ? $": {r}" : "")}", 1000);
        await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.RejectEvent, AuditResourceType.Event, eventId, details));
        return Ok(summary);
    } catch (KeyNotFoundException) { return NotFound(new { error = "Event not found" }); }
    catch (Exception ex) { _logger.LogError(ex,"Error rejecting event {EventId}",eventId); return StatusCode(500,new {error="An error occurred while rejecting the event"}); }
}
public record RejectEventRequest(string? Reason = null);   // audit-only, ≤1000 truncated
```

### Exception → HTTP mapping (AdminController approve/reject)

| Service exception | HTTP | Body | Spec |
|---|---|---|---|
| `KeyNotFoundException` (unknown event) | 404 | `{error:"Event not found"}` — **no audit** | EA-003 "Unknown event" |
| Non-admin at pipeline | 403 | — (class-level `RequireAdminRole`) | EA-003/004 "Non-admin" |
| Other | 500 | `{error:"An error occurred …"}` | existing pattern |

## Frontend Design

- **`lib/eventStatus.js` (new, shared util + tested)** — `statusBadgeVariant(status)`: `pending→'warning'`, `approved→'success'`, `rejected→'error'`; `statusLabel(status)`: `Pendiente`/`Aprobado`/`Rechazado`. Reused by `AdminPanel` + `OrganizerDashboard`. Backend already sends the string (D-1).
- **`lib/queryKeys.js`** — add `adminEvents: ['admin-events']` (catalog invalidation tracking; v1 AdminPanel still manual-fetch).
- **`AdminPanel.jsx`** (admin-only, no role gate needed): header `Eventos ({events.length})` + a warning `Badge` `Pendientes: N` where `N = events.filter(e=>e.status==='Pending').length`. New `Estado` column ⇒ `<Badge variant={statusBadgeVariant(e.status)}>{statusLabel(e.status)}</Badge>`. Per-row: `Approve` button when `status!=='Approved'`; `Reject` button when `status!=='Rejected'` (both on Pending; Approve-only on Rejected ⇒ EA-005 re-publish; Reject-only on Approved ⇒ hide). Handlers `await apiClient.post(\`/admin/events/\${id}/approve\` …)` / `…/reject`, {reason}. On 200: `queryClient.invalidateQueries(['events'])` + `['event', id]` + `loadData()` refetch (D-6). On error: `setFeedback({type:'error', …})`, **state unchanged** (EA-008). Add `addApprovalTarget`/`busyApprovalId` state to disable the clicked row's buttons.
- **`OrganizerDashboard.jsx`** (D-8): `const { user } = useAuth()`; new `Estado` column `Badge`. `Editar` button rendered only when `user?.role==='Admin'` (hidden for `Organizador` — UI-only; backend `EventOwnership` unchanged). Metrics/list remain unfiltered by expiry+status (EHE-006; `GetOrganizerMetricsAsync` already unfiltered).
- **`EventForm.jsx`** (EA-009): in create mode (L142-144) change the success copy to `'Evento creado correctamente. Queda pendiente de aprobacion.'`. Edit-mode copy unchanged.
- **`api/client.js`** — **unchanged** (D-7); the shared axios instance already handles `X-CSRF-PROTECT` on POST + 401 redirect.
- **`Badge.jsx`** — **unchanged**; `success|warning|error` variants already cover the mapping.

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `backend/Models/EventStatus.cs` | Create | `EventStatus` enum + `[JsonStringEnumConverter]` (D-1) |
| `backend/Models/Event.cs` | Modify | add `Status` prop (default `Pending`) |
| `backend/Data/ApplicationDbContext.cs` | Modify | `OnModelCreating` Event entity: `.Property(e=>e.Status).IsRequired();` (D-3) |
| `backend/Migrations/*_AddEventApproval.cs` + `.Designer.cs` | Create | `AddColumn<int>(Status, default 0)` + hand-wired backfill `try/catch`; `Down` drops column |
| `backend/Data/EventApprovalBackfill.cs` | Create | best-effort `RunAsync` ALL→Approved (D-9) |
| `backend/Services/IEventService.cs` | Modify | `EventWithAvailability.Status` (EA-007) |
| `backend/Services/EventService.cs` | Modify | `CreateEventAsync` Pending; `GetAllPublishedEventsAsync` Where Approved; mapper Status (D-2) |
| `backend/Controllers/EventController.cs` | Modify | `GetEvent` post-read non-`Approved` 404 (D-2, EHE-003) |
| `backend/Services/IAdminService.cs` | Modify | `EventSummary.Status`; `Approve`/`Reject`/`GetPendingEvents` methods (EA-003/004/007) |
| `backend/Services/AdminService.cs` | Modify | impl approve/reject/get-pending; projection Status |
| `backend/Controllers/AdminController.cs` | Modify | 2 endpoints + `RejectEventRequest` record (EA-003/004) |
| `backend/Models/AuditLog.cs` | Modify | `AuditActionType` += `ApproveEvent`,`RejectEvent` (no migration) |
| `backend/Services/IMetricsService.cs` | Modify | `EventMetrics.Status` (EA-007) |
| `backend/Services/MetricsService.cs` | Modify | `GetOrganizerMetricsAsync` projection Status (EHE-006) |
| `backend/Migrations/ApplicationDbContextModelSnapshot.cs` | Regenerate | by `dotnet ef migrations add` |
| `frontend/src/lib/eventStatus.js` | Create | `statusBadgeVariant`/`statusLabel` |
| `frontend/src/lib/queryKeys.js` | Modify | add `adminEvents` key |
| `frontend/src/pages/AdminPanel.jsx` | Modify | `Estado` column + pending count + Approve/Reject + `useQueryClient` invalidation (EA-008) |
| `frontend/src/pages/OrganizerDashboard.jsx` | Modify | `Estado` `Badge` + hide `Editar` for organizer (EA-009) |
| `frontend/src/components/EventForm.jsx` | Modify | create success "pendiente de aprobacion" copy (EA-009) |

## Testing Strategy

| Layer | File | Key cases (TDD red→green) | Approach |
|-------|------|---------------------------|----------|
| Unit (service) | `backend/Tests/EventServiceTests.cs` | create→Pending; `GetAllPublishedEventsAsync` excludes Pending+Rejected (future-dated); `GetEventByIdAsync(includeExpired:true)` returns Pending (manage); mapper sets Status | InMemory + Moq |
| Unit (controller) | `backend/Tests/EventControllerTests.cs` | `GetEvent` 404 pending future / 404 rejected future / 200 approved future; `GetAllEvents` delegates (excludes pending) | `Mock<IEventService>` |
| Unit (controller) | `backend/Tests/AdminControllerTests.cs` | approve 200 + audit `ApproveEvent`(resourceId); reject w/ reason 200 + audit `RejectEvent` Details has reason (truncated); reject no reason 200; unknown 404 + **no audit**; non-admin 403 | `Mock<IAdminService>`/`Mock<IAuditLogService>` + `WebApplicationFactory<Program>` for 403 (existing pattern) |
| Unit (controller) | `backend/Tests/MetricsControllerTests.cs` | `GetOrganizerMetrics` each item carries `Status` | `Mock<IMetricsService>` |
| Property (FsCheck) | `backend/Tests/AdminPropertyTests.cs` (extend) | ∀ status ∈ {Pending,Approved,Rejected}: approve→Approved, reject→Rejected succeed (no blocked transition — EA-005); `GetPendingEvents` returns only Pending | `FsCheck.Xunit` |
| Unit (backfill) | `backend/Tests/EventApprovalBackfillTests.cs` (new) | `RunAsync` sets ALL existing → Approved; empty db no-op | InMemory (mirrors `TicketReservationBackfillTests`) |
| Frontend | `frontend/src/pages/OrganizerDashboard.test.jsx` | status `Badge` per row (3 variants); `Editar` hidden for `Organizador`, shown for `Admin` (`useAuth` mocked) | `MemoryRouter`, `vi.mock` api client + `useAuth` |
| Frontend | `frontend/src/pages/AdminPanel.test.jsx` | pending-count badge; Approve/Reject shown per status; approve success → refetch + `Approved` badge; failure → error, state unchanged | `vi.mock('../api/client.js')`, spy `loadData`/`useQueryClient` |
| Frontend | `frontend/src/components/EventForm.test.jsx` | create success shows "pendiente de aprobacion"; edit copy unchanged | `vi.mock('../api/client.js')` |
| Frontend | `frontend/src/lib/__tests__/eventStatus.test.js` (new) | variant + label mapping | pure util |

Run: backend `dotnet test` from `backend/`; frontend `npx vitest run` from `frontend/` (skill
`dotnet-testing` strict Red→Green; skill `react-testing` vitest + Testing Library + `vi.mock`).

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or
process-integration boundary. Approve/reject are standard `[ApiController]` actions on the
existing `AdminController`; no `Process.Start`, no dynamic routing, no shell. (Class-level
`RequireAdminRole` is the sole authz boundary; covered by its existing 403 path.)

## Migration / Rollout

Manual EF migration (skills `efcore-data`): `dotnet ef migrations add AddEventApproval`
(uses `ApplicationDbContextFactory` → `MigrationConnection:5432`), hand-wire the best-effort
backfill, then `database update` with `ASPNETCORE_ENVIRONMENT=Development`. No feature flag
(no `HideExpiredEvents`-style switch needed; gating is the enum itself). Safe rollback:
`dotnet ef database update <prior>` drops `Status` (D-9 backfill left events `Approved`, so a
revert to prior code re-shows everything publicly — the pre-change behavior). Edge case: a
`Pending` event created after deploy but before approval would, on rollback, become publicly
visible again — accepted (it is the pre-approval baseline).

## Implementation Order (work units, TDD red→green each)

1. **Model + migration**: `EventStatus`(+attr), `Event.Status`, `OnModelCreating`; generate
   `AddEventApproval`; `EventApprovalBackfill`+wire `Up()`/`Down()`. RED:
   `EventApprovalBackfillTests` → GREEN.
2. **DTOs + EventService**: add `Status` to `EventSummary`/`EventWithAvailability`/`EventMetrics`;
   mappers; `CreateEventAsync` Pending; `GetAllPublishedEventsAsync` Where Approved. RED:
   `EventServiceTests` + `MetricsControllerTests` → GREEN.
3. **AdminService + AdminController approve/reject + audit enums**: `AuditActionType`
   members; `IAdminService`/`AdminService` approve/reject/get-pending; `AdminController`
   endpoints + `RejectEventRequest`. RED: `AdminControllerTests` → GREEN.
4. **Public-detail filter**: `EventController.GetEvent` post-read 404. RED:
   `EventControllerTests` + `AdminPropertyTests` transition property → GREEN.
5. **Frontend utils + AdminPanel**: `lib/eventStatus.js` + `queryKeys.adminEvents`;
   `AdminPanel` status badge + pending count + Approve/Reject + invalidation. RED:
   `AdminPanel.test.jsx` + `eventStatus.test.js` → GREEN.
6. **Frontend OrganizerDashboard**: `Estado` Badge + hide `Editar` for organizer. RED:
   `OrganizerDashboard.test.jsx` → GREEN.
7. **Frontend EventForm**: create success "pendiente de aprobacion" copy. RED:
   `EventForm.test.jsx` → GREEN.
8. **Verify**: `dotnet test`; `npx vitest run`; apply migration in dev; manual catalog/direct-URL
   check (pending 404, approved visible).

## Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| Status serializes as int (frontend `Badge` expects string) without the per-enum converter | Med | D-1: `[JsonStringEnumConverter]` on `EventStatus` + assert in `eventStatus.test.js`; verified `Program.cs` has no global converter. |
| Future caller of `GetEventByIdAsync` assumes buyer-status-filtered | Low | D-2: keep filter in the public route only; comment + tests for both routes (public 404 vs manage 200). |
| Organizer edits a Pending event via the API (UI-only hide) | Accepted | EA-009/proposal: `EventOwnership` backend unchanged — documented limitation. |
| Backfill fails and aborts the migration | Low | D-9: `try/catch` + `Console.Error.WriteLine` (EA-006); `EventApprovalBackfillTests` proves load+set+save. |
| `AddColumn` NOT NULL default mismatch with `ModelSnapshot` (phantom diff) | Low | D-3: EF diffs model↔snapshot (history defaultValue not re-diffed); review generated migration before applying. |
| Approve/reject vs concurrent reservation has no row lock | Low | Admin-only moderation, low contention; `Event` has no `RowVersion` — simple single-row update; note only. |
| `useAuth().user.role` shape drifts | Low | D-8: matches `Navbar`/`RoleGuard` (`user?.role` string); `OrganizerDashboard.test.jsx` mocks it for both roles. |

## Open Questions

- [ ] None blocking. Minor: confirm the exact "pendiente de aprobacion" copy wording with UX at apply time; confirm whether `GetPendingEventsAsync` paging (v1 unused by AdminPanel, derived-client-side) is wanted now or deferred.