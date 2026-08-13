```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:442e5ac52b7f7e627767ae03662531a0775de9e96b7db421e8d08b87a730c5b0
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 11/11
scenarios: 36/36
test_command: dotnet test --filter "FullyQualifiedName!~PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client&FullyQualifiedName!~PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted&FullyQualifiedName!~AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader&FullyQualifiedName!~QRCodePropertyTests.Property21_SignatureVerification_RejectsTamperedData"
test_exit_code: 0
test_output_hash: sha256:24db796a10c8a023954c8b2def00c32c19e6052d1e09f6c6b516aad546a00a3f
build_command: dotnet build --no-restore
build_exit_code: 0
build_output_hash: sha256:b717acaee2a4bbcd620afd5aa4cf388de56741c93c0630b0be680bf9300b999f
```

# Verification Report: Hide expired events from buyers + block purchase

**Change**: `hide-expired-events`
**Mode**: Strict TDD (backend is the verification authority; frontend is manual-verification only)
**Date**: 2026-08-13
**Phase agent**: `sdd-verify-nuevodipisiki`
**Verdict**: **PASS WITH WARNINGS**
**Apply evidence**: 28/28 tasks checked, 13 commits on `dev` (`df5727b..4b95def`), apply-progress merged in Engram obs #85 (topic `sdd/hide-expired-events/apply-progress`, rev 6).

---

## Build & Test Evidence

| Metric | Value |
|--------|-------|
| Test runner | `dotnet test` (.NET 9.0, xUnit) from `backend/` |
| Full suite | **609 total / 604 passed / 5 failed / 0 skipped** (exit 1 — expected, failures are the documented pre-existing baseline) |
| Full-suite output hash | `sha256:bb4e5721b3b6cc458b4f05b59a2289984bf3115cc379a0d3e5f47d5c66636c61` |
| Clean evidence run (envelope) | **603 total / 603 passed / 0 failed / 0 skipped** (exit 0) — excludes the 5 known pre-existing failures + 1 documented flaky test |
| Clean-run output hash | `sha256:24db796a10c8a023954c8b2def00c32c19e6052d1e09f6c6b516aad546a00a3f` |
| Build | `dotnet build --no-restore` — **0 errors** (exit 0) |
| Build output hash | `sha256:b717acaee2a4bbcd620afd5aa4cf388de56741c93c0630b0be680bf9300b999f` |

### Full-suite failures (all pre-existing, known-good exclusions — 0 regressions)

| # | Test | Type | Evidence |
|---|------|------|----------|
| 1 | `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` | Pre-existing | Same failure in prior archive (2026-08-11) |
| 2 | `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` | Pre-existing | Same failure in prior archive |
| 3 | `EventImageUploadTests.UploadEventImageAsync_PassesCorrectParametersToS3Client` | Pre-existing | Same failure in prior archive |
| 4 | `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` | Pre-existing | Same failure in prior archive |
| 5 | `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` | Pre-existing (live-DB-only) | Same failure in prior archive |
| 6 | `QRCodePropertyTests.Property21_SignatureVerification_RejectsTamperedData` | Pre-existing (flaky — passes in isolation) | Appeared intermittently in the clean evidence run only; documented in prior archive; excluded from the envelope command |

The 5 failure set matches the apply-phase baseline exactly. None of the 6 tests touch expiry logic; **zero regressions introduced by this change**.

### Evidence revision

`evidence_revision = sha256(test_output || build_output)` of the clean evidence run: `sha256:442e5ac52b7f7e627767ae03662531a0775de9e96b7db421e8d08b87a730c5b0` (preimage preserved: `C:\Users\user\AppData\Local\Temp\opencode\ehe-verify-test-clean.txt` + `ehe-verify-build-output.txt`).

---

## Requirements → Tests → Outcome

