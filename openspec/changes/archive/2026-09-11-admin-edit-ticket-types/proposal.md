# Proposal: Admin Edit of Pre-Approval Ticket Types

## Intent / Why

Admins need to correct pre-approval availability, not only increase stock: names, prices, quantities, additions, and deletions, while preserving public visibility (EA-001/EA-005, EHE-002/EHE-003).

## Scope

### What Changes / In Scope
- Add atomic `PUT /api/admin/events/{id}/ticket-types` replacing the complete list.
- Allow full edit for `Pending` and `Rejected` without commercial history; keep `Approved` add-only.
- Add `EditTicketsModal`, status-aware AdminPanel action/label/icon, management loading, and invalidation.
- Add audit action, tests, PEM-002 bookkeeping (six → seven), and ATS changes.

### Out of Scope
- Schema migration or organizer `EventForm` changes.
- Fixing latent `AddTicketsModal` `useEvent`; the new modal MUST use `useManagementEvent`.
- Hardening `AddTicketStockAsync`/`AddTicketTypeAsync` to reject Pending/Rejected. This changes behavior/tests and is a separate follow-up; add-only behavior remains.

## Capabilities

### New Capabilities
- `admin-ticket-availability-edit`: Atomic admin replacement of ticket types for eligible pre-approval events.

### Modified Capabilities
- `admin-ticket-stock`: Extend ATS-001/005/006/007/009 with full-edit behavior and audit/UI contracts.
- `past-event-mutation-guard`: Extend PEM-002 from six to seven mutation endpoints.

## Approach

Implement one transactional replacement with EF execution-strategy patterns and `EventFinalizedGuard` (PEM-001). Guard order (ratified at spec time): finalized (`EventFinalizedGuard`, PEM-001) → status → payload → ownership/reference checks. Return `TicketTypeWithAvailability[]`; audit with `TryLogAuditAsync` and a varchar-backed `AuditActionType`. Pending and Rejected without history allow full edit; Rejected with history returns RFC 7807 409 and retains add-only; Approved is unchanged (EA-005, EHE-003). History means sold `Ticket` and/or an inventory-occupying reservation; exact predicate, including expired/released reservations, is pinned by spec/design.

Spec/design MUST resolve price `>=0` vs `>0`, cap semantics (per-type total ≤1000 vs legacy accumulated totals), history predicate, and atomicity test failure injection. Invalidate management, event, and events queries; use a distinct ticket-edit icon from “Editar”.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `backend/{Services,Controllers,Models}` | New/Modified | Endpoint, guards, DTOs, audit, tests; no migration |
| `frontend/src/{pages,components}` | New/Modified | Modal and status-aware action |
| `openspec/specs` | Delta | ATS, PEM, capability |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Referenced type deletion / partial writes | Med | History 409, transaction rollback, integration tests |
| Cap or price regression | Med | Pin canonical rules in spec before implementation |

## Rollback Plan

Revert endpoint/service/UI/test commits together; no schema rollback is required. Restore the prior AdminPanel action path.

## Dependencies

- Management endpoint, EF patterns, `useManagementEvent`, and ATS/PEM/EA/EHE specs.

## Success Criteria

- [ ] Eligible Pending/Rejected events replace ticket types atomically; blocked Rejected-with-history returns 409.
- [ ] Approved events retain add-only behavior; audits, availability, caches, and accessibility tests pass.
- [ ] `dotnet test` and frontend Vitest pass.

## Review Workload Forecast

Single PR; approximately 350–400 changed lines, within the 2,000-line review budget. No chained PR recommended.
