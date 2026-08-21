# Archive Report: past-events-readonly

**Change**: past-events-readonly — Past Events Read-Only (Event Immutability)
**Archived**: 2026-08-21
**Archive path**: `openspec/changes/archive/2026-08-21-past-events-readonly/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram persistence)
**Capabilities**: `past-event-mutation-guard`, `past-event-consultation` (2 NEW canonical domains), `role-access`, `event-approval`, `admin-ticket-stock` (3 MODIFIED canonical domains)
**Main specs**: `openspec/specs/{past-event-mutation-guard,past-event-consultation,role-access,event-approval,admin-ticket-stock}/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. All 22/22 implementation tasks (1.1–5.4) complete. Verify: **0 CRITICAL, 0 blockers, 0 regressions**; **14/14 requirements and 39/39 scenarios compliant** (`gentle-ai.verify-result/v1` envelope, evidence_revision `sha256:3873045e73cffd78cac426787f92da4c90553f29911c8ac5067f6b021b6919c4`, persisted verify-report obs #568).

**Final test evidence (at close)** — orchestrator final-state facts (launch prompt, most recent account) + persisted verify-report obs #568, mutually corroborating:
- Backend suite `dotnet test` exit 1 is **entirely PRE-EXISTING debt**: 6 failures (5 documented in apply-progress + `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` timing flake), **all proven at baseline commit `599642b`** (the timing flake reproduced in an isolated baseline worktree run). Zero failures are regressions from this change.
- Builds: `dotnet build` exit 0, 0 warnings/errors; `dotnet format` 0 errors on the 15 touched files (457 pre-existing WHITESPACE errors in untouched files, identical to baseline).
- Frontend: `npm run build` exit 0 (only chunk-size advisory); ESLint 0 errors on the 5 changed files; full Vitest 445 passed / 3 failed (all 3 pre-existing at baseline).
- Delivery commits on branch `fix/admin-past-events-edit` (single-pr strategy, 4 work-unit commits + progress commit): `cb8c5cd` (guard foundation), `bfb119f` (service guards), `d027432` (controllers + handler → 409), `9b6ae7b` (frontend read-only Ver view), `4f7019b` (progress). **NOT merged, no PR — orchestrator handles delivery separately.**

**Note on task-count discrepancy**: the persisted filesystem tasks.md (the Task Completion Gate's named artifact) contains **22** checked tasks; verify-report agrees (22/22). apply-progress.md (intermediate snapshot) and the launch prompt say "17 tasks" — a miscount in the snapshot (its own TDD table lists all 22 rows). The higher-ranked tasks artifact + verify-report govern: **22/22**.

**Known debt (out of scope, tracked separately)**: 9 pre-existing baseline failures (5 backend + 3 frontend + 1 flaky `EventNotificationQueue` timing test), proven at baseline commit `599642b`, ajenos al cambio. Verify SUGGESTIONs carried as follow-ups: replace the wall-clock timing assertion in `EventNotificationQueueTests`, assert the RFC 7807 `instance` field in the Update 409 test, add a dedicated past-event purchases/refunds regression test, and add a frontend test runner (Vitest) for the consultation UI.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | `reviewGate` key structurally absent in the orchestrator's structured status — no review was ever discovered for this candidate (no `review/` dir, no receipt/ledger/transaction files or Engram topics). `reviewOffer` (if any) is an invitation, not a gate — declining proceeds to archive under ordinary repository policy. |
| Task Completion | Pass | Persisted tasks artifact (`openspec/changes/{change-name}/tasks.md`, the hybrid artifact named by the gate): **22/22 `[x]`, 0 unchecked** (verified via grep before move and re-verified on the archived copy). Engram obs #565 is a condensed paraphrase without checkboxes (no checkbox conflict); filesystem artifact is authoritative. |
| CRITICAL verification issues | None | `critical_findings: 0`, `blockers: 0` in verify-report (obs #568). Verify verdict is `fail` at evidence level ONLY because `test_exit_code: 1` from pre-existing baseline failures; the report itself concludes the change is ready for archive with 0 CRITICAL, and the orchestrator's final-state facts confirm. |
| Action Context | Pass | No `actionContext.mode: workspace-planning` in status; all operations inside workspace root `/home/martin/proyectos/Ticketstart`. |

## Spec Sync

### New canonical domains (mechanical copy — never model Read→Write)

No `openspec/specs/past-event-mutation-guard/` or `openspec/specs/past-event-consultation/` existed before this archive. Per OpenSpec convention each delta IS the full spec for the new capability; both were copied **mechanically** with the shell (`cp` to temp → `diff -r` readback → `mv`), verified by mandatory readback:

```
=== VERBATIM diff -r OUTPUT (past-event-mutation-guard copy readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
=== VERBATIM diff -r OUTPUT (past-event-consultation copy readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Results: `openspec/specs/past-event-mutation-guard/spec.md` — 5 requirements (PEM-001..005), 10 scenarios, coverage matrix. `openspec/specs/past-event-consultation/spec.md` — 4 requirements (PEC-001..004), 7 scenarios, coverage matrix. No header normalization needed (deltas already use canonical `## Requirements` framing).

### MODIFIED canonical domains (deltas merged into existing main specs)

| Domain | Main spec before | Merge action | Result |
|--------|------------------|--------------|--------|
| role-access | `openspec/specs/role-access/spec.md` (EHE-006/007/008) | Replaced EHE-006 block with the delta's full MODIFIED block (all scenarios); updated Purpose (view/edit → VIEW; mutation revoked) + coverage matrix | EHE-007/EHE-008 byte-preserved; EHE-006 now 7 scenarios (added organizer-consults-past, organizer-cannot-mutate-past; removed organizer-edits-past-event) |
| event-approval | `openspec/specs/event-approval/spec.md` (EA-001..010) | Replaced EA-003 and EA-004 blocks with the delta's full MODIFIED blocks (all scenarios); updated Purpose + coverage matrix | EA-001/002/005..010 byte-preserved; EA-003 and EA-004 each +1 scenario (approve-past-rejected, reject-past-rejected) with past-event guard text |
| admin-ticket-stock | `openspec/specs/admin-ticket-stock/spec.md` (ATS-001..009) | Replaced ATS-002 and ATS-004 blocks with the delta's full MODIFIED blocks (all scenarios); updated Purpose | ATS-001/003/005..009 byte-preserved; ATS-002 +1 scenario (increment-past-rejected); ATS-004 +1 scenario (new-type-past-rejected). No coverage matrix exists in this canonical spec (predates the convention); none added — minimal structural change |

Merge normalization (recorded, intentional): delta `## MODIFIED Requirements` framing resolves to the canonical `## Requirements` section (requirements not mentioned in the deltas are byte-preserved). **Deviating from the prior `event-approval`/`hide-expired-events` archive convention**: those archives dropped the delta `(Previously: ...)` change-history parentheticals from the canonical specs; the orchestrator's launch prompt explicitly instructed "preserving the `(Previously: …)` notes", so all 5 parentheticals (EHE-006, EA-003, EA-004, ATS-002, ATS-004) ARE carried into the canonical specs. Recorded here for audit — this is the current instruction and supersedes the older convention.

**Noted latent inconsistency (preserved, not resolved)**: canonical EA-005 scenario "No transition is blocked by workflow" (pre-existing, untouched by this change) states any approve/reject succeeds unless the event does not exist. With the new EA-003/004 past-event guard, approve/reject on a *past* event now fails with 409. EA-005 was not in this change's delta, so it is byte-preserved per the merge rule; the inconsistency is recorded here, not silently resolved. The verify compliance matrix covers only this change's 14 requirements (EA-005 not among them).

## Archive Move

`openspec/changes/past-events-readonly/` → `openspec/changes/archive/2026-08-21-past-events-readonly/` via **`git mv`** (mixed tracked/untracked set, moved successfully). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (it did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `design.md`, `exploration.md`, `tasks.md` (22/22 `[x]`, 0 unchecked), `apply-progress.md`, `verify-report.md`, `specs/{past-event-mutation-guard,past-event-consultation,role-access,event-approval,admin-ticket-stock}/spec.md` + this report. Active `openspec/changes/` no longer contains the change. No `state.yaml` exists in this repo's change folders (consistent with prior archives — the archive move is the status closure; config.yaml has no per-change archive-status tracking).

## Decisions & Deviations (final)

1. **`(Previously: ...)` notes preserved in canonical specs** — explicit orchestrator instruction; deviates from the event-approval/hide-expired-events convention of dropping them. Recorded above.
2. **Verify `fail` verdict does not block archive** — evidence-level failure only (`test_exit_code: 1`); `critical_findings: 0`, `blockers: 0`; all 6 failures proven pre-existing at baseline `599642b`; verify-report itself states the change is ready for archive with 0 CRITICAL. Suite debt is a separate baseline-cleanup workstream.
3. **Task count 22/22** — tasks artifact (rank 2) + verify-report agree; "17 tasks" in apply-progress/launch prompt is a snapshot miscount (its own table lists 22 rows).
4. **No migration/rollback exposure** — purely additive change (exception type, guard helper, service guard calls, controller catches, handler case, DI ctor param, frontend page + prop + UI edits). Rollback is `git revert <sha>` per design; no DB cleanup, no flag toggle (rule is HARD, flag-independent per ADR-6).
5. **No review artifacts exist for this candidate** — no receipt/ledger/transaction topics or files; `reviewGate` structurally absent.
6. **Delivery pending (orchestrator)**: single PR of the 4 work-unit commits on `fix/admin-past-events-edit` (cb8c5cd, bfb119f, d027432, 9b6ae7b; progress commit 4f7019b). Review budget: ~800–1000 changed lines vs 2000 budget, risk Low (per tasks.md forecast).

## Engram Traceability

Observations read for this archive (Engram, project `ticketstart`):

| ID | Artifact | Read |
|----|----------|------|
| #562 | `sdd/past-events-readonly/proposal` | search preview + filesystem copy (proposal.md, full) |
| #563 | `sdd/past-events-readonly/design` | search preview + filesystem copy (design.md, full) |
| #564 | `sdd/past-events-readonly/spec` | search preview + filesystem copies (5 delta spec.md, full) |
| #565 | `sdd/past-events-readonly/tasks` | mem_get_observation full content — condensed paraphrase, no checkboxes; filesystem tasks.md (22/22 `[x]`) is authoritative |
| #566 | `sdd/past-events-readonly/apply-progress` | search preview (intermediate snapshot — final-state facts supersede) |
| #568 | `sdd/past-events-readonly/verify-report` | search preview (envelope matches filesystem verify-report.md, full read) |

This archive report persisted as Engram topic `sdd/past-events-readonly/archive-report`.