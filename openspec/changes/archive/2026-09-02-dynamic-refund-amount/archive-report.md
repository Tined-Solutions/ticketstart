# Archive Report: dynamic-refund-amount

**Change**: `dynamic-refund-amount` — Admin-defined refund amount (partial refunds, ledger-only)
**Archived**: 2026-09-02
**Status**: CLOSED — archived successfully (SDD cycle complete: proposed → specified → designed → tasked → applied → verified → archived)
**Artifact store**: hybrid (openspec filesystem + Engram)

## Close-Out Summary

The change implements admin-defined refund amounts on `POST /api/admin/events/{eventId}/purchases/{reservationId}/refund`: body `{ quantity, amount }` with `0 < amount ≤ TicketType.Price × K`; amount guards (≤ 0, > cap, > 2 decimals — rejected, never rounded) firing after quantity guards inside the locked transaction; the ledger storing the amount verbatim; a frontend amount input with percent quick-button sugar (a percent never crosses the wire); and the no-motivo / no-Money-Movement / no-email invariants preserved. All 4 delta requirements (APR-003, APR-010, APR-011, APR-012) and the MODIFIED Purpose were merged into the canonical spec, and the change folder was moved to the archive.

## Spec Sync Summary

Target: `openspec/specs/admin-purchase-refunds/spec.md` (canonical source of truth).

| Delta section | Merge action | Result in canonical spec |
|---|---|---|
| MODIFIED Purpose | Replaced Purpose paragraph | Now states the admin-defined decimal amount (0 < amount ≤ unit price × K) |
| MODIFIED APR-003 (Atomic quantity-based refund) | Replaced requirement body + scenarios | 11 scenarios (was 6): added Custom partial amount stored verbatim, Quantity guard fires before amount guard, Amount above cap is blocked, Amount zero or negative is blocked, More than two decimals rejected not rounded |
| MODIFIED APR-010 (Admin UI) | Replaced requirement body + scenarios | 7 scenarios (was 4): added Amount input prefilled to K × unit price, Percent helper converts client-side, Invalid amount blocks submit |
| MODIFIED APR-011 (Test coverage) | Replaced requirement body | Adds mechanical signature updates, amount guard/parity/verbatim coverage, FsCheck property suite, controller `{ quantity, amount }` body + audit amount with no motivo |
| MODIFIED APR-012 (Cumulative refund operation record) | Replaced requirement body + scenarios | 4 scenarios (was 2): added Full-price amount preserves today's ledger semantics, Cumulative custom amounts never exceed total paid; Amount semantics = admin-defined, stored verbatim |
| Unchanged: APR-001, APR-002, APR-004–APR-009, APR-013–APR-015, Non-Goals | Preserved verbatim | No edits |

Notes:

- The delta's `(Previously: …)` annotations are delta provenance and were intentionally not carried into the canonical spec (the canonical spec records current behavior; change history lives in the archived delta).
- The merge was non-destructive: 11 of 15 requirements and the Non-Goals paragraph were preserved verbatim. `rules.archive` (warn before destructive merges) was checked and not triggered.
- Final canonical spec state: 15 requirements (APR-001…APR-015) with Purpose and Non-Goals intact, heading hierarchy preserved.

## Verification Provenance

- **Verify verdict**: PASS — native `gentle-ai sdd-verify-validate` gate verdict: pass (gate-admitted).
- **Evidence revision**: `sha256:1f130ec84799c7ca5ce906387cee57960d20a88180a05f67071c589f3f899d70` (preimage: concatenated baseline-excluded backend + frontend evidence logs).
- **Delta coverage**: 4/4 requirements, 23/23 scenarios compliant (APR-003: 11, APR-010: 7, APR-011: 1, APR-012: 4, plus MODIFIED Purpose).
- **Severity counts at verification time**: 0 CRITICAL, 0 WARNING, 3 SUGGESTION (see Follow-Up Suggestions below).
- **Final observed test evidence** (verify date 2026-09-02):
  - Backend full `dotnet test`: 725/730 passed, run twice with identical counts — failures strictly within the 6 pre-existing baselines; `EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` (a wall-clock timing test) flaked green in both full runs.
  - Backend change-scope suite (baseline-excluded envelope command): 724/724, exit 0.
  - Refund-filtered suites: 40/40 (service 24 + controller 11 + FsCheck 5).
  - Frontend `npm test`: 490/493 passed, run twice — exactly the 3 pre-existing baselines (Checkout ×2, identityValidation DNI letters).
  - `dotnet build` and `npm run build`: exit 0.
