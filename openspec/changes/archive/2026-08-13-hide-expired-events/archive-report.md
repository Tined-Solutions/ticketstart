# Archive Report: hide-expired-events

**Change**: hide-expired-events
**Archived**: 2026-08-13
**Archive path**: `openspec/changes/archive/2026-08-13-hide-expired-events/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram)
**Capabilities**: `catalog-filtering`, `purchase-guards`, `role-access`, `feature-flag` (4 new canonical domains)
**Main specs**: `openspec/specs/{catalog-filtering,purchase-guards,role-access,feature-flag}/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. 28/28 implementation tasks complete. All 11 requirements (EHE-001..011) and all 36/36 scenarios verified. Validator verdict `pass_with_warnings` (admitted via `gentle-ai sdd-verify-validate`, evidence_revision `sha256:442e5ac5...`). 0 blockers, 0 CRITICAL findings.

**Final test evidence (at close):**
- Backend full suite: **609 total / 604 passed / 5 known pre-existing failures / 0 regressions** (per orchestrator final-state facts and persisted verify-report #86). The 5 failures are the documented pre-existing baseline (PaymentProperty17, Webhook_InvalidSignature ×2, PendingEmailRetry exhaustion, AuthCookie live-DB-only), all present in prior archives; plus the known flaky `QRCodePropertyTests.Property21` excluded from the clean evidence run.
- Clean evidence run (envelope): **603/603 passed, exit 0** — excludes the 5 pre-existing failures + 1 documented flaky test.
- Build: `dotnet build --no-restore` — 0 errors, exit 0.
- Delivery: **13 local commits on `dev` (`df5727b..4b95def`), NOT pushed** — orchestrator handles delivery/PRs (chained PRs per maintainer decision).

These figures are the orchestrator's final-state facts (launch prompt, most recent account) and corroborate the persisted `verify-report` (#86, written 2026-08-13 11:37). No contradiction between sources. Apply-progress (#85) is an intermediate snapshot; its claims of pending work were superseded by verify/archive completion and are not restated as current facts.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | `reviewGate` absent in native `gentle-ai sdd-status` output; no review artifacts ever discovered for this candidate (no `reviews/` files, no `sdd/hide-expired-events/review/*` Engram topics). `reviewOffer` present is an invitation, not a gate — declining proceeds to archive under ordinary repository policy. |
| Task Completion | Pass | `tasks.md`: 28/28 `[x]`, 0 unchecked implementation tasks (native status `taskProgress.allComplete: true`). |
| CRITICAL verification issues | None | `critical_findings: 0` in verify-report. |
| Action Context | Pass | `actionContext.mode: repo-local`; `allowedEditRoots: [C:\Users\user\Desktop\ticketstart]`; all operations inside workspace root. |

## Spec Sync

All 4 delta specs target **new canonical domains** (no existing `openspec/specs/{domain}/` before this archive). Per the OpenSpec convention, each delta IS the full spec for a new capability and was copied mechanically (shell `Copy-Item`, never model Read→Write).

**Per-domain merge** (delta → canonical):

| Domain | Source delta | Canonical | Merge |
|--------|-------------|-----------|-------|
| catalog-filtering | `openspec/changes/hide-expired-events/specs/catalog-filtering/spec.md` | `openspec/specs/catalog-filtering/spec.md` | 3 requirements (EHE-001/002/003), 10 scenarios; header `## ADDED Requirements` → `## Requirements`; **S1 retitle applied** |
| purchase-guards | `.../specs/purchase-guards/spec.md` | `openspec/specs/purchase-guards/spec.md` | 3 requirements (EHE-004/005/011), 8 scenarios; header normalized |
| role-access | `.../specs/role-access/spec.md` | `openspec/specs/role-access/spec.md` | 3 requirements (EHE-006/007/008), 8 scenarios; header normalized |
| feature-flag | `.../specs/feature-flag/spec.md` | `openspec/specs/feature-flag/spec.md` | 2 requirements (EHE-009/010), 10 scenarios; header normalized |

**Mechanical copy readbacks (each domain, source vs temp copy before move):** `git diff --no-index --exit-code` → **exit 0, empty output** for all 4 (byte-identical copy, verbatim output captured in phase result).

**Post-copy merge verification** (`git diff --no-index` canonical vs delta source): exactly 2 changed lines per spec — `-## ADDED Requirements` / `+## Requirements`. No requirement, scenario, or coverage-matrix content altered. This is the repo's canonical framing convention (`## Requirements`, matching all 3 pre-existing main specs); the `## ADDED Requirements` delta framing is intentionally normalized during archive sync.

**S1 (verify SUGGESTION) — scenario retitle applied in BOTH copies:**
- Delta source spec line 27: `#### Scenario: Event at exact start instant is expired (strict less-than)` → `#### Scenario: Event at exact start instant is **not** expired (strict less-than: \`Date == asOf\` → \`false\`)`
- Applied to the change spec BEFORE the archive move, so the archived change copy carries the retitle; the canonical copy inherits it via the mechanical copy (confirmed at `openspec/specs/catalog-filtering/spec.md:27`). This is an intentional, orchestrator-ordered spec correction (S1), recorded here; the archived copy is otherwise byte-identical to the source snapshot (see readback below).

## Archive Move

`openspec/changes/hide-expired-events/` → `openspec/changes/archive/2026-08-13-hide-expired-events/` via `git mv` (fell back to `mv` — the folder mixes tracked and untracked files, verify-report.md being untracked). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `design.md`, `tasks.md` (28/28), `verify-report.md`, `specs/{catalog-filtering,purchase-guards,role-access,feature-flag}/spec.md` + this report. Active `openspec/changes/` no longer contains the change. No `state.yaml` exists in this repo's change folders (consistent with prior archives); the archive move is the status closure.

## Decisions & Deviations (final)

1. **S1 retitle** (orchestrator-ordered): catalog-filtering exact-instant scenario title corrected to "not expired (strict less-than: `Date == asOf` → `false`)" in BOTH the archived change copy and the canonical spec. Intentional, recorded, per verify-report SUGGESTION S1.
2. **Header normalization** during delta sync: `## ADDED Requirements` → `## Requirements` in the 4 canonical main specs (repo convention; only the framing header changes, requirement content byte-identical).
3. **W1 (verify WARNING) — Npgsql `FindAsync` fallback NOT smoke-tested against real PostgreSQL**: carried to deployment. The Event navigation is loaded via a second PK `FindAsync` because `SELECT ... FOR UPDATE` is not `.Include`-composable on Npgsql; SQLite single-round-trip test intentionally does not assert the Npgsql branch. Impact: low (correctness preserved; the untested surface is generated SQL shape, not logic). Manual smoke-test steps are in `verify-report.md` §W1 and MUST be run before/at deployment.
4. **S2 (verify SUGGESTION) — NOT done, listed as follow-up**: dedicated regression tests `UploadEventImage_PastEvent_*` / `UpdateEvent_PastEvent_*` (past event + owner/admin → 200) were not carried by tasks (task 2.5 was code-only). Code is correct by inspection (`EventController.cs:128,210` pass `includeExpired: true`), but end-to-end 200-level tests are outstanding. Recommended hardening follow-up, optionally with the convention note: *any `[Authorize(Policy="EventOwnership")]` action loading an event for editing MUST call `GetEventByIdAsync(id, includeExpired: true)`*.
5. **Rollback** (EHE-009): set `HideExpiredEvents:Enabled=false` in `appsettings.json` (runtime, no redeploy) — all filters and guards become no-ops. Code revert path in proposal §Rollback Plan. No migration to revert (no schema change).

## Engram Traceability

Observations read for this archive (Engram, project `ticketstart`):

| ID | Artifact | Read |
|----|----------|------|
| #77 | `sdd/hide-expired-events/exploration` | search preview (filesystem copy absent in change folder) |
| #79 | `sdd/hide-expired-events/proposal` | filesystem copy (proposal.md, full) + search preview |
| #80 | `sdd/hide-expired-events/spec` | filesystem copies (4 delta spec.md, full) + search preview |
| #81 | `sdd/hide-expired-events/design` | filesystem copy (design.md, full) + search preview |
| #84 | `sdd/hide-expired-events/tasks` | filesystem copy (tasks.md, full) + mem_get_observation full content |
| #85 | `sdd/hide-expired-events/apply-progress` | mem_get_observation full content (intermediate snapshot — final-state facts supersede) |
| #86 | `sdd/hide-expired-events/verify-report` | mem_get_observation full content + filesystem copy |

(No review topics exist for this candidate; `#82`/`#83` are session artifacts, not change phase outputs.)
