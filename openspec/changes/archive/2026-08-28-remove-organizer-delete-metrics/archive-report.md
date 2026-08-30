# Archive Report: remove-organizer-delete-metrics

**Change**: remove-organizer-delete-metrics — Organizer Loses Event Deletion (Admin-Only 403 Guard) + Per-Event Metrics Page Removal (UI-Only)
**Archived**: 2026-08-28 (orchestrator-specified archive date, matching the change-closure commit date; archive housekeeping executed 2026-08-30)
**Archive path**: `openspec/changes/archive/2026-08-28-remove-organizer-delete-metrics/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram persistence)
**Capabilities**: `event-deletion` (NEW canonical domain), `role-access`, `past-event-consultation` (2 MODIFIED canonical domains), `past-event-mutation-guard` (archive-note one-line clarification)
**Main specs**: `openspec/specs/{event-deletion,role-access,past-event-consultation,past-event-mutation-guard}/spec.md`

## Final State

**Cycle outcome: COMPLETE (archived without a formal verify-report — see below).** The change was fully planned and implemented; all **22/22** implementation tasks (1.1–6.3) are checked in the persisted `tasks.md` (0 unchecked). The formal `sdd-verify` phase was **skipped by owner decision** — no `verify-report.md` exists in this change (unlike the 2026-08-21 archive). Verification status at close: **owner verified the delivered behavior personally** (orchestrator launch-prompt final-state fact, rank 3 in the Final-State Authority hierarchy).

**Delivery commits on branch `feat/frontend-brand-polish` (single-pr strategy, 5 work-unit commits, backend-first, docs last):** `4f99efd` (feat(api)!: Admin-only delete guard, ED-001), `63b1bc8` (test(api): WAF coverage of the per-event metrics endpoint), `acb9ab9` (feat(frontend): organizer delete-flow removal + kebab narrowing), `e9b3f20` (feat(frontend): metrics page + route deletion), `49f4353` (docs(openspec): SDD artifacts). All five are dated 2026-08-28. Each commit independently revertible; PR revert = full rollback.

**Apply-time verification evidence (attributed to `apply-progress.md`, intermediate snapshot — valid at its writing time):** backend `dotnet test` 684 passed / 5 failed / 689 total, with all 5 failures proven pre-existing at baseline `88c8fdb` via stash run (webhook/CSRF, S3 upload params, email-retry flake); the 5 touched suites 127/127; metrics suites 27/27; frontend OrganizerDashboard+AdminPanel 68/68; full Vitest 452 passed / 3 failed / 455 total with the 3 failures being the orchestrator-excluded pre-existing debt (`Checkout.test.jsx` ×2, `identityValidation.test.js` ×1).

**What the change delivered:**
- **Organizer loses event deletion — Admin-only 403 guard, any status** (ED-001): service-level Admin-only guard in `EventService.DeleteEventAsync`, running BEFORE the finalized-event guard, so an organizer gets 403 (never 409) whether the event is `Pending`, `Approved`, or past. No side effects on rejection. Admin keeps delete unchanged (ED-002: active → 204, past → 409 `event-finalized`). AdminPanel flow + shared `DeleteConfirmationDialog` untouched (ED-003).
- **Organizer per-event metrics page + route removed — UI-only**: `OrganizerEventMetrics.jsx` + test deleted, `/organizer/events/:id/metrics` route unregistered (falls through to NotFound); backend `GET /metrics/events/{id}`, `MetricsController`, `GetEventMetricsAsync`, `CalculateMetricsAsync` and metrics tests intentionally kept (explicit product non-goal to retire them; WAF keep-alive tests added at 63b1bc8). Dashboard kebab narrows: organizer rows keep only "Ver"; admin kebab = Editar only; shared load/retry error-feedback path preserved.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | No `reviewGate` in the orchestrator's structured status and no review artifacts exist for this candidate (no receipt/ledger/transaction files or Engram topics). Archive proceeds under ordinary repository policy. |
| Task Completion | Pass | Persisted `tasks.md`: **22/22 `[x]`, 0 unchecked** (verified via grep before the move: 22 checked, 0 unchecked). Note: `apply-progress.md` says "all 21 tasks" — a snapshot miscount (its own per-task evidence lists all 22 rows); the tasks artifact governs. |
| CRITICAL verification issues | None recorded | No verify-report exists (formal verify skipped by owner decision, recorded above). No CRITICAL findings were reported by any artifact; verification is owner-verified personally per the orchestrator's final-state facts. |
| Action Context | Pass | No `actionContext.mode: workspace-planning`; all operations inside workspace root `/home/martin/proyectos/Ticketstart`. |

## Spec Sync

### New canonical domain (mechanical copy — never model Read→Write)

No `openspec/specs/event-deletion/` existed before this archive. The delta IS a full spec (canonical format already: `## Purpose`, `## Requirements`, `## Coverage Matrix`). Copied **mechanically** with the shell (`cp` to temp → `diff -r` readback → `mv`):

