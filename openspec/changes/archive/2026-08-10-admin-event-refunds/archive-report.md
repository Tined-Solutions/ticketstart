# Archive Report: admin-event-refunds

**Change**: admin-event-refunds
**Archived**: 2026-08-10
**Archive path**: `openspec/changes/archive/2026-08-10-admin-event-refunds/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram)
**Capability**: `admin-purchase-refunds` (new)
**Main spec**: `openspec/specs/admin-purchase-refunds/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. 25/25 implementation tasks complete. All 11 requirements (APR-001..011) and all 17 scenarios COMPLIANT with green covering tests. Zero new test failures in either suite. 46 new tests green (38 backend + 8 frontend).

**Final test evidence (at close):**
- Backend: **527 passed / 6 unique pre-existing failures** / 533 total. The 6 are pre-existing baseline accepted by the owner; `ConfigValidationTests.Startup_*`, `QRCodePropertyTests.Property21`, and `AdminUserCreationIntegrationTests.PostAdminUsers_WithInvalidEmail` are flaky (green in isolation). Zero failures attributable to this change.
- Frontend: **400 passed / 26 pre-existing failures** / 426 total. Baseline accepted: StaffScan 22, Checkout 2, OrganizerEventDetail 1, identityValidation 1. Zero new.
- `dotnet build`: 0 errors, exit 0.

These figures are the orchestrator's final-state facts (launch prompt, most recent account) and corroborate the persisted `verify-report` (Engram #438, written 2026-08-10 17:11). No contradiction between sources.

**Verify verdict note (historical):** the persisted `verify-report` snapshot carries machine verdict `fail` with `blockers: 1` — a strict-TDD command-exit gate triggered by the pre-existing baseline-red full suite, NOT a change defect. `critical_findings: 0`, and the report itself states: "Blockers for archive: none from change scope." The orchestrator acknowledged the baseline-red suite and the DB-history environment item and launched archive. Per the Final-State Authority hierarchy, this snapshot claim ("persistable but not archive-ready") is superseded by the orchestrator's archive decision and final-state facts; the archive proceeded under ordinary repository policy with `reviewGate` structurally absent (no review artifacts exist for this candidate).

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | `reviewGate` absent; no `sdd/admin-event-refunds/review/*` topics in Engram, no receipt files. Kill-switch path not applicable — no review artifacts discovered. |
| Task Completion | Pass | `tasks.md`: 25/25 `[x]`, 0 unchecked implementation tasks. |
| CRITICAL verification issues | None | `critical_findings: 0` in verify-report; blocker is machine-gate baseline debt, not change scope. |
| Action Context | Pass | No `actionContext.mode: workspace-planning`; no `allowedEditRoots` restriction. |

## Spec Sync

The delta spec (`specs/admin-purchase-refunds/spec.md`) is a **full spec** — it contains no `ADDED`/`MODIFIED`/`REMOVED`/`RENAMED` sections (new capability, matching the `admin-ticket-stock` convention where the change spec IS the main spec). The main spec `openspec/specs/admin-purchase-refunds/spec.md` already existed and is **byte-identical** to the change spec (md5 `01c0ec24807a27af5c879beeb9745135` on both; `diff -r` exit 0, empty output). Merge outcome: 11 requirements, 17 scenarios present in main spec; no requirements added/modified/removed at archive time (sync was complete at spec phase).

Verbatim `diff -r` readbacks (mandatory mechanical copy contract):

