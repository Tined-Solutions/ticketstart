# Apply Progress: admin-event-refunds

**Status**: `success` — 25/25 tasks complete, ready for `sdd-verify`
**Mode**: Strict TDD (backend) / TDD-ready (frontend)
**Delivery**: single-pr with maintainer-approved `size:exception` (≤ 4000 lines) — no chained PRs

## Baseline (pre-existing failures — NOT chased)

- Backend: 7 pre-existing failures (`PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PendingEmailRetryTests` x2, `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client`, `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`, `ConfigValidationTests.Startup_*` flaky). Baseline 488 passed / 7 failed (495 total).
- Frontend: 26 pre-existing failures (StaffScan 22, Checkout 2, OrganizerEventDetail 1, identityValidation 1). Baseline 392 passed / 26 failed (418 total).
- Result: **zero NEW failures** in every run; all new tests green.

## Task Status by Phase

| Phase | Tasks | Status |
|-------|-------|--------|
| 1 Model + Migration + Backfill | 1.1–1.6 | ✅ complete |
| 2 IAdminPurchaseService | 2.1–2.3 | ✅ complete |
| 3 Ticket-state consumers | 3.1–3.6 | ✅ complete |
| 4 Controller endpoints | 4.1–4.3 | ✅ complete |
| 5 Frontend "Compras" | 5.1–5.4 | ✅ complete |
| 6 Verification | 6.1–6.3 | ✅ complete |

All checkboxes marked `[x]` in `openspec/changes/admin-event-refunds/tasks.md`.

## Tests Run (final)

- Backend: `dotnet build` (0 errors) + `dotnet test` → 526 passed / 6 unique pre-existing failures (533 total; count varies 6–7 due to pre-existing flakiness). **38 new tests, all green**.
- Frontend: `npx vitest run` → 400 passed / 26 pre-existing failures (426 total). **8 new tests, all green**.

## TDD Cycle Evidence (Strict TDD — hard gate)

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|------|-----------|-------|------------|-----|-------|-------------|----------|
| 1.1 | `backend/Tests/TicketReservationBackfillTests.cs` | Unit (InMemory) | ✅ 488/495 baseline | ✅ Written (compile-fail) | ✅ 7/7 | ✅ 7 cases (full chunk, partial→NULL, multi-res, no-res, pre-linked, overflow, multi-key) | ✅ Clean |
| 1.2 | — (build) | N/A | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 1.3 | — (build) | N/A | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 1.4 | — (build) | N/A | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 1.5 | `TicketReservationBackfillTests` (GREEN) | Unit | ✅ | ✅ Written | ✅ 7/7 | see 1.1 | ✅ Clean |
| 1.6 | `dotnet ef migrations has-pending-model-changes` | Tool | ✅ | N/A | ✅ No model drift | ➖ Single | ➖ None needed |
| 2.1 | `backend/Tests/AdminPurchaseServiceTests.cs` | Unit (InMemory) | ✅ | ✅ Written (compile-fail) | ✅ 12/12 | ✅ 12 cases (happy, no-tx, IsUsed, race arm, already-refunded, 404, listing mask, totalRefunded, empty, link-unverified x2) | ✅ Clean |
| 2.2 | `AdminPurchaseServiceTests` (GREEN) | Unit | ✅ | ✅ Written | ✅ 12/12 | see 2.1 | ✅ Clean |
| 2.3 | build + suite | DI | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 3.1 | `TicketServiceTests`, `MetricsConsolidationTests`, `MetricsPropertyTests`, `EventServiceTicketStockTests`, `ReservationServiceTests` | Unit | ✅ | ✅ 9/9 failed | ✅ 9/9 | ✅ 9 cases (QR refunded, 4 sold-count sites, 2 lookups, resend, ReservationId link) | ✅ Clean |
| 3.2 | build | DTO | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 3.3 | TicketServiceTests (GREEN) | Unit | ✅ | ✅ | ✅ 9/9 | see 3.1 | ✅ Clean |
| 3.4 | EventServiceTicketStockTests (GREEN) | Unit | ✅ | ✅ | ✅ 9/9 | see 3.1 | ✅ Clean |
| 3.5 | ReservationServiceTests (GREEN) | Unit | ✅ | ✅ | ✅ 9/9 | see 3.1 | ✅ Clean |
| 3.6 | Metrics tests (GREEN) | Unit | ✅ | ✅ | ✅ 9/9 | see 3.1 | ✅ Clean |
| 4.1 | `backend/Tests/AdminControllerPurchaseTests.cs` | Unit (Moq) | ✅ | ✅ Written (compile-fail) | ✅ 10/10 | ✅ 10 cases (policy attr, 401 x2, happy list, 404, audit w/o motivo, 409, 404 refund, no-MP path, 500) | ✅ Clean |
| 4.2 | `AdminControllerPurchaseTests` (GREEN) | Unit | ✅ | ✅ | ✅ 10/10 | see 4.1 | ✅ Clean |
| 4.3 | build + suite | DTO map | ✅ | N/A | ✅ build | ➖ Single | ➖ None needed |
| 5.1 | `frontend/src/pages/AdminPurchases.test.jsx` + `AdminPanel.test.jsx` | Vitest + RTL | ✅ 392/418 baseline | ✅ Written | ✅ 8/8 | ✅ 8 cases (mask+badge, empty, error, refund success→invalidate, failure→unchanged, non-admin denied, admin allowed, panel navigate) | ✅ Clean |
| 5.2 | AdminPurchases.test.jsx (route) | Integration | ✅ | ✅ | ✅ 8/8 | see 5.1 | ➖ None needed |
| 5.3 | AdminPanel.test.jsx | Unit | ✅ | ✅ | ✅ 8/8 | see 5.1 | ➖ None needed |
| 5.4 | AdminPurchases.test.jsx (page) | Integration | ✅ | ✅ | ✅ 8/8 | see 5.1 | ✅ Clean |
| 6.1 | full `dotnet test` | Suite | ✅ | N/A | ✅ 526 passed | N/A | N/A |
| 6.2 | full `npm test` | Suite | ✅ | N/A | ✅ 400 passed | N/A | N/A |
| 6.3 | grep + `git diff` | Static | ✅ | N/A | ✅ clean | N/A | N/A |

