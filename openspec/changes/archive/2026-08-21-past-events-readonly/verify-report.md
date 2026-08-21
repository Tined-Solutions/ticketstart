```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:3873045e73cffd78cac426787f92da4c90553f29911c8ac5067f6b021b6919c4
verdict: fail
blockers: 0
critical_findings: 0
requirements: 14/14
scenarios: 39/39
test_command: dotnet test
test_exit_code: 1
test_output_hash: sha256:9d56e402c2e97f5b42f08caab2d4e2ca424746b8ed2ebcee055621a95e956b7b
build_command: dotnet build
build_exit_code: 0
build_output_hash: sha256:bcc72e7c3ae7089b0573f804465b44647933f1fe1b290eeb81aa226209655e6f
```

## Verification Report

**Change**: past-events-readonly — Past Events Read-Only (Event Immutability)
**Version**: N/A (delta specs, 5 domains)
**Mode**: Strict TDD (backend) + manual verification (frontend, no test runner per config.yaml)
**Date**: 2026-08-21
**Branch**: `fix/admin-past-events-edit` (4 work-unit commits cb8c5cd→9b6ae7b on baseline 599642b)
**Artifact store**: hybrid (openspec file + Engram)

### Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 22 |
| Tasks complete | 22 |
| Tasks incomplete | 0 |
| Spec requirements (actual) | 14 |
| Spec scenarios (actual) | 39 |

### Build & Tests Execution

**Build** (`dotnet build` from `backend/`): ✅ Passed — exit 0, 0 warnings, 0 errors
```text
0 Warning(s)
0 Error(s)
Time Elapsed 00:00:04.26
```

**Tests** (`dotnet test` from `backend/`): ❌ 678 passed / 6 failed / 0 skipped / 684 total — exit 1
```text
Failed!  - Failed:     6, Passed:   678, Skipped:     0, Total:   684
```
All 6 failures are **pre-existing / environmental, NOT regressions** (see Issues):

1. `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` — documented pre-existing.
2. `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client` — documented pre-existing.
3. `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` — documented pre-existing.
4. `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` — documented pre-existing.
5. `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` — documented pre-existing (live-DB environment-driven).
6. `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` — wall-clock timing assertion (`Enqueue took 1369ms, expected <1000ms`); file untouched by this change (last modified in `d94ffed`, pre-baseline) and **reproduced at baseline commit 599642b in an isolated worktree run (1526ms > 1000ms)** → pre-existing flaky/environmental.

A first full run of the same suite passed 679/5 (the timing test passed then; `QRCodePropertyTests.Property21`, documented flaky, passed in both runs). Zero failures were caused by this change.

**Coverage**: ➖ Not available — no coverage tool configured (`coverage_available: false`).

### Spec Compliance Matrix

