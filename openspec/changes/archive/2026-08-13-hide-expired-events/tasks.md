# Tasks: Hide expired events from buyers + block purchase

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~700–800 (300p/400t/35FE) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

- **Unit 1 (PR1)** — Clock + `IsExpired` + exception + flag. Test: `dotnet test --filter "Event_IsExpired|Flag_"`; harness: boot, fail-fast; rollback: revert clock/flag.
- **Unit 2 (PR2)** — Catalog filter + `/manage` actions. Test: `dotnet test --filter "EventServiceTests|EventControllerTests"`; harness: WAF 404/200/200; rollback: revert EventService/Controller.
- **Unit 3 (PR3)** — Purchase guards + catches. Test: `dotnet test --filter "Reservation|Payment"`; harness: WAF 409 problem+json; rollback: revert guard/catch.
- **Unit 4 (PR4)** — FE hooks + swaps. Test: N/A (no FE runner, manual); harness: browser; rollback: revert hooks/swaps.

## Phase 1: Foundation & Domain

- [x] 1.1 RED: `Event_IsExpired_Future_False`/`_Past_True`/`_ExactInstant_False` (EHE-001, strict `<`)
- [x] 1.2 GREEN: `IsExpired(asOf) => Date < asOf` on `backend/Models/Event.cs`
- [x] 1.3 Create `backend/Models/EventExpiredException.cs` ("Event has already started")
- [x] 1.4 RED: `Flag_MissingSection_FailsFast`/`Flag_DefaultTrue` (EHE-009)
- [x] 1.5 GREEN: `HideExpiredEventsOptions.cs`; `Program.cs` fail-fast + `AddSingleton(TimeProvider.System)`; appsettings `Enabled:true`; `Microsoft.Extensions.Time.Testing` test-project only
- [x] 1.6 GREEN: inject `TimeProvider`; migrate UtcNow → `_clock` — EventService 61,80,183,322,403,419,445,641 (W2); ReservationService 93,201,219,276,377,425,468; PaymentService 77,102 (NOT 252+, EHE-011)
- [x] 1.7 RED: `CreateEvent_PastDate_FrozenClock_AnyException`/`_FutureDate_Succeeds` (ADR-3)

## Phase 2: Catalog Filtering (EHE-002/003/006/007)

- [x] 2.1 RED: `GetAllPublished_FlagEnabled_ExcludesExpired`/`_AllExpired_Empty`/`_MixOrderIndependent`
- [x] 2.2 RED: `GetEventById_Public_Expired_Null`/`_Active_200`/`_SameDayAfterStart_404`/`_ManagementIncludeExpired_200`
- [x] 2.3 GREEN: `IEventService`: `GetEventByIdAsync(Guid,bool includeExpired=false)`/`GetAllPublishedEventsAsync(bool)`; `.Where(e => e.Date > _clock.GetUtcNow())` when `Enabled && !includeExpired` (never `e.IsExpired` in IQueryable)
- [x] 2.4 GREEN: `EventController` add `[HttpGet("manage")]` (RequireStaffRole) + `[HttpGet("{id:guid}/manage")]` (EventOwnership)
- [x] 2.5 GREEN (CRITICAL): `UpdateEvent` l.96 + `UploadEventImage` l.175 → `includeExpired:true`; `CreateEvent` l.63 default; DeleteEvent: NO change (W1)
- [x] 2.6 RED/GREEN: `Organizer_ManagementEvent_Expired_200`/`Staff_ManagementList_IncludesExpired`/`_Anon_401` + route test `GET /api/events/manage`

## Phase 3: Purchase Guards (EHE-004/005/011)

- [x] 3.1 RED: `CreateReservation_Expired_409`/`_Active_201`/`_Race_13_59_to_14_01_409`/`_FlagDisabled_Succeeds`; no row persisted
- [x] 3.2 GREEN: `CreateReservationTransactionalAsync`: `.Include(t => t.Event)` (3 providers; smoke-test Npgsql `FromSqlInterpolated`+Include, else `FindAsync` fallback); null Event → `KeyNotFoundException`; guard `Enabled && IsExpired(_clock)` BEFORE stock check
- [x] 3.3 RED: `CreatePaymentPreference_Expired_Throws`/`_Active_Succeeds`/`_RaceAfterExpiry_Throws`/`ProcessApprovedPayment_ExpiredEvent_ProducesTickets`
- [x] 3.4 GREEN: same guard in `CreatePaymentPreferenceAsync` after active check; confirm Event `.Include`
- [x] 3.5 GREEN (catch-order): `catch (EventExpiredException) → Problem(409,"event-expired")` ABOVE generic catch — ReservationController l.105, PaymentController l.81
- [x] 3.6 RED+GREEN: `GlobalExceptionHandler` fallback sets `Type`/`Title` (ADR-5 option a)
- [x] 3.7 RED: `CreateReservation_EventLoadedViaInclude_SingleRoundTrip`

## Phase 4: Frontend (manual, EHE-006/007/010)

- [x] 4.1 Create `useManagementEvent.js` + `useManagementEvents.js`
- [x] 4.2 `OrganizerEventDetail.jsx`: `useEvent` → `useManagementEvent`
- [x] 4.3 `StaffScan.jsx`: `useEvents` → `useManagementEvents` (chooser); scan unchanged
- [x] 4.4 Optional: `EventDetail.jsx` "event expired" banner (decorative only)

## Phase 5: Verification & Cleanup

- [x] 5.1 Audit fixtures; seed `_clock.GetUtcNow().AddYears(1)`
- [x] 5.2 Full `dotnet test` from `backend/` incl. webhook regression (EHE-011)
- [x] 5.3 Flag for verify: `catalog-filtering/spec.md` l.27 title misleading — strict `<` → exact instant NOT expired; intentional, spec unedited
- [x] 5.4 Confirm `Enabled=false` → filters/guards no-op (runtime rollback)
