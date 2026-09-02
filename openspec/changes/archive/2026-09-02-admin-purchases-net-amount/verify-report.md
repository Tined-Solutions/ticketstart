```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:f0e93a5a21ecba0207b740c420b229f35af087b1df2dc03b1517f9d3c026f111
verdict: pass
blockers: 0
critical_findings: 0
requirements: 1/1
scenarios: 5/5
test_command: npx vitest run src/pages/AdminPurchases.test.jsx (cwd frontend/)
test_exit_code: 0
test_output_hash: sha256:99ef5f853e7c4bcda9e9bf72561163d33db551ad1560542ac1abf3246e1dcfea
build_command: npm run build (cwd frontend/)
build_exit_code: 0
build_output_hash: sha256:42f902dfc56d5e9732e9af50f0a37a67d8fe75449292c5af66fe98da746612da
```

# Verify Report: admin-purchases-net-amount

- **Change**: `admin-purchases-net-amount`
- **Branch**: `feat/dynamic-refund-amount` — candidate `0bd37d4b131a9d3b91f3b315f4e4431f60e6f0b9`
- **Date**: 2026-09-02
- **Mode**: Strict TDD (frontend Vitest per `skills/react-testing`)
- **Verdict**: **PASS** — 1/1 requirements, 5/5 scenarios, 0 blockers, 0 CRITICAL, 0 WARNING
- **Evidence-revision preimage**: `cat verify-focused-adminpurchases.txt verify-full-frontend.txt verify-frontend-build.txt | sha256sum` → `sha256:f0e93a5a21ecba0207b740c420b229f35af087b1df2dc03b1517f9d3c026f111` (logs preserved at `/tmp/opencode/`)

## Verdict rationale (read this first)

- **ZERO new failures.** The full frontend suite fails exactly 3 tests, all three byte-identical to the documented pre-existing baselines (`Checkout.test.jsx` ×2, `identityValidation.test.js` DNI letters) recorded in the admitted `dynamic-refund-amount` verify-report (2026-09-02). Commit `0bd37d4` touches only `AdminPurchases.jsx` (+9/−2) and `AdminPurchases.test.jsx` (+91); the failing files are untouched by this change, so their failures cannot be new. Focused change-scope suite: **18/18** (16 pre-existing + 2 new APR-016 tests), exit 0.
- **All 5 APR-016 scenarios COMPLIANT** with green covering tests (mapping below); the settled design decisions hold in code; all five non-goals hold (no backend/API/DB change; `purchase.amount` never mutated; dialog derivation lines 51–52 and the `{ quantity, amount }` payload untouched; no per-row breakdown/badge enrichment; no OrganizerDashboard/MetricsService work).
- `critical_findings: 0`, `blockers: 0`.

## Observed test evidence (exact commands and counts)

| # | Command (cwd) | Observed result | Exit |
|---|---|---|---|
| 1 | `npx vitest run src/pages/AdminPurchases.test.jsx` (frontend/) — change-scope envelope command | **18 passed / 0 failed / 18 total** | **0** |
| 2 | `npm test` (frontend/) — full unfiltered suite | **Passed 492 / Failed 3 / Total 495** | 1 (baselines only) |
| 3 | `npm run build` (frontend/) | built, chunk-size warnings only | **0** |
| 4 | `npx eslint src/pages/AdminPurchases.jsx src/pages/AdminPurchases.test.jsx` (frontend/) | 0 errors, 0 warnings | **0** |

**Full-suite failures (exactly the 3 pre-existing baselines, all in files untouched by commit `0bd37d4`)**:
1. `Checkout.test.jsx > returns to the reservation form when clicking Editar datos, preserving input data` (DNI formatting `11.222.333` vs `11222333`).
2. `Checkout.test.jsx > sends a PATCH request when saving edits on an existing reservation`.
3. `identityValidation.test.js > rejects DNI with letters`.

Pre-existing provenance: the admitted `dynamic-refund-amount` verify-report (2026-09-02) records these exact three failing at base `bd7b7cc`; apply-progress safety net records the same 3 failures with 490 passes at base (`490 + 2 new APR-016 tests = 492` — arithmetic matches exactly). **New-failure list: empty.**