| Requirement | Scenario | Test / Evidence | Result |
|-------------|----------|-----------------|--------|
| PEM-001 (3) | expired-throws | `EventFinalizedGuardTests.EnsureMutable_ExpiredEvent_ThrowsEventFinalizedException` (Tests/EventFinalizedGuardTests.cs:30) | ✅ COMPLIANT |
| PEM-001 | active-passes | `EnsureMutable_ActiveEvent_DoesNotThrow` (:45) | ✅ COMPLIANT |
| PEM-001 | exact-instant-mutable | `EnsureMutable_ExactInstant_DoesNotThrow` (:56) — strict `<` (Models/Event.cs:28) | ✅ COMPLIANT |
| PEM-002 (2) | each-mutation-409 | `EventControllerTests` 7 endpoint tests (:476,:515,:536,:557,:577,:596,:619) + `AdminControllerTests` 4 mock tests (:782,:805,:825,:845) | ✅ COMPLIANT |
| PEM-002 | rfc7807-problem-details | Update test asserts `application/problem+json`, `type`, `title`, `status:409` (:500-506); `instance` framework-generated, not directly asserted | ✅ COMPLIANT (SUGGESTION: `instance` unasserted) |
| PEM-003 (2) | no-side-effects-on-reject | `EventServiceImmutabilityTests` (no save/`EnqueueAsync Never`/S3 `Never`/Quantity unchanged/no row; :117,:179,:198,:218,:238), `AdminServiceTests` (no status flip; :66,:100), `AdminControllerTests` (audit `Times.Never`) | ✅ COMPLIANT |
| PEM-003 | future-still-side-effects | `UpdateEventAsync_FutureEvent_StillSucceeds` (:148), `ApproveEventAsync_FutureEvent_StillFlipsToApproved`/Reject (:82,:116) | ✅ COMPLIANT |
| PEM-004 (1) | flag-independent | `UpdateEventAsync_PastEvent_FlagDisabled_StillThrows` (:137) + no `HideExpiredEvents` reference in guard (Guards/EventFinalizedGuard.cs) | ✅ COMPLIANT |
| PEM-005 (2) | consultation-ok | `GetEventById_ManagementIncludeExpired_200` (:358), `Organizer_ManagementEvent_Expired_200` (:379), inline manage-200 in Update/Approve tests (:487,:614) | ✅ COMPLIANT |
| PEM-005 | purchases-refunds-ok | Static: no guard in `AdminPurchaseService`/`GetPurchases`/`RefundPurchase` (grep, AdminController.cs:252/:288); existing purchase/refund suite green | ✅ COMPLIANT (no dedicated new past-event test — SUGGESTION) |
| PEC-001 (2) | roles-open-ver | `EventReadOnlyView.jsx` (management fetch + `EventForm readOnly`) + App.jsx:85-92 route RoleGuard `['Organizador','Admin']` | ✅ COMPLIANT (manual, no frontend runner) |
| PEC-001 | no-mutation-affordances | `EventForm.jsx` readOnly: inputs disabled (:264,:283,:302,:320), file input hidden (:336), submit hidden (:491) | ✅ COMPLIANT (manual) |
| PEC-002 (2) | past-unapproved-loads | `useManagementEvent` → `/events/{id}/manage` (hooks/useManagementEvent.js:9); Pending past event + manage 200 (EventControllerTests.cs:614-615) | ✅ COMPLIANT |
| PEC-002 | non-authorized-denied | `EventOwnershipHandler` unchanged (Authorization/EventOwnershipHandler.cs:44 admin, :67 ownership); existing 403/401 tests green | ✅ COMPLIANT |
| PEC-003 (1) | no-side-effects | View issues only the GET; `GetEventByIdAsync` read-only; no mutation/save/audit path | ✅ COMPLIANT |
| PEC-004 (2) | compras-enabled | AdminPanel.jsx:446-454 Compras enabled, no `disabled`, navigates to purchases | ✅ COMPLIANT (manual) |
| PEC-004 | metricas-enabled | OrganizerDashboard.jsx:264-272 Metricas enabled | ✅ COMPLIANT (manual) |
| EA-003 (4) | admin-approves | `ApproveEventAsync_FutureEvent_StillFlipsToApproved` + audit via `TryLogAuditAsync` (AdminController.cs:338-343) | ✅ COMPLIANT |
| EA-003 | approve-past-rejected | `ApproveEventAsync_PastEvent_ThrowsEventFinalized_NoStatusFlip` + EventControllerTests:596 + AdminControllerTests:782 (no audit) | ✅ COMPLIANT |
| EA-003 | non-admin-rejected | Class-level `RequireAdminRole` (AdminController.cs:14, reflection test :770-774); existing 403 tests green | ✅ COMPLIANT |
| EA-003 | unknown-event | `ApproveEvent_UnknownEvent_ReturnsNotFound_NoAudit` (AdminControllerTests.cs:701) | ✅ COMPLIANT |
| EA-004 (4) | reject-with-reason | `RejectEventAsync_PastEvent_ThrowsEventFinalized_NoStatusFlip` (reason optional); truncation ≤1000 pre-existing | ✅ COMPLIANT |
| EA-004 | reject-past-rejected | `RejectEventAsync_PastEvent` + EventControllerTests:619 + AdminControllerTests:805 (no audit) | ✅ COMPLIANT |
| EA-004 | reject-without-reason | `RejectEventAsync_FutureEvent_StillFlipsToRejected` (reason null, :116) | ✅ COMPLIANT |
| EA-004 | non-admin-rejected | `RequireAdminRole` class-level; `RejectEvent_UnknownEvent_ReturnsNotFound_NoAudit` (:719); existing 403 tests green | ✅ COMPLIANT |
| ATS-002 (4) | happy-path-increment | Existing `EventServiceTicketStockTests`/`AdminControllerTicketStockTests` green | ✅ COMPLIANT |
| ATS-002 | increment-past-rejected | EventControllerTests:557 + AdminControllerTests:825 + `AddTicketStockAsync_PastEvent_ThrowsEventFinalized_QuantityUnchanged` (ImmutabilityTests:218) | ✅ COMPLIANT |
| ATS-002 | unknown-event-mismatch | Existing 404 tests green (controller catch :198) | ✅ COMPLIANT |
| ATS-002 | invalid-quantity | Existing 400 tests green (validation :297-301) | ✅ COMPLIANT |
| ATS-004 (3) | happy-path-new-type | Existing `AddTicketType` tests green | ✅ COMPLIANT |
| ATS-004 | new-type-past-rejected | EventControllerTests:577 + AdminControllerTests:845 + `AddTicketTypeAsync_PastEvent_ThrowsEventFinalized_NoRowCreated` (:238) | ✅ COMPLIANT |
| ATS-004 | invalid-payload | Existing 400 tests green (validation :401-414) | ✅ COMPLIANT |
| EHE-006 (7) | organizer-dashboard-lists-past | Dashboard backend unchanged; existing dashboard tests green (unfiltered) | ✅ COMPLIANT |
| EHE-006 | organizer-consults-past | `Organizer_ManagementEvent_Expired_200` (EventControllerTests.cs:379) | ✅ COMPLIANT |
| EHE-006 | organizer-cannot-mutate-past | All 7 endpoint 409 tests (owner + Admin requester) | ✅ COMPLIANT |
| EHE-006 | organizer-metrics-include-past | `MetricsService.GetOrganizerMetricsAsync` unchanged; existing metrics tests green | ✅ COMPLIANT |
| EHE-006 | dashboard-lists-pending-rejected | Dashboard endpoint unchanged (no status filter); suite green | ✅ COMPLIANT |
| EHE-006 | opens-pending-detail | Pending past event + manage 200 with `Status` surfaced (EventControllerTests.cs:614; EventWithAvailability.Status) | ✅ COMPLIANT |
| EHE-006 | dashboard-hides-edit | `canEdit = user?.role === 'Admin'` (OrganizerDashboard.jsx:60,:250) — Edit hidden for organizers, kept for admin | ✅ COMPLIANT (manual) |