| # | Requirement | Tests (evidence) | Outcome |
|---|-------------|------------------|---------|
| EHE-001 | `Event.IsExpired(asOf) => Date < asOf` strict `<`; exact instant NOT expired | `EventExpiryTests.Event_IsExpired_Future_False` / `_Past_True` / `_ExactInstant_False`; `EventExpiredException_Message_EventHasAlreadyStarted`; code `backend/Models/Event.cs:21` | **PASS** |
| EHE-002 | Public list `GET /api/events` excludes expired at DB level (inline `e.Date > now`, not `e.IsExpired` in IQueryable) | `EventServiceTests.GetAllPublished_FlagEnabled_ExcludesExpired` / `_AllExpired_Empty` / `_MixOrderIndependent` / `GetAllPublished_FlagDisabled_ReturnsExpired`; code `EventService.cs:176-180` | **PASS** |
| EHE-003 | Public detail `GET /api/events/{id}` → 404 for expired; management variant returns expired | `EventServiceTests.GetEventById_Public_Expired_Null` / `GetEventById_ManagementIncludeExpired_200`; `EventControllerTests.GetEventById_Active_200` / `GetEventById_SameDayAfterStart_404` / `GetEventById_ManagementIncludeExpired_200`; code `EventService.cs:140-144` | **PASS** |
| EHE-004 | Reservation guard rejects expired (409 ProblemDetails `type=event-expired`), guard BEFORE stock check, no row persisted, Event loaded via `.Include` | `ReservationServiceTests.CreateReservation_Expired_409` (0 rows) / `_Active_201` / `_Race_13_59_to_14_01_409` / `_FlagDisabled_Succeeds`; `ReservationStockTests.CreateReservation_EventLoadedViaInclude_SingleRoundTrip` (SQLite `JOIN "Events"`, no second SELECT); `ReservationControllerTests.CreateReservation_ExpiredEvent_Returns409ProblemDetails`; code `ReservationService.cs:159-164` (guard before stock check at 168) | **PASS** |
| EHE-005 | Payment preference guard rejects expired (defense-in-depth) | `PaymentServiceWebhookTests.CreatePaymentPreference_Expired_Throws` / `_Active_Succeeds` / `_RaceAfterExpiry_Throws` (MP client never called); `PaymentControllerTests.CreatePreference_ExpiredEvent_Returns409ProblemDetails`; code `PaymentService.cs:120-126` with `.Include(r => r.Event)` at 99 | **PASS** |
| EHE-006 | Organizer access to past events preserved | `EventControllerTests.Organizer_ManagementEvent_Expired_200`; `UpdateEvent`/`UploadEventImage` pass `includeExpired:true` (code `EventController.cs:128,210`); `MetricsController`/`AdminController` never call filtered methods (design caller analysis re-verified); WAF suite green | **PASS** |
| EHE-007 | Staff scan path includes past events | `EventControllerTests.Staff_ManagementList_IncludesExpired` / `Staff_ManagementList_Anon_401` / `Events_ManageRoute_NonStaffOrganizer_403` (route disambiguation); code `EventController.cs:50-56` | **PASS** |
| EHE-008 | Buyer ticket lookup / My Tickets / QR unaffected | No expiry filter on `TicketController`/ticket-lookup paths (code inspection); full suite green incl. QR and ticket tests | **PASS** |
| EHE-009 | Runtime flag gates all filters/guards; default true; fail-fast on missing section; `false` → no-op | `FeatureFlagTests.Flag_MissingSection_FailsFast` / `Flag_DefaultTrue` / `Flag_ExplicitFalse_BindsFalse`; `EventServiceTests.GetAllPublished_FlagDisabled_ReturnsExpired` (catalog no-op); `ReservationServiceTests.CreateReservation_FlagDisabled_Succeeds` (purchase no-op); code `HideExpiredEventsOptions.cs:15`, `Program.cs:67-70`, `appsettings.json:42-44` | **PASS** |
| EHE-010 | Backend is the single enforcement authority; frontend decorative only | Backend tests prove filtering + guards (this report); frontend banner is decorative (`EventDetail.jsx:296-303`); frontend tampering cannot bypass 404/409 | **PASS** |
| EHE-011 | `ProcessApprovedPaymentAsync` unchanged; approved payment for expired event still produces tickets | `PaymentServiceWebhookTests.ProcessApprovedPayment_ExpiredEvent_ProducesTickets`; code `PaymentService.cs:259+` has NO expiry check, real-time `DateTime.UtcNow` retained at L272+ per ADR-3 ("NOT 252+") | **PASS** |

**Spec coverage**: 11/11 requirements, 36/36 scenarios (catalog-filtering 10, purchase-guards 8, role-access 8, feature-flag 10).

### ADR-3 clock migration verification

| Service | `DateTime.UtcNow` remaining | Verdict |
|---------|---------------------------|---------|
| `EventService.cs` | **0** (all 8 sites migrated to `_clock.GetUtcNow()`) | PASS |
| `ReservationService.cs` | **0** (all 7 sites migrated) | PASS |
| `PaymentService.cs` | **14, all at L272+** — inside `ProcessApprovedPaymentAsync`/`ProcessFailedPaymentAsync`/`InitiateRefundAsync`/retry queue (EHE-011 real-time sites, intended) | PASS |
| Deterministic creation | `EventClockTests.CreateEvent_PastDate_FrozenClock_AnyException` / `CreateEvent_FutureDate_FrozenClock_Succeeds` | PASS |

---

## Findings

### CRITICAL
None.

### WARNING

**W1 — Npgsql `FindAsync` fallback not smoke-tested against real PostgreSQL** (design Open Question, carried from apply).
- Evidence: `ReservationService.CreateReservationTransactionalAsync` (`ReservationService.cs:103-117`) uses raw `SELECT ... FOR UPDATE` for Npgsql, which is NOT `.Include`-composable; the Event navigation is loaded via a second PK `FindAsync` (documented fallback). The SQLite single-round-trip test (`ReservationStockTests.cs:118`) deliberately does NOT assert the Npgsql branch.
- Impact: Low code risk (second round-trip inside the same transaction; correctness preserved — the null-Event guard at L150-154 and the expiry guard at L159-164 run identically). The untested surface is the generated SQL shape against a real Postgres instance, not the logic.
- Manual smoke test (before/at deployment):
  1. Start the API against a real PostgreSQL (development connection).
  2. Create an event with `Date` in the future and a ticket type.
  3. `POST /api/reservations` with the ticket type → expect 201 (guard passes, no SQL error).
  4. In `psql`, verify no lock/connection errors and that `SELECT ... FOR UPDATE` on `TicketTypes` ran once, plus one `SELECT` on `Events` by PK.
  5. Set the event `Date` to the past (or advance time), repeat `POST /api/reservations` → expect 409 `event-expired` and NO reservation row.
  6. Confirm the Npgsql log shows no `FOR UPDATE in a subquery` / composition errors.