- **Total tests written**: 46 (38 backend + 8 frontend) — all passing.
- **Layers used**: Unit (30), Integration (2 frontend route/page), Tool-verified migration (1), Suite (2).
- **Approval tests**: none needed (no behavior-preserving refactor; changes were additive filters + new branches).
- **Pure functions created**: 2 (`TicketReservationBackfill.RunAsync`, masking helpers in `AdminPurchaseService`).

## Work Unit Evidence (single PR — size:exception)

| Evidence | Value |
|---|---|
| Focused test command + exact result | `dotnet test --filter "FullyQualifiedName~TicketReservationBackfillTests"` → 7/7 pass; `...~AdminPurchaseServiceTests` → 12/12; `...~RefundedTickets\|EntradaReembolsada\|ReservationId` → 9/9; `...~AdminControllerPurchaseTests` → 10/10; `npx vitest run src/pages/AdminPurchases.test.jsx src/pages/AdminPanel.test.jsx` → 33/33 |
| Runtime harness command/scenario | `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration" (migration == model). Live `dotnet ef database update` attempted → BLOCKED by pre-existing DB-history drift (earlier pending migration `AddPendingEmailSend` fails on `column "status" does not exist`); not caused by this change. Local API walk-through of POST refund endpoint is the verify-phase runtime path |
| Rollback boundary | `backend/Migrations/20260810120000_AddTicketReservationAndRefund.Down()` (drop columns/FK/index); remove 2 endpoints + service registration; restore 4 sold-count queries (`!t.IsRefunded` removal); flip `Refunded` tx rows back to `Approved` (one SQL); reset `IsRefunded=false`; remove frontend route/button/page; audit rows kept |

## Deviations from Design

1. **`AdminPurchaseRow` gains a `LinkUnverified` field** (9th record member). The design's interface sketch omits it, but spec APR-009's scenario requires the listing to surface ambiguous legacy backfill leftovers ("the listing shows the purchase's tickets unverified"); without the field the scenario is unverifiable. Additive only — the frontend renders a "Vínculo no verificado" warning badge.
2. **Design typo fixed**: `Purchures` → `Purchases` in `AdminPurchasesResponse`.
3. **Migration backfill is a testable C# static (`TicketReservationBackfill.RunAsync`) invoked from the migration `Up()`** (guarded try/catch → NULL leftovers are the accepted APR-009 state, so a factory/config failure never blocks the schema migration), rather than raw provider-specific SQL. This is what makes task 1.1's "chunked assignment + NULL leftovers proven" actually testable; `has-pending-model-changes` confirms the hand-written migration + snapshot exactly match the model.
4. **Backfill ambiguity rule**: chunk assigned only when FULL (count == reservation.Quantity); a partial final chunk and any overflow stay NULL (proven by 3 dedicated tests).
5. **`GetPurchasesAsync` quantity = `Reservation.Quantity`** (purchase ground truth) with `LinkUnverified` carrying the backfill provenance; amount/date come from the Approved/Refunded transaction (fallback to reservation for defensive no-tx rows).

## Issues Found

- Live Supabase migration history is stale (missing `AddPendingEmailSend` / `DropCurrentlyReserved`, both pre-existing changes). `dotnet ef database update` therefore fails BEFORE reaching this change's migration. Not chased; environment owner must bring the DB history current (apply the two pending pre-existing migrations, or realign) before `20260810120000_AddTicketReservationAndRefund` can apply. The migration itself is proven correct via model-snapshot sync + 7 behavioral tests.
- One test expectation bug in my own RED suite (partial-chunk semantics) — fixed the test, not the implementation.

## Remaining

None. All 25 tasks complete. Next phase: `sdd-verify`.

## Workload / PR Boundary

- Mode: single PR, `size:exception` (pre-approved ≤ 4000 lines; actual ~1,500 authored)
- Boundary: whole change (25 tasks, 6 phases) in one batch
- Review budget impact: within the pre-approved exception; no chained PRs