```
=== VERBATIM diff -r OUTPUT (event-deletion copy readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Result: `openspec/specs/event-deletion/spec.md` — 3 requirements (ED-001..003), 7 scenarios, coverage matrix.

### MODIFIED canonical domains (deltas merged into existing main specs)

| Domain | Main spec before | Merge action | Result |
|--------|------------------|--------------|--------|
| role-access | `openspec/specs/role-access/spec.md` (EHE-006/007/008) | Replaced the EHE-006 block with the delta's full MODIFIED block; updated the EHE-006 coverage-matrix row | EHE-007/EHE-008 byte-preserved; EHE-006 now 12 scenarios (6 preserved byte-identical, 1 updated — organizer-cannot-mutate-past now splits 409 non-delete vs DELETE 403, 5 added — dashboard-hides-eliminar-metricas, metrics-route-unresolved, per-event-metrics-owner-200, per-event-metrics-admin-200, load-error-feedback-survives). Delta `(Previously: ...)` note carried into the canonical spec |
| past-event-consultation | `openspec/specs/past-event-consultation/spec.md` (PEC-001..004) | Replaced the PEC-004 block with the delta's full MODIFIED block; updated the PEC-004 matrix row | PEC-001/002/003 byte-preserved; PEC-004: compras-enabled scenario byte-identical, "Metricas stays enabled on past row" replaced by "Metricas entry no longer present on past row" (matrix: metricas-enabled → metricas-entry-absent). Delta `(Previously: ...)` note carried |
| past-event-mutation-guard | `openspec/specs/past-event-mutation-guard/spec.md` (PEM-001..005) | Applied the one-line clarification prescribed by the change's `archive-note.md` at exactly the two prescribed spots: PEM-002 requirement body (after the seven-endpoint list, L35 area) and the PEM-002 scenario GIVEN line "valid requester (owner or Admin)" (L39) | PEM-001/003/004/005 and both PEM-002 scenarios otherwise byte-preserved; clarification records: DELETE valid-requester set narrowed to Admin-only per `event-deletion` ED-001 — organizer deleting any event (past included) receives 403 from the Admin-only service guard in `EventService.DeleteEventAsync` BEFORE the finalized guard, never 409; Admin + past event keeps the 409 `event-finalized` contract (ED-002). Non-delete mutations (PUT, image upload, stock/type, approve/reject) unaffected — still 409 for organizers on past events |

Merge normalization (recorded, intentional): delta `## MODIFIED Requirements` framing resolves to the canonical `## Requirements` section; requirements not mentioned in the deltas are byte-preserved. Per the current convention (established by the 2026-08-21 archive, superseding the older drop-the-parenthetical practice), the deltas' `(Previously: ...)` change-history notes ARE carried into the canonical specs. Canonical spec titles were left untouched (delta titles describe the change's focus, not the corpus heading).

### Archive-note vs deltas — consistency check

No discrepancies. The `archive-note.md` claims all match the delta content: ED-001 (organizer 403 before the finalized guard, any status), ED-002 (Admin + past event keeps 409), and `role-access` EHE-006's "Organizer cannot mutate a past event" scenario delete half (403 instead of 409 for DELETE). The note's line references are accurate against the canonical file (L35 = PEM-002 endpoint list, L39 = "valid requester (owner or Admin)").

## Archive Move

`openspec/changes/remove-organizer-delete-metrics/` → `openspec/changes/archive/2026-08-28-remove-organizer-delete-metrics/` via **`git mv`** (all 9 files tracked). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (it did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `design.md`, `exploration.md`, `tasks.md` (22/22 `[x]`, 0 unchecked), `apply-progress.md`, `archive-note.md`, `specs/{event-deletion,past-event-consultation,role-access}/spec.md` + this report. **No `verify-report.md`** — the formal verify phase was skipped by owner decision (recorded in Final State). Active `openspec/changes/` no longer contains the change. No `state.yaml` exists in this repo's change folders (consistent with prior archives).

## Decisions & Deviations (final)

1. **No formal verify-report — intentional, owner decision.** The orchestrator's launch prompt states the owner verified the delivered behavior personally and decided to skip the formal `sdd-verify` phase. Recorded per the Final-State Authority (launch-prompt fact, rank 3); apply-progress's exact test numbers are attributed to their snapshot, not presented as the close-of-cycle verification. Deviates from the 2026-08-21 archive (which had a persisted verify-report); intentional, not an inconsistency.
2. **Task count 22/22** — the persisted tasks artifact governs; "all 21 tasks" in apply-progress is a snapshot miscount (its own per-task evidence lists all 22 rows). Same class of discrepancy as the 2026-08-21 archive's "17 tasks" note.
3. **`(Previously: ...)` notes preserved in canonical specs** — current convention (explicitly established by the 2026-08-21 archive as superseding the older drop-them practice).
4. **PEM-002 kept its "all seven endpoints" 409 wording with the clarification appended** — the archive-note prescribes a one-line clarification, not a rewrite; minimal integration preserves the delta/corpus wording and semantics (the clarification sentence scopes DELETE's narrowed valid-requester set inline).
5. **Archive date 2026-08-28** — orchestrator-specified target directory; matches the date of all 5 change-closure commits. Housekeeping was executed 2026-08-30; recorded for audit precision.
6. **No migration/rollback exposure** — capability removal + UI removal; rollback is `git revert` of the 5 commits (or the PR as a whole). No DB or config changes.

## Engram Traceability

This archive report persisted as Engram topic `sdd/remove-organizer-delete-metrics/archive-report` (hybrid mode per `openspec/config.yaml` `artifact_store: hybrid`), including the discrepancies above (tasks 21-vs-22 miscount; verify phase skipped by owner decision).