## Requirements and scenarios (1/1 requirements, 5/5 scenarios)

### APR-016: Net amount display in admin purchases — PASS (5/5 scenarios)

Implementation evidence (`frontend/src/pages/AdminPurchases.jsx`, candidate `0bd37d4`):

- Monto cell (line 312): `formatCurrency(purchase.refundedQuantity > 0 ? purchase.amount - purchase.refundedAmount : purchase.amount)` — conditional derived value, read-only; `purchase.amount` is never assigned anywhere in the file.
- Header (line 254): `{data.eventName} · Total: {formatCurrency(totalAmount)} · Reembolsado: {formatCurrency(data.totalRefunded)} · Neto: {formatCurrency(netAmount)}` — `totalAmount` (line 243) = `data?.purchases?.reduce((s, p) => s + p.amount, 0) ?? 0` (X = Σ amount, guarded while loading); `netAmount` (line 244) = `totalAmount - data.totalRefunded` (Z = X − Y). Y is rendered from `data.totalRefunded` verbatim — never recomputed or normalized.
- Case-insensitive fragments preserved: `/reembolsado: \$ 150/i` (test line 123) and `/reembolsado: \$ 350/i` (test line 212) both still match the extended header `<p>`.
- HARD CONSTRAINT: refund dialog `unitPriceCents`/`capCents` derivation (lines 51–52) and the mutation payload `{ quantity, amount }` (line 217) are byte-identical pre/post change (git diff shows no hunk touching them); `purchase.amount` unmutated.

Scenario → test mapping (`frontend/src/pages/AdminPurchases.test.jsx`):

| Scenario | Covering test | Result |
|---|---|---|
| Partially refunded row shows net amount (200−50 → `$ 150` + warning badge) | `AdminPurchases — net amount (APR-016) > renders the net Monto for partially refunded, fully refunded, and non-refunded rows` (row `res-partial`: `$ 150` + `1 de 2 reembolsadas`) | ✅ COMPLIANT |
| Fully refunded row shows zero (`$ 0` + error badge) | same test (row `res-2`: `$ 0` + `1 de 1 reembolsadas`) | ✅ COMPLIANT |
| Non-refunded row keeps original amount (`$ 200`) | same test (row `res-1`: `$ 200` + `Confirmada`) | ✅ COMPLIANT |
| Header summary shows Total, Reembolsado, Neto (X = Σ amount, Y verbatim, Z = X − Y) | `header shows Total, Reembolsado, and Neto` (`/total: \$ 350 · reembolsado: \$ 150 · neto: \$ 200/i`) + `/reembolsado: \$ 150/i` (existing test, line 123) | ✅ COMPLIANT |
| Header Reembolsado equals Σ Refunds.Amount, rendered verbatim | header test (Y = 150 from payload `totalRefunded`, not recomputed) + refund-flow refetch test (`/reembolsado: \$ 350/i` after `totalRefunded` 350, line 212) | ✅ COMPLIANT |

## Design coherence

| Decision | Followed? | Notes |
|----------|-----------|-------|
| Reject mutation of `purchase.amount` — derive display value inline | ✅ Yes | line 312 conditional expression; no assignment anywhere |
| Reject backend aggregation — sum `purchase.amount` in frontend, use `data.totalRefunded` verbatim | ✅ Yes | lines 243–244, 254 |
| Reuse `formatCurrency` defaults (whole pesos) | ✅ Yes | lines 254, 312 |
| Guard derivations while `data` is loading | ✅ Yes | `data?.purchases?.reduce(...) ?? 0` (line 243); header renders only when `data` present |