**Compliance summary**: 39/39 scenarios compliant; 14/14 requirements covered.

### Correctness (Static Evidence)

| Requirement | Status | Notes |
|------------|--------|-------|
| PEM-001 shared guard | ✅ Implemented | `EventFinalizedGuard.EnsureMutable` (Services/Guards/EventFinalizedGuard.cs:16) on materialized entity, `eventEntity.IsExpired(clock.GetUtcNow().UtcDateTime)` |
| PEM-002 7 endpoints → 409 | ✅ Implemented | 3 catches EventController.cs (:160,:205,:265); 4 catches AdminController.cs (:202,:235,:351,:398); `Problem(409, "event-finalized")` |
| PEM-003 guard before side-effects | ✅ Implemented | Load→ownership→guard→mutate in all 7 service methods (EventService.cs:493,624,351,398; AdminService.cs:114,135); controller audit lines unreachable on throw |
| PEM-004 flag-independent | ✅ Implemented | No `HideExpiredEvents` reference in guard; `_hideExpiredOptions` only gates read filters (EventService.cs:144,:181) |
| PEM-005 carve-outs | ✅ Implemented | GET `/events/{id}/manage` (EventController.cs:71-83, includeExpired, no guard); `AdminPurchaseService` untouched (no guard refs) |
| PEC-001..004 consultation | ✅ Implemented | `EventReadOnlyView.jsx` + `EventForm` readOnly + route RoleGuard both roles |
| EA-003/004 approve/reject | ✅ Implemented | Guard before status flip; audit only after service returns; 404/403 paths unchanged |
| ATS-002/004 stock/type | ✅ Implemented | Guard inside FOR UPDATE txn (EventService.cs:349-351); before insert (AddTicketType :398) |
| EHE-006 preserved access | ✅ Implemented | No expired/status filter added to organizer endpoints; Edit hidden for organizers (UI) |

### Coherence (Design)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| D-1 distinct exception | ✅ Yes | `EventFinalizedException` (Models/EventFinalizedException.cs:12) → 409 `event-finalized` |
| D-2 guard at service layer | ✅ Yes | All 7 service methods, incl. in-transaction stock guard (ADR-7) |
| D-3 static helper shape | ✅ Yes | `internal static class EventFinalizedGuard` — matches design exactly |
| D-4 hard rule, flag-independent | ✅ Yes | ADR-6 — no flag gating |
| D-5 read-only "Ver" view | ✅ Yes | EventReadOnlyView.jsx + route + useManagementEvent + EventForm readOnly |
| D-6 EventForm readOnly mode | ✅ Yes | Disabled inputs, hidden submit + file input, preview kept |
| D-7 badge + tooltip pattern | ✅ Yes | Finalizado badge; disabled mutations with `title="Evento finalizado — solo lectura"`; Compras/Metricas enabled; row not grayed |
| D-8 AdminService DI | ✅ Yes | `TimeProvider` ctor (AdminService.cs:18); Program.cs:39 `AddScoped` + :75 `TimeProvider.System` singleton, no Program.cs change |
| Design deviation | ➖ None | apply-progress confirms no deviations; WAF InMemory txn-warning ignore is test-infra only |

