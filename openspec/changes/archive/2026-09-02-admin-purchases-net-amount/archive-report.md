# Archive Report: admin-purchases-net-amount

**Change**: `admin-purchases-net-amount` — Net amount display in admin purchases (APR-016)
**Archived**: 2026-09-02
**Status**: CLOSED — archived successfully (SDD cycle complete: proposed → specified → designed → tasked → applied → verified → archived)
**Artifact store**: hybrid (openspec filesystem + Engram)

## Close-Out Summary

The change makes the admin purchases table communicate retained revenue after partial refunds: each refunded row's Monto cell renders `purchase.amount − purchase.refundedAmount` (original amount otherwise, `$ 0` when fully refunded with the error badge still visible), and the header renders `Total: $X · Reembolsado: $Y · Neto: $Z` from Σ `purchase.amount`, `data.totalRefunded` rendered verbatim, and their difference. It is a display-only frontend change: `purchase.amount` is never mutated, the refund dialog derivation (`unitPriceCents`/`capCents`) and the `{ quantity, amount }` POST payload stay untouched, and there is no backend/API/DB work. The MODIFIED Purpose, ADDED APR-016 (1 requirement, 5 scenarios), and ADDED Non-Goals were merged into the canonical spec, and the change folder was moved to the archive.

## Spec Sync Summary

Target: `openspec/specs/admin-purchase-refunds/spec.md` (canonical source of truth).

| Delta section | Merge action | Result in canonical spec |
|---|---|---|
| MODIFIED Purpose | Replaced Purpose paragraph | Now also requires the admin purchases page to display each refunded row's net amount and a `Total · Reembolsado · Neto` event summary |
| ADDED APR-016 (Net amount display in admin purchases) | Appended at end of the requirement list | New requirement with 5 scenarios (partially refunded row shows net amount, fully refunded row shows zero, non-refunded row keeps original amount, header summary shows Total/Reembolsado/Neto, header Reembolsado equals Σ Refunds.Amount) |
| ADDED Non-Goals | Appended into the existing Non-Goals paragraph | Adds display-scope non-goals: no backend/API/database/refund-dialog semantics change, no `purchase.amount` mutation, no per-row amount-breakdown variant or per-purchase badge enrichment, no OrganizerDashboard/MetricsService revenue-asymmetry work |
| Unchanged: APR-001…APR-015, existing Non-Goals content | Preserved verbatim | No edits |

Notes:

- The delta's `(Previously: …)` annotation is delta provenance and was intentionally not carried into the canonical spec (the canonical spec records current behavior; change history lives in the archived delta), matching the `dynamic-refund-amount` archive convention.
- The merge was non-destructive: all 15 pre-existing requirements and the pre-existing Non-Goals content were preserved verbatim. `rules.archive` (warn before destructive merges) was checked and not triggered.
- Final canonical spec state: 16 requirements (APR-001…APR-016) with Purpose and Non-Goals intact, heading hierarchy preserved.

## Verification Provenance

- **Verify verdict**: PASS — native envelope: requirements 1/1, scenarios 5/5, 0 blockers, 0 CRITICAL, 0 WARNING.
- **Evidence revision**: `sha256:f0e93a5a21ecba0207b740c420b229f35af087b1df2dc03b1517f9d3c026f111` (preimage: concatenated focused/full/build evidence logs).
- **Delta coverage**: 1/1 requirement (APR-016), 5/5 scenarios compliant.
- **Severity counts at verification time**: 0 CRITICAL, 0 WARNING, 1 SUGGESTION (informational — header scenario illustrative numbers differ from the test fixture; no action required).
- **Final observed test evidence** (verify date 2026-09-02):
  - Focused change-scope suite `npx vitest run src/pages/AdminPurchases.test.jsx`: 18/18, exit 0.
  - Full frontend suite `npm test`: 492 passed / 3 failed / 495 total — the 3 failures are the documented pre-existing baselines (Checkout.test.jsx ×2, identityValidation.test.js DNI letters), all in files untouched by the change; zero new failures.
  - `npm run build`: exit 0; ESLint on the changed files: 0 errors, 0 warnings.
- **Provenance ranking**: the numbers above come from the orchestrator's final-state facts (implementation commit `0bd37d4`, 8/8 tasks complete, full suite 492/3) and the persisted verify report. No verify warnings were fixed after the verify-report was written and no tasks were completed after apply-progress beyond the 8/8 already recorded, so no later work changed these numbers. The apply-progress locator reported `<unresolved>` at archive time; the persisted tasks artifact (8/8 checked) and the verify report govern final state.

## Deviation Notes

None. The change stayed within its forecast: a single frontend commit `0bd37d4` touching only `frontend/src/pages/AdminPurchases.jsx` (+9/−2) and `frontend/src/pages/AdminPurchases.test.jsx` (+91), well under the 400-line review budget; single-pr delivery per the tasks forecast.

## Non-Goals — Verified Holding at Close

- No backend, API, or database change (`git diff 0bd37d4^..0bd37d4 --name-only` = the two frontend files only).
- `purchase.amount` never mutated (read-only conditional expression at the Monto cell; no assignment anywhere in the file).
- Refund dialog semantics untouched: `unitPriceCents`/`capCents` derivation and the `{ quantity, amount }` POST body byte-identical pre/post change.
- No per-row amount-breakdown variant, no optional per-purchase badge enrichment.
- No OrganizerDashboard/MetricsService revenue-asymmetry work.

## Follow-Up Suggestions (non-blocking, from verify)

1. **Informational (SUGGESTION 1)**: the APR-016 header scenario's illustrative numbers (Total 500 / Neto 350) differ from the test fixture (Total 350 / Neto 200); the test verifies the identical derivation formula (X = Σ amount, Y = `totalRefunded` verbatim, Z = X − Y) with a different fixture, so scenario intent is fully covered. No action required.

## Gate Record

- **Task Completion Gate**: PASS — `tasks.md` 8/8 checked; no stale checkboxes; no archive-time reconciliation needed.
- **Archive Readiness**: PASS — native `gentle-ai sdd-status` reports `dependencies.archive: ready`, `nextRecommended: archive`, `applyState: all_done`, and empty `blockedReasons`; action context is repo-local with `allowedEditRoots` covering this workspace.
- **CRITICAL check**: 0 CRITICAL findings in the verify report — nothing blocks archive.

## Final State

- **Branch**: `feat/dynamic-refund-amount`; implementation HEAD `0bd37d4b131a9d3b91f3b315f4e4431f60e6f0b9`.
- **Apply commit**: `0bd37d4` `feat(frontend): columna Monto neta y resumen Total/Reembolsado/Neto en compras del admin` — 2 files, 98 insertions / 2 deletions.
- **Canonical spec (source of truth)**: `openspec/specs/admin-purchase-refunds/spec.md` — synced (see Spec Sync Summary).
- **Archived change folder**: `openspec/changes/archive/2026-09-02-admin-purchases-net-amount/` — contains proposal.md, specs/admin-purchase-refunds/spec.md (delta), design.md, tasks.md (8/8), verify-report.md, and this archive-report.md. Moved mechanically with a byte-identical `diff -r` readback against a pre-move recursive snapshot.
- **Engram**: `sdd/admin-purchases-net-amount/archive-report` (this report, persisted via mem_save).
- **Docs commit**: this archive phase created the single docs commit (spec sync + archive move, orchestrator-approved); no implementation code was touched and nothing was pushed.