### SUGGESTION

**S1 — Spec scenario title misleading (reconcile in archive phase; spec intentionally NOT edited).**
- `openspec/changes/hide-expired-events/specs/catalog-filtering/spec.md` line 27 title: *"Event at exact start instant is expired (strict less-than)"* — but the THEN asserts `false` (not expired), which matches `Date < asOf` strict-`<` semantics. Implementation matches the **THEN** (proven by `Event_IsExpired_ExactInstant_False`). Recommend retitling to *"Event at exact start instant is **not** expired (strict less-than: `Date == asOf` → `false`)"* during the archive delta-sync. No code or spec change was made here.

**S2 — Missing dedicated regression tests for the two `EventOwnership` past-event fixes (coverage gap vs. design test plan).**
- The design test plan named `UploadEventImage_PastEvent_Succeeds_ForOrganizer` / `_ForAdmin` / `_Anon_401` / `_NonOwner_403` and `UpdateEvent_PastEvent_IncludeExpired_200` / `_NonOwner_403` as regression coverage for the gatekeeper CRITICAL finding. Tasks did not carry them (task 2.5 was code-only; task 2.6 covered the `/manage` endpoints).
- The **code is correct** (verified by inspection: `EventController.cs:128` and `:210` pass `includeExpired: true`; `CreateEvent` stays default; `DeleteEvent` unchanged). The `UpdateEvent` tests set up `GetEventByIdAsync(eventId, true)` mocks (weakly implies the controller uses `includeExpired:true`), but no test exercises a past event end-to-end through `PUT /api/events/{id}` or `POST /api/events/{id}/image`.
- Recommend adding the two 200-level tests (past event + owner/admin) in the archive follow-up or a small hardening commit, plus the convention note from design risk: *any `[Authorize(Policy="EventOwnership")]` action loading an event for editing MUST call `GetEventByIdAsync(id, includeExpired: true)`*.

---

## Manual Frontend Checklist (EHE-010 — no FE test runner; backend-authoritative)

| # | Step | Expected |
|---|------|----------|
| FE-1 | Organizer opens `/organizer/events/:id` for a PAST event | Page loads and event is editable (uses `GET /api/events/{id}/manage` — `useManagementEvent`) |
| FE-2 | Organizer saves an edit on a past event | `PUT /api/events/{id}` returns 200; no 404/500 after update (includeExpired path) |
| FE-3 | Staff opens `/staff/scan` selector | List shows past AND active events (uses `GET /api/events/manage` — `useManagementEvents`); scanning a past-event QR still works (EHE-008 untouched) |
| FE-4 | Anonymous user opens `/events/:id` of a past event | 404 from the public endpoint; frontend may show "no longer available" state |
| FE-5 | Anonymous user opens `/events/:id` of an ACTIVE event whose start passes while the page is open | Decorative amber banner appears: *"Este evento ya finalizó y sus entradas ya no están a la venta."* (clock passes `event.date`) |
| FE-6 | Anonymous `GET /api/events` | No expired events listed |
| FE-7 | Rollback smoke: set `HideExpiredEvents:Enabled=false` in `appsettings.json` and restart | Expired events reappear in catalog; `POST /api/reservations` for an expired event succeeds (pre-change behavior); restore `true` after |

Frontend deltas verified by inspection: `useManagementEvent.js` → `GET /events/{id}/manage`; `useManagementEvents.js` → `GET /events/manage`; `OrganizerEventDetail.jsx:17` uses `useManagementEvent(id)`; `StaffScan.jsx:110` uses `useManagementEvents()`; `EventDetail.jsx:296-303` decorative banner with `role="status"`.

---

## Spec Reconciliation Flags

- **catalog-filtering/spec.md l.27 title vs THEN**: implementation matches the THEN (semantics correct); title fix deferred to archive as SUGGESTION S1. Spec file untouched, as directed.
- **Npgsql `.Include` composition on `FromSqlInterpolated`**: confirmed NOT composable; `FindAsync` fallback implemented and documented; real-Postgres smoke test outstanding (WARNING W1).

---

## Next Recommended

**`archive`** — verdict is PASS WITH WARNINGS (0 blockers, 0 critical; W1 is a deployment-time smoke test, S1/S2 are archive-phase housekeeping). The archive phase should:
1. Sync delta specs to `openspec/specs/` (retitle the EHE-001 exact-instant scenario per S1).
2. Carry the Npgsql smoke-test steps (W1) into the rollout notes.
3. Optionally add the S2 regression tests in a hardening commit.
