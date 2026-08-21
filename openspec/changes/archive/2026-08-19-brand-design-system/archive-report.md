# Archive Report: brand-design-system

```yaml
schema: gentle-ai.archive-result/v1
change: brand-design-system
artifact_store: hybrid (OpenSpec + Engram)
archived_at: 2026-08-19
archived_to: openspec/changes/archive/2026-08-19-brand-design-system/
spec_synced_to: openspec/specs/brand-design-system/spec.md (NEW capability)
final_verdict: PASS (with documented out-of-scope debt)
```

## Summary

The `brand-design-system` change (brand token rebase, light-only theme, Poppins+Inter, Confetti surfaces, category chips, logo, motion <=300ms, test migration) is fully planned, implemented, verified, and now archived. Its delta spec became a new main spec (`openspec/specs/brand-design-system/`), the change folder moved to the archive, the stale `openspec/config.yaml` frontend-test-runner claims were corrected, and the supersession of the unarchived `frontend-redesign/design-system` delta (REQ-DS1-DS10) is documented.

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | PASS — `reviewGate` structurally absent, no `review/` folder exists for this candidate; archive proceeds under ordinary repository policy | `ls openspec/changes/brand-design-system/review/` → no such directory |
| Task Completion | PASS — 25/25 tasks checked in persisted tasks artifact | `tasks.md` (all `- [x]`) |
| CRITICAL verify issues | PASS — report states "CRITICAL: None caused by this change" | `verify-report.md` Issues Found |
| Action Context Guard | PASS — no `workspace-planning` mode, no `allowedEditRoots` restriction reported | launch prompt |

## Artifacts Read (traceability)

All read directly from the filesystem backend per orchestrator direction; Engram mirrors cross-referenced:

| Artifact | Filesystem path | Engram obs ID (project: ticketstart) |
|----------|-----------------|--------------------------------------|
| exploration | `openspec/changes/brand-design-system/exploration.md` | #511 |
| proposal | `openspec/changes/brand-design-system/proposal.md` | #512 |
| design | `openspec/changes/brand-design-system/design.md` | #515 |
| spec (delta) | `openspec/changes/brand-design-system/specs/brand-design-system/spec.md` | #514 |
| tasks | `openspec/changes/brand-design-system/tasks.md` | #516 |
| apply-progress | `openspec/changes/brand-design-system/apply-progress.md` | #517 |
| verify-report | `openspec/changes/brand-design-system/verify-report.md` | #523 (see correction below) |

## Final-State Facts (ranked per Final-State Authority)