### TDD Compliance (Strict TDD)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | "TDD Cycle Evidence" table present in apply-progress.md |
| All tasks have tests | ✅ | 22/22 tasks; backend tasks RED/Written, frontend manual (no runner, config-sanctioned) |
| RED confirmed (tests exist) | ✅ | 6 test files verified on disk (EventFinalizedGuardTests, EventServiceImmutabilityTests, AdminServiceTests, EventControllerTests, AdminControllerTests, ErrorHandlingPropertyTests) |
| GREEN confirmed (tests pass) | ✅ | All new tests pass in full `dotnet test` run (not in fail list); focused evidence matches apply-progress (3/3, 11/11, 76/76) |
| Triangulation adequate | ✅ | Guard 3 cases; service 7 (5 methods × past + flag-disabled + future); admin 4; endpoints 7+4; handler 1 (mirrors :206) |
| Safety Net for modified files | ✅ | 155/155 baseline asserted in apply-progress; full suite green except documented pre-existing |

**TDD Compliance**: 6/6 checks passed

### Test Layer Distribution

| Layer | Tests (new) | Files | Tools |
|-------|-------------|-------|-------|
| Unit | 19 | 6 | xUnit + Moq + FsCheck (FakeTimeProvider, InMemory) |
| Integration (WAF) | 7 + 2 manage-200 | 1 | Microsoft.AspNetCore.Mvc.Testing |
| E2E | 0 | 0 | not configured |
| **Total new** | **26+** | **7** | |

### Changed File Coverage

Coverage analysis skipped — no coverage tool detected (`coverage_available: false`).

### Assertion Quality

| File | Line | Assertion | Issue | Severity |
|------|------|-----------|-------|----------|
| EventServiceImmutabilityTests.cs | 133 | `Verify(EnqueueAsync, Never)` | None — real behavior (no notification on reject) | — |
| EventControllerTests.cs | 500-506 | 409 + media type + type/title/status | None — full RFC 7807 shape; `instance` framework-generated (not asserted) | SUGGESTION |

**Assertion quality**: ✅ All assertions verify real behavior (status codes, ProblemDetails fields, DB persistence state, `Never` verifications). No tautologies, ghost loops, or smoke-only tests found.

### Quality Metrics

**Linter (dotnet format)**: ✅ 0 errors in files touched by this change (457 pre-existing WHITESPACE errors in untouched files, identical to baseline)
**Type Checker**: ➖ Not available (no TypeScript; JSX project)
**ESLint (frontend)**: ✅ 0 errors on the 5 changed files (`EventForm.jsx`, `EventReadOnlyView.jsx`, `App.jsx`, `AdminPanel.jsx`, `OrganizerDashboard.jsx`); `vite.config.js` pre-existing errors untouched
**Frontend build**: ✅ `npm run build` exit 0 (only chunk-size advisory)

### Issues Found

**CRITICAL**: None — zero regressions attributable to this change.

**WARNING**:
1. Backend suite exit code is 1: 5 documented pre-existing failures + `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` (timing flake, **proven failing at baseline 599642b**, file untouched by this change). Suite debt blocks a green CI until baseline fixes land. (Maps to: suite health, no requirement ID)
2. Frontend scenarios (PEC-001, PEC-004, EHE-006 dashboard-hides-edit) are verified by code inspection + build + non-regression of the existing Vitest suite only — no frontend test runner exists to assert the "Ver"/disabled-button behavior automatically (documented config constraint; design lists the runner as follow-up).

**SUGGESTION**:
1. `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` (Tests/EventNotificationQueueTests.cs:73) is a wall-clock assertion (`< 1000ms`) — inherently flaky; replace with a non-timing assertion.
2. RFC 7807 `instance` field is never directly asserted (PEM-002 rfc7807 scenario); add one assertion to the Update 409 test.
3. PEM-005 purchases/refunds carve-out has no dedicated new test seeding a past event (covered statically — no guard in `AdminPurchaseService` — plus existing suite); a focused regression test would lock the carve-out.
4. `QRCodePropertyTests.Property21` remains flaky (documented in apply-progress; passed in both runs here).
5. Add a frontend test runner (Vitest) so consultation UI gets automated coverage (design Open Question, ATS-009 precedent).

### Verdict

**FAIL (evidence-level)** — The `dotnet test` command exits non-zero (6 failed / 678 passed), so the strict envelope is a valid canonical `fail` (command-exit evidence). This is NOT a substantive failure of the change: every one of the 6 failures is pre-existing/environmental and proven at baseline commit 599642b (5 documented in apply-progress + the timing flake reproduced in an isolated baseline worktree run), and **zero failures are regressions from this change**. Implementation matches all 14 requirements / 39 scenarios: all 7 mutation endpoints guard past events before any save/audit/notification, carve-outs (GET manage, purchases, refunds) intact, rule is flag-independent, all 22 tasks complete, `blockers: 0`, `critical_findings: 0`. The remaining warnings are pre-existing suite debt (timing-flaky and live-DB-dependent tests) and manual-only frontend coverage. Per the change instructions, with all CRITICAL findings resolved the change is ready for archive; the suite debt is tracked in the findings for a separate baseline-cleanup workstream.