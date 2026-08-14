# Archive Report: admin-partial-refunds

**Change**: admin-partial-refunds
**Archived**: 2026-08-14
**Archive path**: `openspec/changes/archive/2026-08-14-admin-partial-refunds/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram)
**Capability**: `admin-purchase-refunds` (modified)
**Main spec**: `openspec/specs/admin-purchase-refunds/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. 14/14 implementation tasks complete (task 5.2 — migration apply — **EXECUTED 2026-08-14**, owner-approved). All 8 changed/added requirements (APR-002/003/010/011 modified; APR-012/013/014/015 added) and all 18 delta scenarios COMPLIANT with green covering tests. Zero new test failures in either suite; all full-suite failures are documented pre-existing baselines in untouched files.

**Task 5.2 EXECUTED (owner-approved, 2026-08-14) — orchestrator final-state fact, verbatim**: migration `20260814134333_AddRefunds` applied to Supabase dev via `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext`. History 15/15 → **16/16, 0 pending**. `Refunds` table + legacy backfill live on dev. tasks.md now 14/14 checkboxes complete.

**Verify outcome — orchestrator final-state fact, verbatim**: 8/8 requirements, 18/18 scenarios COMPLIANT; 39 change-scope tests green (19 service + 13 controller + 2 structural backend, 7 frontend). Full suites: backend 640 pass/5 fail, frontend 438 pass/3 fail — ALL documented pre-existing baselines in untouched files, ZERO new regressions. Machine gate exit 1 only from baseline debt.

**Final test evidence (at close, corroborated by persisted verify-report #490, written 2026-08-14 14:33):**
- Backend full suite: **640 passed / 5 pre-existing failures** / 645 total (CSRF webhook, S3 upload, MP webhook signature ×2, email retry — all in files untouched by this change).
- Frontend full suite: **438 passed / 3 pre-existing failures** / 441 total (Checkout ×2 DNI/PATCH, identityValidation DNI letters — untouched files).
- Change-scope focused: backend **32/32** (AdminPurchaseServiceTests 19, AdminControllerPurchaseTests 11, AddRefundsTable_BackfillContainsPureSqlInsertSelect 2) + frontend AdminPurchases.test.jsx **7/7** = **39/39 change-scope tests green**.
- `dotnet build`: 0 errors, exit 0.

**Breakdown discrepancy (recorded, not silently resolved)**: the orchestrator launch prompt's parenthetical breakdown says "19 service + 13 controller + 2 structural backend, 7 frontend" (which sums to 41, not 39). The persisted verify-report (#490, 2026-08-14 14:33) and its on-disk focused run state **11 controller tests** (19+11+2+7 = 39, matching the same launch prompt's stated total of 39), and explicitly corrects the 13/13 claim from apply-progress as a documentation nit ("the file actually contains 11 tests"). The final total **39** is corroborated by both sources; the "13 controller" sub-count is recorded here as inherited from the apply-progress misstatement and does not match the verified on-disk test count (11).