## TDD Compliance (strict-tdd-verify.md)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ✅ | TDD Cycle Evidence table in apply-progress (Engram `sdd/admin-purchases-net-amount/apply-progress`, obs #659) |
| All tasks have tests | ✅ | 8/8 tasks; test-bearing tasks 1.1/1.2 cover all 5 scenarios; RED/GREEN runs recorded per task |
| RED confirmed (tests exist) | ✅ | test file exists (18 tests); RED run recorded as 2 failed / 16 passed before implementation |
| GREEN confirmed (tests pass) | ✅ | focused 18/18 pass on independent execution (this verify run) |
| Triangulation adequate | ✅ | row behavior: 3 distinct cases (partial/full/non-refunded) vs spec scenarios 1–3; header: 2 cases (150 verbatim, 350 post-refetch) vs scenarios 4–5 |
| Safety Net for modified files | ✅ | 16/16 pre-existing focused tests at base (test file modified; safety net present) |

**TDD Compliance**: 6/6 checks passed

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 0 | 0 | — |
| Integration | 18 | 1 (`AdminPurchases.test.jsx`) | @testing-library/react, user-event, vitest |
| E2E | 0 | 0 | — |
| **Total** | **18** | **1** | |

All 5 spec scenarios covered at the integration (component) layer — appropriate for a display-only JSX change.

## Changed File Coverage

Coverage analysis skipped — no coverage tool detected (`@vitest/coverage-*` provider not installed). Informational, not a failure.

## Assertion Quality

✅ All assertions verify real behavior — no tautologies, no orphan empty checks, no type-only assertions alone, no ghost loops (row-scoped `within()` assertions over `.find()` results would throw if a row were missing, so they cannot silently pass), no smoke-only renders. Mock/assertion ratio healthy (~5 `vi.mock` vs ~60 expectations).

## Quality Metrics

**Linter**: ✅ No errors, no warnings (`npx eslint` on the 2 changed files, exit 0)
**Type Checker**: ➖ Not available (Vite project; `npm run build` exit 0 covers bundling)
**Coverage**: ➖ Not available

## Task coverage (8/8 checked — verified against code)

- Phase 1 RED (1.1 row net-amount tests ×3 cases; 1.2 header Total/Reembolsado/Neto + preserved `/reembolsado` fragments; 1.3 RED observed) — assertions present at test lines 463–541; apply-progress records the RED run (2 failed / 16 passed).
- Phase 2 GREEN (2.1 header reductions + render at lines 243–244, 254; 2.2 conditional Monto cell at line 312 with no mutation; 2.3 focused 18/18 + full suite baseline-only; 2.4 commit `0bd37d4` present on `feat/dynamic-refund-amount`).
- Phase 3 final verification (3.1 full suite 492/3 — exactly the 3 pre-existing baselines, zero new failures).

## Non-goal checks — all hold

1. **No backend/API/DB changes**: `git diff 0bd37d4^..0bd37d4 --name-only` = `frontend/src/pages/AdminPurchases.jsx` + `frontend/src/pages/AdminPurchases.test.jsx` only.
2. **`purchase.amount` not mutated**: no assignment in the file; only reads (lines 51, 115, 243, 312).
3. **Refund dialog semantics untouched**: `unitPriceCents`/`capCents` (lines 51–52) and the `{ quantity, amount }` POST body (line 217) byte-identical pre/post change.
4. **No per-row amount-breakdown variant, no per-purchase badge enrichment**: badges unchanged (`refundBadge`, lines 37–43).
5. **No OrganizerDashboard/MetricsService work**: no files outside `AdminPurchases.*` changed.

## Findings by severity

- **CRITICAL**: none.
- **WARNING**: none.
- **SUGGESTION 1** (informational, no action): the header scenario's illustrative numbers (Total 500 / Neto 350) differ from the test fixture (Total 350 / Neto 200); the test verifies the identical derivation formula (X = Σ amount, Y = `totalRefunded` verbatim, Z = X − Y) with a different fixture, so scenario intent is fully covered. No action required.

## Conclusion

The implementation matches the delta spec (APR-016), the design, and all 8 tasks. All 5 delta scenarios map to green, observed tests; all non-goals hold; the focused change-scope suite and the frontend build exit 0; the full suite reproduces exactly the 3 documented pre-existing baselines with zero new failures. **Status: PASS — ready for archive.**