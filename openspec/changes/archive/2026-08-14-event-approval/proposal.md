# Proposal: Event Approval

## Intent / Problem

The public catalog (`GET /api/events`) and public detail (`GET /api/events/{id}`) expose every organizer-created event instantly — there is no moderation gate. `Event` carries no status field today (exploration obs #473).

**Why**: buyer-facing trust/safety. Without an approval step, any organizer can publish unchecked/promo/error content visible to all buyers immediately. Admin MUST approve each event before it becomes public.

## Scope

### In Scope
- `Event.Status` enum (`Pending | Approved | Rejected`); new events default to `Pending` (`EventStatus` follows the existing `ReservationStatus`/`TransactionStatus` pattern).
- Public catalog + public detail MUST require `Status == Approved`, in addition to the existing future-date filter (EHE-002).
- Admin-only Approve/Reject endpoints (`RequireAdminRole`, inherited at `AdminController` class level); audit via `TryLogAuditAsync`. Optional rejection reason (not required).
- Admin panel: pending count badge + Approve/Reject row actions; reuse `Badge.jsx` (`pending=warning`, `approved=success`, `rejected=error`).
- Organizer dashboard: status badge per event (via `EventMetrics` DTO); hide Edit entry for organizers. Backend edit paths (`UpdateEventAsync`, `ReplaceEventImageAsync`) stay intact so admin keeps edit.
- Migration: add `Status` column (default `Pending`); backfill existing events → `Approved` inside `Up()`.

### Out of Scope
- Email rejection notice (communication handled externally), mandatory rejection reason, automated re-upload workflow.
- Revoking organizer edit authority at the API (`EventOwnership` unchanged) — known limitation.
- New "my events" page; backend edit-policy changes.

## Capabilities

### New Capabilities
- `event-approval`: event moderation lifecycle — create-in-pending, admin approve/reject, status-based visibility rules, backfill.

### Modified Capabilities
- `catalog-filtering` (EHE-002, EHE-003): public list/detail MUST also require `Status == Approved`; the management variant stays unfiltered by status (mirrors the EHE-003 expiry pattern; manage + POST-201 depend on it).
- `role-access` (EHE-006): organizer endpoints + management variant MUST return events regardless of status, so an organizer sees own pending/rejected events.

## Approach

Add `EventStatus` enum + `Event.Status` (`OnModelCreating`). `CreateEventAsync` sets `Pending`. Apply status filtering at the public route/`GetAllPublishedEventsAsync`, never inside the shared `GetEventByIdAsync` (manage + POST-201 rely on it unfiltered). New `AdminService` approve/reject methods + endpoints; add `Status` to `EventSummary` and `EventMetrics`. Manual EF migration with backfill via `ApplicationDbContextFactory` + best-effort try/catch (repo pattern). Frontend adds badges + actions.

## Affected Areas

| Area | Impact |
|------|--------|
| `backend/Models/Event.cs`, `Models/EventStatus.cs` | New |
| `backend/Data/ApplicationDbContext.cs` (`OnModelCreating`) | Modified |
| `backend/Services/EventService.cs` (`CreateEventAsync`, `GetAllPublishedEventsAsync`, `MapToEventWithAvailabilityAsync`) | Modified |
| `backend/Controllers/EventController.cs` (public detail filter) | Modified |
| `backend/Controllers/AdminController.cs`, `Services/AdminService.cs`, `IAdminService.cs` (`EventSummary`) | Modified |
| `backend/Services/MetricsService.cs` + `EventMetrics` DTO | Modified |
| `backend/Migrations/*AddEventApproval*.cs` | New |
| `frontend/.../AdminPanel.jsx`, `OrganizerDashboard.jsx`, `components/ui/Badge.jsx` | Modified |

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Direct URL exposes pending detail | Med | Filter at public route, not shared `GetEventByIdAsync` (POST-201/manage depend on it) |
| Organizer edits pending event via API | Accepted | Known limitation per scope; UI-only hide, API authority unchanged |
| Backfill scope ambiguity | Low | Set ALL existing rows → `Approved` (assumption, confirm in spec) |
| Allowed status transitions undefined | Low | Admin may flip any status; spec to define transitions |

## Rollback Plan

Revert via `dotnet ef database update` to the prior migration (drops `Status`); remove new endpoints/columns. Safe: backfilled events stay `Approved`, so rollback under prior code does not hide any previously-public content.

## Dependencies

- EF Core manual migration (no auto-migrate at startup); `ApplicationDbContextFactory` for backfill.

## Success Criteria

- [ ] New event created as `Pending`; absent from public list/detail until `Approved`.
- [ ] Admin Approve → event appears publicly; Reject → stays hidden.
- [ ] Only `Admin` role may approve/reject (Staff/Organizer get 403).
- [ ] Organizer dashboard shows status badge; Edit entry hidden for organizers.
- [ ] Migration backfills existing events to `Approved`.
- [ ] `dotnet test` (backend) and `vitest` (frontend) green; no email sent; no mandatory rejection reason.

## Open Questions (Resolved)

- No email rejection notice (handled externally); no mandatory rejection reason.
- Backend edit policy unchanged (admin keeps edit; organizer Edit hidden in UI only).
- Backfill scope = all existing events → `Approved` (residual assumption to confirm in spec/design).