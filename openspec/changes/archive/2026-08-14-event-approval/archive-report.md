# Archive Report: event-approval

**Change**: event-approval
**Archived**: 2026-08-14
**Archive path**: `openspec/changes/archive/2026-08-14-event-approval/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram persistence)
**Capabilities**: `event-approval` (1 new canonical domain), `catalog-filtering`, `role-access` (2 MODIFIED canonical domains)
**Main specs**: `openspec/specs/{event-approval,catalog-filtering,role-access}/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. All 22/22 implementation tasks (EA-T1..EA-T22) complete. All 13 requirements (EA-001..010 + EHE-002/003/006) and 41/41 scenarios verified. Verify verdict `pass_with_warnings` (strict `gentle-ai.verify-result/v1` envelope, evidence_revision `sha256:438f7bba7e3ebf29f1af1dd2b243b1e57ff84de1327ae87b4caad21747a58ec8`). 0 CRITICAL, 0 blockers, 0 WARNING findings, 3 SUGGESTION (S1–S3).

**Final test evidence (at close)** — orchestrator final-state facts (launch prompt, most recent account) + persisted verify-report obs #479, mutually corroborating:
- Backend clean evidence run: **629/629 passed, exit 0** (excludes 5 documented pre-existing baseline failures + 1 flaky `EventNotificationQueue` timing test).
- Frontend clean evidence run: **378/378 passed, exit 0** (excludes 2 pre-existing `Checkout.test.jsx` DNI failures + 1 pre-existing `identityValidation.test.js` failure).
- Full suites show only pre-existing baseline failures (5 backend + 3 frontend + 1 flaky), verified via git that no failing file was touched by this change — zero regressions introduced.
- Builds: backend `dotnet build --nologo` exit 0; frontend `npx vite build` exit 0.
- Delivery: **9 commits on `main` local (`7dddcb8..4866719`, HEAD `4866719`), NOT pushed, no PR** — orchestrator handles delivery.

**Operational pending (non-blocking of archive, MUST be done before deploy)**: apply migration `AddEventApproval` to a database — `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --context TicketeraOnline.Api.Data.ApplicationDbContext` from `backend/` (verify INFO I1). Code is correct and rollback-safe without the applied DB; runtime will fail without the `Status` column.