**Verify verdict note (historical):** the persisted `verify-report` snapshot carries machine verdict `fail` with `blockers: 1` — a strict-TDD command-exit gate triggered by the pre-existing baseline-red full suite (640/5 backend, 438/3 frontend), NOT a change defect. `critical_findings: 0`, and the report itself states: "From the change's own content: PASS — 8/8 requirements, 18/18 scenarios COMPLIANT… Archive can proceed once the orchestrator acknowledges the pre-existing baseline-red suite and completes the owner rollout step 5.2." Per the Final-State Authority hierarchy, the snapshot's stale claims ("5.2 PENDING-OWNER", "persistable but not archive-ready") are superseded by the orchestrator's final-state facts (5.2 EXECUTED) and the archive decision; the archive proceeded under ordinary repository policy.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | `reviewGate` absent. Review consent was **DECLINED for this candidate** (user choice, `declined_this_candidate`); no `sdd/admin-partial-refunds/review/*` topics exist in Engram, no receipt/lineage created. Dispatcher `blockedReasons` and owner authorization documented in the Review Blockers section below. |
| Task Completion | Pass | Archived `tasks.md`: 14/14 `[x]`, 0 unchecked implementation tasks. Task 5.2 was marked complete after verify (owner execution), consistent with the launch-prompt final-state fact; no archive-time stale-checkbox reconciliation was needed. |
| CRITICAL verification issues | None | `critical_findings: 0` in verify-report (#490); the single blocker is machine-gate baseline debt, not change scope. No CRITICAL override accepted or needed. |
| Action Context | Pass | No `actionContext.mode: workspace-planning`; no `allowedEditRoots` restriction. Operations stayed inside the repo. |

## Review Blockers (documented dispatcher reason + owner authorization)

Per orchestrator final-state fact #3, verbatim:

> **Review consent DECLINED for this candidate** (user choice, `declined_this_candidate`, no review lineage created). Native SDD dispatcher blockedReasons: "verify evidence cannot enter remediation: test_exit_code must be zero for archive readiness; bounded review transaction is missing". **The owner explicitly authorized archive under orchestrator decision** (explicit aval: "Segui con el proceso sin importar la review, dejala documentada").

This is an owner-acknowledged, intentional-with-warnings archive: the decline of the optional post-verify review offer and the dispatcher's `blockedReason` (non-zero `test_exit_code` from baseline debt; no bounded review transaction because none was ever started) are recorded here as the audit trail. No review artifact exists for this candidate, so `reviewGate` is structurally absent and no gate value blocks archive; the owner's explicit authorization resolves the dispatcher note.

## Spec Sync

The delta spec (`specs/admin-purchase-refunds/spec.md`) modifies the existing canonical spec `openspec/specs/admin-purchase-refunds/spec.md` with `ADDED` (APR-012/013/014/015) and `MODIFIED` (APR-002/003/010/011) sections; no `REMOVED`/`RENAMED` sections. Merge applied to the canonical spec:

| Requirement | Action | Detail |
|-------------|--------|--------|
| APR-002 | Modified | Row exposes RefundedQuantity/RefundedAmount; `Refunded` derived; `totalRefunded` = Σ `Refunds.Amount`; scenarios updated (partial + full listing). |
| APR-003 | Modified | Quantity-based atomic refund: body `{ quantity }`, K>0 validation, block K>active / no Approved / any used; K oldest selected; flip to Refunded ONLY at 0 active; 6 scenarios. |
| APR-010 | Modified | "X de Y reembolsadas" badge (error/warning), quantity selector + live preview, disabled when fully refunded, mutation posts `{ quantity }`; 4 scenarios. |
| APR-011 | Modified | Replaces binary-refund tests, adds partial/cumulative/backfill/validation tests; frontend mock shape + badge/selector coverage. |
| APR-012 | Added | Cumulative refund operation record (`Refunds` row per op; TotalRefunded = Σ; RefundedQuantity/RefundedAmount; derived Refunded). |
| APR-013 | Added | Deterministic ticket selection — K oldest non-refunded/non-used under lock. |
| APR-014 | Added | Legacy refund backfill — pure-SQL `INSERT…SELECT` in `AddRefundsTable`, AdminId NULL, no TotalRefunded regression. |
| APR-015 | Added | Non-goals as negative requirements (no MP, no motivo, no per-ticket UI, no Reservation change, no auto-refund edits, no edit/revert). |
| APR-001, APR-004..009 | Preserved | Unchanged requirement blocks copied through verbatim (not mentioned in delta). |

**Merge result**: canonical spec now contains **15 requirements / 28 scenarios** (was 11/17 at archive of `admin-event-refunds`; +4 requirements, +11 scenarios net).

**Merge decision (documented)**: the delta's `(Previously: ...)` parenthetical notes inside MODIFIED blocks are diff bookkeeping for the change reviewer, not current-state spec content; they were NOT carried into the canonical source of truth so the main spec reads as the authoritative current behavior (consistent with the existing canonical style). The delta spec in the archive retains them verbatim.

The `rules.archive` config (`Warn before merging destructive deltas`) was applied — this merge is non-destructive (no REMOVED requirements; all MODIFIED blocks replaced full requirement text including preserved scenarios; large unchanged sections preserved). No warning condition triggered.

## Archive Move

`openspec/changes/admin-partial-refunds/` → `openspec/changes/archive/2026-08-14-admin-partial-refunds/` via `git mv` (tracked files staged as renames; untracked `verify-report.md` moved with the directory). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `explore.md`, `proposal.md`, `spec.md`, `specs/admin-purchase-refunds/spec.md`, `design.md`, `tasks.md` (14/14), `apply-progress.md`, `verify-report.md` + this report. Active `openspec/changes/` no longer contains the change. The change folder was preserved (moved, not deleted) per the repo convention; no `state.yaml` exists in this repo's change folders, so the archive move is the status closure.

## Decisions & Deviations (final, from verify-report / apply-progress)

1. D1–D10 design decisions all followed (verify-report Coherence table: 10/10 ✅). New immutable `Refunds` ledger; flip Approved→Refunded only at 0 active; K oldest `OrderBy(CreatedAt).Take(K)` under row lock; pure-SQL backfill (no EF-context, no try/catch — memory #442); `TicketIds` as `Guid[]` + `uuid[]` PG-only; `Restrict` delete; `Amount = TicketType.Price × K`; plain record DTO (no annotations, service throws → 409); body `{ quantity }`; no ripple consumer changes (PaymentService/MetricsService/EventService/ReservationService/TicketService 0 commits in apply range).
2. Migration `20260814134333_AddRefunds` (create table + index + FK Restrict + pure-SQL backfill `WHERE "Status" = 3`; Designer file mandatory and present).
3. Test-helper path walk-up + Designer glob `*_AddRefunds*.cs` resolved in-test (apply-progress §Issues), verified on disk (structural test 2/2 green).
4. **Documentation nit (verify-report WARNING 2)**: apply-progress §Test results misstates `AdminControllerPurchaseTests 13/13`; the file contains 11 tests (combined 32/32 focused count correct). The 13/13 figure was also inherited into the orchestrator launch prompt's parenthetical breakdown; final count recorded as 11 (see Final State discrepancy note).
5. **APR-015 / D10 invariants confirmed**: `PaymentService.InitiateRefundAsync` untouched (0 commits); no MP call, no motivo, no Reservation status change, no refund-edit/revert endpoint; audit after commit (APR-007) with no motivo (APR-008).

## Delivery (still pending owner — orchestrator final-state fact #4, verbatim)

> **Delivery still pending owner**: 4 commits local on dev (98a3332, bcbfc58, 9163d3d, 5f31a92) NOT pushed. Single PR with size:exception (≤4000) approved; rollout = push + PR.

Commits: `98a3332` (feat(backend): tabla Refunds con backfill por SQL puro), `bcbfc58` (feat(backend): reembolso parcial por cantidad con ledger Refunds y body {quantity}), `9163d3d` (feat(frontend): selector de cantidad y badge X de Y reembolsadas), `5f31a92` (docs(openspec): apply completado 13/14 → 5.2 rollout del owner). No pushes performed — owner decides delivery.

## Rollback (post-archive, if ever needed — orchestrator final-state fact #5, verbatim)

> `dotnet ef migrations remove` pre-deploy OR drop `Refunds` table post-deploy; revert service/DTO/row/UI to binary; `IsRefunded` flags + audit rows kept (no data loss).

Pre-deploy: `dotnet ef migrations remove` (removes `20260814134333_AddRefunds`; history back to 15/15). Post-deploy: `DROP TABLE "Refunds";` — additive schema, no FK consumer blocks revert; backfill rows restorable by re-running the migration's INSERT…SELECT. Code: revert service/DTO/row/UI to binary refund (`RefundPurchaseAsync(reservationId, adminId)`, 9-arg `AdminPurchaseRow`, no `RefundPurchaseRequest`, binary badge/button). Data: `IsRefunded` flags on Tickets + audit rows KEPT (no data loss).

## Engram Traceability

Observations read for this archive (Engram, project `ticketstart`):

| ID | Artifact | Read |
|----|----------|------|
| #484 | `sdd/admin-partial-refunds/explore` | filesystem copy (explore.md) + search preview |
| #485 | `sdd/admin-partial-refunds/proposal` | filesystem copy (proposal.md) + search preview |
| #486 | `sdd/admin-partial-refunds/spec` | filesystem copy (spec.md + specs/admin-purchase-refunds/spec.md) + search preview |
| #487 | `sdd/admin-partial-refunds/design` | filesystem copy (design.md) + search preview |
| #488 | `sdd/admin-partial-refunds/tasks` | filesystem copy (tasks.md, full) + search preview |
| #489 | `sdd/admin-partial-refunds/apply-progress` (validation) | search preview |
| #490 | `sdd/admin-partial-refunds/verify-report` | filesystem copy (verify-report.md, full) + search preview |

(No review topics exist for this candidate — no `sdd/admin-partial-refunds/review/*` observations were read or created; `reviewGate` structurally absent.)

## Intentional-with-warnings flags

1. **Machine-gate baseline debt** (verify-report verdict `fail`, `blockers: 1`): full-suite exit 1 from 5 backend + 3 frontend pre-existing baseline failures in untouched files. Zero change-scope failures. Archive proceeded per Final-State Authority — the orchestrator's final-state facts and archive decision supersede the snapshot's "not archive-ready" wording; no CRITICAL findings exist to override.
2. **Review declined + dispatcher blockedReason**: documented verbatim in the Review Blockers section with the owner's explicit aval.
3. **Snapshot sub-count inconsistency** (13 vs 11 controller tests): recorded in Final State; final count 11 per verified on-disk evidence.