- **Provenance ranking**: the numbers above come from the orchestrator's final-state facts and the persisted verify report. The apply-progress snapshot (Engram observation #641) recorded "backend 724/730, frontend 490/493" at apply time; the 724-vs-725 backend delta is the timing baseline's documented env-dependent flake, not a contradiction. Both snapshots agree on the material fact: zero new failures, all failures confined to the pre-existing baselines verified failing at base `bd7b7cc`.

## Deviation Notes

1. **Controller test rename (legitimate, recorded for completeness)**: `RefundPurchase_Success_PassesQuantityBodyAndWritesAuditWithoutMotivo` was renamed to `RefundPurchase_Success_PassesAmountBodyAndWritesAuditWithoutMotivo` during apply. This is a D6-sanctioned mechanical rename (quantity→amount body semantics executed against current test names) and was absent from the apply-progress deviation list. The verify report examined and sanctioned it; recorded here so the archived audit trail is complete.
2. **size:exception — 939 authored lines (accepted)**: the tasks forecast was ~700 lines; the actual authored total is 939 lines (859 insertions / 80 deletions across 11 files), exceeding the approved 800-line size:exception budget by ~14%. Main overshoot: `backend/Tests/AdminPurchaseRefundPropertyTests.cs` (318 lines vs ~150 forecast) due to repo-convention per-class seed helpers; spec-mandated coverage was kept intact. Accepted via maintainer ledger reset — revision `sha256:e3373550378520f9fce9ef341c72418c2f3d31abb2cd00ada16e3b6b4bcb62fc`, actor martin. Delivery remained a single PR per the `single-pr` strategy.
3. **Change folder left untracked at apply**: `openspec/changes/dynamic-refund-amount/` was intentionally left untracked by sdd-apply for the orchestrator's docs commit; this archive phase moved it on disk without committing (see Final State).

## Non-Goals — Verified Holding at Close

- No DB migration (no `backend/Migrations` changes; only `backend/Models/Refund.cs` doc comment under Models).
- No Mercado Pago call in the manual refund path (service makes zero external-service calls; controller test proves PaymentService is never invoked).
- No email in the refund path.
- No motivo field (DTO is `(Quantity, Amount)` only; audit assertions pin no motivo/reason — APR-008 invariant preserved).
- Percent never crosses the wire (mutation posts `{ quantity, amount }` only).

## Follow-Up Suggestions (non-blocking, from verify)

1. **`EventNotificationQueueTests.EnqueueAsync_ReturnsImmediately` timing threshold**: the wall-clock assertion (< 1000 ms) is load-sensitive (flaked red once under a filtered run; green in both full verify runs). Consider a lenient threshold or a load-guard in a future hygiene change.
2. **`<input type="number">` scientific notation**: some browsers accept strings like `1e2`; the dialog's `toCents`/`Number` classification currently handles such input correctly against the cap/decimals guards. Revisit if the dialog ever moves to a text-based input.

## Gate Record

- **Task Completion Gate**: PASS — `tasks.md` 17/17 checked; no stale checkboxes; no archive-time reconciliation needed.
- **Native Review Receipt Gate**: `reviewGate` structurally absent (no review artifact was ever discovered for this candidate) → archived under ordinary repository policy.
- **CRITICAL check**: 0 CRITICAL findings in the verify report — nothing blocks archive.

## Final State

- **Branch**: `feat/dynamic-refund-amount`; HEAD `165ea49763bca13d8201eb68ba360931b31c020b`; base `bd7b7ccb1c8f789787bc2ccd7cb9383f638a7eb4`.
- **Apply commits** (from base): `40f0335` (WU1 signature/DTO), `5060b95` (WU2 guards + verbatim ledger), `b9dd48f` (WU3 FsCheck suite), `6641b49` (WU4 controller body + audit amount), `113077a` (WU5 `formatCurrency` fractionDigits), `165ea49` (WU6 dialog amount + % buttons, HEAD). 11 files, 859 ins / 80 del.
- **Canonical spec (source of truth)**: `openspec/specs/admin-purchase-refunds/spec.md` — synced (see Spec Sync Summary).
- **Archived change folder**: `openspec/changes/archive/2026-09-02-dynamic-refund-amount/` — contains proposal.md, exploration.md, specs/admin-purchase-refunds/spec.md (delta), design.md, tasks.md (17/17), verify-report.md, and this archive-report.md. Moved mechanically with a byte-identical `diff -r` readback against a pre-move recursive snapshot.
- **Engram**: `sdd/dynamic-refund-amount/apply-progress` (observation #641, read at archive), `sdd/dynamic-refund-amount/archive-report` (this report, persisted via mem_save).
- **The archive phase modified no source code and made no git commits** — the docs commit (spec sync + archive move) is owned by the orchestrator.