**Known debt (out of scope, tracked separately)**: 9 pre-existing baseline failures (5 backend + 3 frontend + 1 flaky `EventNotificationQueue`), ajenos al cambio.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | `reviewGate` key structurally absent in native `gentle-ai sdd-status` output — no review was ever discovered for this candidate (no `reviews/` files, no `sdd/event-approval/review/*` Engram topics). `reviewOffer` (if any) is an invitation, not a gate — declining proceeds to archive under ordinary repository policy. |
| Task Completion | Pass | Filesystem tasks.md (the hybrid tasks artifact named by the gate): 22/22 `[x]`, 0 unchecked implementation tasks; native status `taskProgress.allComplete: true`. NOTE: Engram obs #477 (topic `sdd/event-approval/tasks`) is the **pre-apply snapshot** (all `- [ ]`); apply updated the filesystem tasks.md only (commit `58faa14`), never upserted the Engram topic. The filesystem artifact + native dispatcher agreement are authoritative; the stale Engram snapshot is recorded for traceability, not restated as current state. |
| CRITICAL verification issues | None | `critical_findings: 0`, `blockers: 0` in verify-report obs #479. |
| Action Context | Pass | `actionContext.mode: repo-local`; `allowedEditRoots: [/home/martin/proyectos/Ticketstart]`; all operations inside workspace root. |
| Dispatcher `dependencies.archive` | Explained, not blocking | Native status reports `archive: blocked` + `verifyReport: missing` because the native dispatcher reads **only OpenSpec files** and cannot observe Engram-persisted artifacts: no `verify-report.md`/`apply-progress.md` exist on disk for this change (both phases persisted Engram-only, obs #479/#478). `blockedReasons: []` is empty. Per the status contract, artifact status is resolved from Engram for Engram-backed artifacts: the strict verify envelope (pass_with_warnings, 0 CRITICAL, 13/13, 41/41) + orchestrator final-state facts satisfy the archive gate. |

## Spec Sync

### New canonical domain — `event-approval`

No `openspec/specs/event-approval/` existed before this archive. Per OpenSpec convention the delta IS the full spec for the new capability; it was copied **mechanically** with the shell (never model Read→Write), verified by mandatory readback:

```
=== VERBATIM diff -r OUTPUT (event-approval domain copy readback) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

Result: `openspec/specs/event-approval/spec.md` — 10 requirements (EA-001..010), 24 scenarios, coverage matrix. No header normalization needed (delta already uses canonical `## Requirements` framing).

### MODIFIED canonical domains (deltas merged into existing main specs)

| Domain | Main spec before | Merge action | Result |
|--------|------------------|--------------|--------|
| catalog-filtering | `openspec/specs/catalog-filtering/spec.md` (EHE-001/002/003) | Replaced EHE-002 and EHE-003 blocks with the delta's full MODIFIED blocks (all scenarios); updated Purpose + coverage matrix | EHE-001 preserved; EHE-002 5 scenarios (added pending-absent, rejected-absent); EHE-003 6 scenarios (added pending-404, rejected-404; management variant renamed to returns-unapproved) |
| role-access | `openspec/specs/role-access/spec.md` (EHE-006/007/008) | Replaced EHE-006 block with the delta's full MODIFIED block (all scenarios); updated Purpose + coverage matrix | EHE-007/EHE-008 preserved; EHE-006 6 scenarios (added dashboard-lists-pending-rejected, opens-pending-detail, dashboard-hides-edit) |

Merge normalization (recorded, intentional): the delta-only `(Previously: ...)` change-history parentheticals were not carried into the canonical main specs (they describe pre-change state, not final state); delta `## MODIFIED Requirements` framing resolves to the canonical `## Requirements` section. This mirrors the repo convention established by the `hide-expired-events` archive (framing normalization during sync). Requirements not mentioned in the deltas (EHE-001, EHE-007, EHE-008) are byte-preserved.

## Archive Move

`openspec/changes/event-approval/` → `openspec/changes/archive/2026-08-14-event-approval/` via `git mv` (all 6 files tracked). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (it did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `design.md`, `tasks.md` (22/22 `[x]`), `specs/{event-approval,catalog-filtering,role-access}/spec.md` + this report. NOTE: no `verify-report.md`/`apply-progress.md` on disk (verify/apply persisted Engram-only — obs #478/#479; see Gates). No `state.yaml` exists in this repo's change folders (consistent with prior archives); the archive move is the status closure. Active `openspec/changes/` no longer contains the change.

## Decisions & Deviations (final)

1. **Dispatcher `archive: blocked` resolved via Engram** — native dispatcher cannot see Engram-only verify/apply artifacts; `blockedReasons: []` empty; strict verify envelope + orchestrator final-state facts satisfy the gate. Recorded here for audit.
2. **Engram tasks topic stale** — obs #477 is the pre-apply snapshot (all `- [ ]`); apply updated only the filesystem tasks.md. Filesystem + native `allComplete: true` are authoritative. Future phases should upsert the Engram tasks topic at apply close.
3. **Verify SUGGESTIONs carried as follow-ups (non-blocking)**: S1 (WAF 403 test hardening for approve/reject, optional); S2 (`Pendientes: 0` badge renders unconditionally — cosmetic, literal from design); S3 (pending count derived from page-1/pageSize-200 listing — same limitation as the full listing, v1 acceptable).
4. **Migration NOT applied to any DB (by design)** — verify INFO I1; pending operational step before deploy (see Final State).
5. **Rollback**: `dotnet ef database update <prior>` drops `Status`; backfilled events keep `Approved`, so rollback re-shows all previously-public content (pre-change behavior). Edge case accepted in design: a Pending event created post-deploy becomes publicly visible on rollback.
6. **No review artifacts exist for this candidate** — no receipt/ledger/transaction topics or files; `reviewGate` structurally absent.

## Engram Traceability

Observations read for this archive (Engram, project `ticketstart`):

| ID | Artifact | Read |
|----|----------|------|
| #473 | `sdd/event-approval/exploration` | search preview (referenced by proposal) |
| #474 | `sdd/event-approval/proposal` | filesystem copy (proposal.md, full) + search preview |
| #475 | `sdd/event-approval/spec` | filesystem copies (3 delta spec.md, full) + search preview |
| #476 | `sdd/event-approval/design` | filesystem copy (design.md, full) + search preview |
| #477 | `sdd/event-approval/tasks` | mem_get_observation full content — PRE-APPLY snapshot (all unchecked); superseded by filesystem tasks.md + native dispatcher |
| #478 | `sdd/event-approval/apply-progress` | search preview (intermediate snapshot — final-state facts supersede) |
| #479 | `sdd/event-approval/verify-report` | mem_get_observation full content (strict verify envelope, final evidence) |

This archive report persisted as Engram topic `sdd/event-approval/archive-report`.