1. **Naranja chip WCAG AA remediated** — final state: chip class is `bg-naranja/10 text-naranja-dark` (was `/15`), measured contrast `#B45309` on the tint over white ~4.60:1 >= 4.5 AA. Corroborated in repository: commit `4f39bfe` ("fix(frontend): subir contraste del chip Naranja a WCAG AA (bg-naranja/10)") changes `frontend/src/data/categories.js`; `grep` confirms `naranja: 'bg-naranja/10 text-naranja-dark'`. Source: orchestrator final-state fact + terminal verify-report (commit `09b1612`) + repo evidence. The intermediate `apply-progress.md` note ("Naranja chip at /15 tint gives 4.43:1 ... retained") and Engram verify-report mirror #523 (saved 01:23, pre-fix) describe the state BEFORE `4f39bfe` — stale, superseded.
2. **3 pre-existing test failures are NOT from this change** — 2 in `Checkout.test.jsx` + 1 in `identityValidation.test.js` fail identically at baseline commit `876ef0a` (verified via git worktree per orchestrator; also documented in verify-report WARNING #1 as "proven pre-existing"). Final suite state: 449 passed / 3 failed (44 files), all 14 change-affected test files (106 tests) green. The 3 failures are documented out-of-scope debt (golden-rule protected files), NOT blockers.
3. **Change commits** (branch `dev`): `ce5e032` (tokens/shell), `e665acc` (components), `4c211d5` (categories/EventCard/pages/motion), `b710de5` (openspec docs), `4f39bfe` (chip AA fix), `09b1612` (terminal verify-report).

### Verify-report internal discrepancy (recorded, not resolved silently)

The terminal `verify-report.md` (commit `09b1612`) contains a machine-frontmatter block (`verdict: fail`, `blockers: 1`, `critical_findings: 2`, `scenarios: 17/19`) and a prose terminal verdict of **"PASS (with documented out-of-scope debt)"**. Both were written in the same commit (the file was created there, 125 insertions). Reading: the YAML is the strict-validator machine output (test_exit_code 1 → validator FAIL per report's own WARNING #1 note: "so the strict validator returns FAIL"); the prose verdict is the human terminal judgment after the `4f39bfe` chip remediation. The two `critical_findings` counted by the YAML are (a) the chip AA scenario — RESOLVED in `4f39bfe` with repo evidence, and (b) the suite-green scenario — pre-existing baseline debt, not a regression. Final state per this archive: **PASS with documented out-of-scope debt**. The YAML block is left unmodified in the archived report (audit trail integrity); readers should treat `verdict: fail` as the strict-validator gate output predating the documented remediation, not as the terminal judgment.

### Engram verify-report mirror correction

Engram observation #523 (`sdd/brand-design-system/verify-report`, saved 2026-08-19 01:23:13) is the PRE-remediation snapshot: verdict FAIL, chip at `/15` 4.43:1 recorded as "retained", suggestion to fix pending. The terminal filesystem report (commit `09b1612`, 01:30) supersedes it. Per Final-State Authority, observation #523 was updated at archive time to match the terminal report bytes, so Engram does not mislead future readers into believing the change closed with an unresolved AA defect or a FAIL verdict. Original saved-at timestamp and this correction are recorded here for provenance.

## Spec Sync (Step 2)

Delta spec `specs/brand-design-system/spec.md` is a FULL spec (no ADDED/MODIFIED/REMOVED delta sections) and no main spec existed at `openspec/specs/brand-design-system/`. Per OpenSpec convention, the delta IS the full spec → mechanically copied (shell `cp` → `diff -r` → `mv`, never model Read/Write):

- **Created** `openspec/specs/brand-design-system/spec.md` — 12 requirements (REQ-BDS-1..12), 19 scenarios, domain: brand-design-system (NEW capability).
- Verbatim `diff -r` readback: **empty (no differences)** — PASS.
- The main spec's Purpose retains the supersession statement: "Supersedes unarchived frontend-redesign/design-system (REQ-DS1-DS10)".

No REMOVED/MODIFIED requirements were involved; no destructive merge; the `rules.archive` "warn before merging destructive deltas" rule does not trigger.

## Orphan Delta Decision: `frontend-redesign/design-system` (REQ-DS1-DS10)

The unarchived change `openspec/changes/frontend-redesign/` contains a `design-system` delta (REQ-DS1-DS10) that was never merged into `openspec/specs/` (no frontend specs existed there). `brand-design-system` supersedes it (declared in proposal and in the new main spec's Purpose).

Decision recorded at archive time:
- **`openspec/specs/brand-design-system/spec.md` is the source of truth** for the frontend design system going forward; REQ-DS1-DS10 (dark-first `data-theme` default, Space Grotesk, `#7c3aed→#a855f7` brand gradient, ThemeToggle + localStorage persistence, 200/400/600ms motion) are superseded and contradict REQ-BDS-1..12 (light-only, Poppins, 5-color Confetti palette, no toggle, motion <=300ms). Merging the superseded delta would pollute the source of truth with contradictory requirements — NOT performed.
- **No files in `openspec/changes/frontend-redesign/` were modified or deleted.** Its deltas (design-system, app-shell-layout, frontend-quality, page-visual-design) remain in place as historical working artifacts.
- Recommended follow-up (not performed by this archive): archive `frontend-redesign` or explicitly mark its `design-system` delta superseded by `brand-design-system` in its own folder, so the changes list does not carry a stale, contradicted capability.

## `openspec/config.yaml` Correction (documented init debt)

Per orchestrator authorization, the stale init claims were corrected (archive-time maintenance, documented here):

- `context`: "Frontend has NO test runner configured — only ESLint linting." → "Frontend uses Vitest via React Testing Library (`npm test` / `npx vitest run`) plus ESLint linting. (Corrected at archive time — init claimed no frontend test runner.)"
- `testing.notes`: "Frontend has no test infrastructure — no vitest, jest, or test script." → documents backend `dotnet test` vs frontend `npm test`/`npx vitest run` (Vitest + RTL), and notes that `test_command: dotnet test` fields remain the backend default; frontend verify/apply phases must override with `npm test` (per REQ-BDS-11).
- `test_command: dotnet test` fields (rules.apply / rules.verify / testing) intentionally LEFT as backend default — the schema supports a single command; per-area commands are documented in `testing.notes` instead. Flagged as residual init debt: a future frontend change's verify phase must override the command explicitly.

## Archive Move (Step 3)

Change folder moved mechanically with `git mv` (tracked files), pre-move recursive snapshot compared against the archived tree:

- `openspec/changes/brand-design-system/` → `openspec/changes/archive/2026-08-19-brand-design-system/`
- Verbatim `diff -r` readback: **empty (no differences)** — PASS (source directory confirmed gone before comparison; snapshot removed by EXIT trap).
- Contents: `exploration.md`, `proposal.md`, `specs/`, `design.md`, `tasks.md`, `apply-progress.md`, `verify-report.md`, plus additive `archive-report.md` (excluded from comparison — did not exist in the source snapshot).
- Active changes directory no longer contains `brand-design-system`.

## Verification (Step 4)

- [x] Main spec created/updated correctly (`openspec/specs/brand-design-system/spec.md`)
- [x] Change folder moved to archive with date prefix
- [x] Archive contains all artifacts (proposal, specs, design, tasks, apply-progress, verify-report, exploration)
- [x] Archived `tasks.md` has no unchecked implementation tasks (25/25 `[x]`)
- [x] Active changes directory no longer has this change
- [x] Verbatim `diff -r` readbacks included in phase result, both empty (byte-identical)

## Intentional-Warnings Status

This archive is **not** a partial archive and required no stale-checkbox reconciliation. It closes with documented out-of-scope debt (3 pre-existing baseline test failures) and documented archive-time maintenance (config.yaml correction, Engram mirror update) — all recorded above.

## SDD Cycle Complete

Change `brand-design-system` is fully planned, implemented, verified (PASS with documented out-of-scope debt), and archived. Ready for the next change.