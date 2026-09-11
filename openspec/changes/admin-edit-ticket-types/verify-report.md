# Verify Report: Admin Edit of Pre-Approval Ticket Types

**Change**: `admin-edit-ticket-types`
**Branch**: `feat/admin-edit-ticket-types` (base `974bdfc`; verified candidate `0690aea`, 5 commits)
**Method**: Inline verification by the orchestrator (the delegated verify run was cancelled by the user before completion; suites were executed directly on the candidate).
**Date**: 2026-09-11

```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:8d817c2d08d3100a894a2d72f285c353c2a69c5b6f84696c734fde9044873f32
verdict: pass
blockers: 0
critical_findings: 0
requirements: 17/17
scenarios: 39/39
test_command: dotnet test (backend/); npm test (frontend/)
```

## Test Evidence

| Suite | Command | Result |
|---|---|---|
| Backend full | `cd backend && dotnet test` | **782 passed / 4 failed / 786** — the 4 failures are pre-existing baseline (below) |
| Frontend full | `cd frontend && npm test` | **530 passed / 0 failed (50 files)** |
| Focused replacement service | `--filter FullyQualifiedName~EventServiceTicketStockTests` | 39/39 (apply) — re-run green in full suite |
| Focused controller | `--filter FullyQualifiedName~AdminControllerTicketStockTests` | 25/25 (apply) — re-run green in full suite |
| Baseline repro @ `974bdfc` | filtered run in a throwaway worktree (with `appsettings.Development.json` present) | **4 failed / 0 passed / 4** — all four fail on untouched base code |

### Baseline failures (pre-existing — NOT regressions)

Independently reproduced at base `974bdfc` in a throwaway worktree:

1. `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`
2. `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`
3. `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`
4. `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`

None of these code paths (payment webhook signature, pending-email retry, CSRF middleware) is touched by this change.

## Requirement Coverage

| Requirement | Evidence (tests / code) | Status |
|---|---|---|
| ATE-001 Atomic replacement | `ReplaceTicketTypesAsync_MixedAddEditDelete…`, `_EmptyList…`, `_InvalidPayload…` (no mutation), `_SaveFailure_RollsBackEntireReplacement` (fault-injecting context), controller `ValidRequest_ReturnsOkAndAuditsExactlyOnce` | ✅ |
| ATE-002 Eligibility + guard order | `_ApprovedEvent_ThrowsTicketTypesNotEditable_NoMutation`, `_PastApprovedEvent_ThrowsEventFinalized_NotNotEditable`; controller `ConflictOutcomes…`, `ApprovedEvent_Returns409ProblemDetails` | ✅ |
| ATE-003 Universal history predicate | `_PendingWithTicketHistory…`, `_PendingWithReservationHistory…`, `_RejectedWithRefundedTicket…` (all `ticket-types-referenced`), `_RejectedWithoutHistory_Succeeds` | ✅ |
| ATE-004 Payload validation | `_InvalidPayload…`, `_NameTooLong…`, `_LegacyTotalAboveCap…`, `_PriceZero_IsAccepted`; modal name/quantity validation tests | ✅ |
| ATE-005 Id reference validation | `_ForeignId_ThrowsArgumentException_NoMutation` | ✅ |
| ATE-006 Response contract | `_MixedAddEditDelete…` recomputes availability; controller returns `TicketTypeWithAvailability[]` | ✅ |
| ATE-007 Error mapping | controller 404/400/409×3/500, `ConflictOutcomes…_ProblemDetails409`, WAF `application/problem+json` with `instance`, `OrganizerRole_Returns403` | ✅ |
| ATE-008 Audit logging | `ValidRequest…AuditsExactlyOnce`, `AuditDetailsTruncated…`, no-audit variants on 400/404/409/500 | ✅ |
| ATE-009 EditTicketsModal contract | 8 Vitest tests: `useManagementEvent` load, loading, complete-list PUT + three-key invalidation (payload includes `price: 0`), first-error focus + `role="alert"`, cap, 409 copy, generic error, cancel | ✅ |
| ATE-010 AdminPanel action | 4 tests: distinct "Editar entradas" for Pending and Rejected, "Agregar entradas" for Approved only, past disabled; blocked copy in modal 409 test | ✅ |
| ATE-011 Test coverage | New focused suites all green; full suites as above | ✅ |
| ATS-001 Admin-only | WAF `OrganizerRole_Returns403` + `NoAuthenticatedUser_ReturnsUnauthorized` | ✅ |
| ATS-005 Audit logging | Audit-once and truncation tests; varchar enum, no migration | ✅ |
| ATS-006 Availability recalculates | Service availability recompute tests + frontend three-key invalidation tests | ✅ |
| ATS-007 Admin UI operations | `AddTicketsModal` 3 tests (three-key invalidation incl. `managementEvent`), `EditTicketsModal` invalidation test, failure-without-mutation tests | ✅ |
| ATS-009 Test coverage | `AddTicketsModal`, `EditTicketsModal`, `AdminPanel` Vitest coverage present | ✅ |
| PEM-002 Seven endpoints | `_PastApprovedEvent_ThrowsEventFinalized_NotNotEditable`; AdminPanel past-disabled test | ✅ |

## Scope Verification

- No schema migration; no canonical `openspec/specs/` edits; no organizer `EventForm` changes; add-only methods untouched (`EventService.cs` diff is +171/−0 on the new method).
- Reviewed deviations from design are consistent with intent: explicit `instance` on 409 ProblemDetails; batched `MapTicketTypesWithAvailabilityAsync` (no N+1); add-only copy carried in `TicketTypesReferencedException` + modal 409 special-case.

## Findings

- **CRITICAL**: none.
- **WARNING**: none.
- **SUGGESTION**: `tasks.md` 6.1 wording ("full suite green") should be read together with the documented baseline exception in `apply-progress.md`; no code action required.

## Verdict

**PASS** — the implementation matches the ATE/ATS/PEM spec deltas; suites green except the four independently re-established baseline failures, which are not attributable to this change.