```
=== VERBATIM diff -r OUTPUT (spec sync readback: change delta vs main spec) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

## Archive Move

`openspec/changes/admin-event-refunds/` → `openspec/changes/archive/2026-08-10-admin-event-refunds/` via `git mv` (fallback `mv`). Recursive pre-move snapshot compared against the archived tree; `archive-report.md` is additive and excluded (did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff -r exit 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `explore.md`, `spec.md`, `specs/admin-purchase-refunds/spec.md`, `design.md`, `tasks.md` (25/25), `apply-progress.md`, `verify-report.md` + this report. Active `openspec/changes/` no longer contains the change. The change folder was preserved (moved, not deleted) per the repo convention; no `state.yaml` exists in this repo's change folders (checked all changes) and there is no `openspec/README.md`, so the archive move itself is the status closure.

## Decisions & Deviations (final, from verify-report / apply-progress)

1. Simple mask for buyer email/DNI (`j***@gmail.com`, `3****1`) — NOT `LogRedactor.HashIdentifier` (proposal says masked, not hashed). Resolved in tasks.
2. `totalRefunded` = Σ `Transaction.Amount` where `Status=Refunded` (flip-consistent; never sums ticket prices).
3. `LookupTicketsByEmailAsync` excludes refunded tickets (matches sold-count semantics, APR-005).
4. `AdminPurchaseRow.LinkUnverified` (9th member) — additive, required by APR-009 "listing shows the purchase's tickets unverified"; rendered as "Vínculo no verificado" badge.
5. `Purchures` → `Purchases` typo fix in the DTO.
6. Backfill implemented as testable C# static (`TicketReservationBackfill.RunAsync`) invoked from migration `Up()` with guarded try/catch (NULL leftovers accepted). `has-pending-model-changes` confirms model/snapshot sync.
7. Backfill assigns only FULL chunks (count == `Reservation.Quantity`); partial/overflow stay NULL.
8. Listing quantity = `Reservation.Quantity`; amount/date from Approved/Refunded tx (fallback reservation).

**APR-008 invariant confirmed**: `PaymentService.InitiateRefundAsync` untouched — `git diff` shows 0 changed lines in `backend/Services/PaymentService.cs`; Mercado Pago / email / motivo appear only in constraint comments; no MP call, no refund email, no motivo in the new admin path.

## Deploy-Time Warning (environment item — NOT a change defect)

**The shared Supabase migration history is stale**: earlier pending migrations (`AddPendingEmailSend`, `DropCurrentlyReserved`) are missing from the shared history, so `dotnet ef database update` fails before reaching this change's migration `20260810120000_AddTicketReservationAndRefund`. The migration itself is proven correct: `dotnet ef migrations has-pending-model-changes` → no model drift, plus 7 dedicated backfill tests green. **Required before deploy**: realign the Supabase migration history (apply or reconcile the missing pending migrations) so the new migration can apply. This is tracked as a pre-existing environment item, not a defect of this change (per apply-progress and verify-report WARNING 2).

## Engram Traceability

Observations read for this archive (Engram, project `ticketstart`):

| ID | Artifact | Read |
|----|----------|------|
| #431 | `sdd/admin-event-refunds/explore` | filesystem copy (explore.md) + search preview |
| #432 | `sdd/admin-event-refunds/proposal` | filesystem copy (proposal.md) + search preview |
| #433 | `sdd/admin-event-refunds/spec` | filesystem copy (spec.md) + search preview |
| #435 | `sdd/admin-event-refunds/design` | filesystem copy (design.md) + search preview |
| #436 | `sdd/admin-event-refunds/tasks` | filesystem copy (tasks.md, full) + search preview |
| #437 | `sdd/admin-event-refunds/apply-progress` | full content (mem_get_observation) + filesystem copy |
| #438 | `sdd/admin-event-refunds/verify-report` | full content (mem_get_observation) + filesystem copy |

(No review topics exist; `#434` is an unrelated discovery observation about design-phase retries.)

## Rollback (post-archive, if ever needed)

Per proposal/design: migration Down drops columns/FK/index; remove the two endpoints + `IAdminPurchaseService`; restore the 4 sold-count filters; flip `Refunded` tx rows back to `Approved` (one SQL); reset `IsRefunded=false`; remove frontend route/button/page; keep audit rows. No data loss beyond the refund state itself.
