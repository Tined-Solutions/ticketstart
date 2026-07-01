# Exploration: ticketera-online State Characterization

## Current State

The `ticketera-online` change has a well-defined proposal, design, specs (7 domains), and task plan (32 top-level tasks, 52 waves). Backend implementation covers the first ~11 tasks (auth, events, reservations, QR/tickets, expiration service) with 188/189 tests passing. The remaining backend tasks (payments, email, metrics, admin, error handling) and ALL frontend tasks are not started.

**Key discrepancy:** `tasks.md` marks sub-tasks 9.3 (reservation property tests) and 11.5 (QR code property tests) as unchecked `[ ]`, but the corresponding test files (`ReservationPropertyTests.cs` — 1379 lines, `QRCodePropertyTests.cs` — 1125 lines) exist and all tests pass. The checkboxes are stale.

## Task Inventory

### DONE (code exists, tests pass)

| Task | Description | Evidence |
|------|-------------|----------|
| 1 | Monorepo scaffolding | `/backend/`, `/frontend/`, `TicketeraOnline.sln` exist |
| 2.1 | NuGet packages | EF Core, JWT, AWSSDK.S3, BCrypt, FsCheck all referenced |
| 2.2 | Database connection | `ApplicationDbContext.cs`, `ApplicationDbContextFactory.cs`, connection pooling config |
| 2.3 | JWT authentication | `Program.cs` lines 48-69, `AuthController.cs` |
| 2.4 | Cloudflare R2 storage | `Program.cs` lines 94-109, `IAmazonS3` registered |
| 2.5 | External services config | `appsettings.json` (Mercado Pago, Resend, HMAC key configured) |
| 3.1-3.6 | All data models | `Models/User.cs`, `Event.cs`, `TicketType.cs`, `Reservation.cs`, `Ticket.cs`, `Transaction.cs` |
| 3.7 | DbContext + relationships | `Data/ApplicationDbContext.cs` |
| 4.1-4.2 | Migrations | `Migrations/20260528003801_InitialCreate.cs` |
| 5.1-5.2 | Auth service + controller | `Services/AuthService.cs`, `Controllers/AuthController.cs` |
| 5.3-5.4 | Auth property tests | `Tests/AuthenticationPropertyTests.cs` — Properties 1-4 (10 tests) |
| 6.1-6.2 | Authorization middleware | `Authorization/EventOwnershipHandler.cs`, `EventOwnershipRequirement.cs`, policies in `Program.cs` |
| 7.1-7.4 | Event service + controller + images | `Services/EventService.cs`, `Controllers/EventController.cs`, R2 upload/delete |
| 7.5-7.6 | Event property tests | `Tests/EventManagementPropertyTests.cs` — Properties 5,6,30-32; `Tests/ImageStoragePropertyTests.cs` — Properties 7-9 |
| 8 | Checkpoint | Marked [x] in tasks.md |
| 9.1 | Reservation service | `Services/ReservationService.cs` with concurrency control |
| 9.2 | Reservation controller | `Controllers/ReservationController.cs` |
| **9.3** | **Reservation property tests** | **`Tests/ReservationPropertyTests.cs` — Properties 10-13, 41 (13 tests). STALE CHECKBOX — marked [ ] but DONE.** |
| 10.1 | Expiration background service | `Services/ReservationExpirationService.cs`, registered in `Program.cs` line 22 |
| 10.2 | Expiration service tests | `Tests/ReservationExpirationServiceTests.cs` (6 tests) |
| 11.1-11.4 | QR + ticket service + controller | `Services/TicketService.cs`, `Controllers/TicketController.cs` |
| **11.5** | **QR code property tests** | **`Tests/QRCodePropertyTests.cs` — Properties 18-21, 27-29 (13 tests). STALE CHECKBOX — marked [ ] but DONE.** |
| 11.6 | Ticket lookup property tests | `Tests/TicketLookupPropertyTests.cs` — Property 26 (7 tests) |

### NOT STARTED (no code exists)

| Task | Description | Missing Artifacts |
|------|-------------|-------------------|
| 12.1-12.4 | Payment service (Mercado Pago) | No `IPaymentService`, `PaymentService`, `PaymentController` |
| 12.5 | Payment property tests | Properties 14-17, 38-39 not written |
| 13 | Checkpoint | Blocked by Task 12 |
| 14.1-14.2 | Email service (Resend) | No `IEmailService`, `EmailService`, email templates |
| 14.3 | Email property tests | Properties 22-25, 40 not written |
| 15.1-15.2 | Metrics service | No `IMetricsService`, `MetricsService`, `MetricsController` |
| 15.3 | Metrics property tests | Properties 33-37 not written |
| 16.1-16.2 | Admin endpoints + audit | No `AdminController`, no audit log entity |
| 16.3 | Admin property tests | Properties 42-43 not written |
| 17.1-17.2 | Global error handling + logging | No `GlobalExceptionHandler`, no structured logging config |
| 17.3 | Error handling property tests | Properties 44-51 not written |
| 18 | Checkpoint | Blocked by Tasks 14-17 |
| 19-28 | ALL frontend tasks | `frontend/src/` contains only Vite scaffold (`App.jsx` counter demo). No React Router, no Axios, no components, no auth context |
| 30-32 | Integration tests + docs | Not started |

### Test Suite Status

- **Total tests:** 189
- **Passing:** 188
- **Failing:** 1 (`VerifyDatabaseSchema.Database_Should_Have_All_Tables` — requires live Supabase PostgreSQL connection, expected infra-dependent failure)
- **Test files:** 14 files covering auth, events, images, reservations, reservation controller, expiration service, QR codes, ticket service, ticket lookup, database config, schema verification

### Frontend Status

- React 19 + Vite 8 scaffold only
- No React Router, Axios, html5-qrcode, or any app-specific dependencies
- `App.jsx` is the default Vite counter demo
- No testing framework configured (no vitest/jest)

## Approaches

1. **Continue apply from Task 12 (payments)** — Pick up where implementation left off. The next wave is payments (12.1-12.5), then email (14), metrics (15), admin (16), error handling (17).
   - Pros: Follows dependency graph, backend-first approach is sound, all prerequisites done
   - Cons: Large remaining scope (~20 backend tasks + 10 frontend tasks)
   - Effort: High

2. **Fix stale checkboxes first** — Update tasks.md to mark 9.3 and 11.5 as [x] before continuing.
   - Pros: Accurate tracking
   - Cons: Trivial, can be done alongside apply
   - Effort: Minimal

3. **Fix failing schema test first** — The `VerifyDatabaseSchema` test fails without a live DB. Consider making it skip gracefully or use a test fixture.
   - Pros: Clean test suite before adding more
   - Cons: Minor issue, doesn't block apply
   - Effort: Low

## Recommendation

**Run `sdd-apply` starting from Task 12 (payment service).** Before starting, fix the two stale checkboxes in tasks.md (9.3, 11.5 → [x]). The failing schema verification test is infrastructure-dependent and non-blocking.

Suggested apply batches (respecting 800-line review budget):
- **Batch 1:** Tasks 12.1-12.4 (payment service + webhook + refund + controller)
- **Batch 2:** Task 12.5 (payment property tests)
- **Batch 3:** Tasks 14.1-14.2 (email service + templates)
- **Batch 4:** Tasks 15.1-15.2 (metrics service + controller)
- **Batch 5:** Tasks 16.1-16.2 (admin + audit)
- **Batch 6:** Tasks 17.1-17.2 (error handling + logging)
- **Batch 7+:** Frontend tasks 19-28 (separate concern, no TDD requirement)

## Risks

- **Stale task tracking:** tasks.md checkboxes don't match code reality for 9.3 and 11.5. Future apply runs may misreport progress if not corrected.
- **1 failing test:** `VerifyDatabaseSchema` needs live DB — will fail in CI without Supabase credentials. Should be conditionally skipped.
- **No frontend testing:** Frontend has no test framework. The `strict_tdd: true` policy applies backend-only, but frontend quality will be unverified.
- **Large remaining scope:** ~20 backend tasks and ~10 frontend tasks remain. Each backend task involves service + controller + tests. Consider batching carefully.
- **WeatherForecast endpoint still in Program.cs:** Scaffold code (`/weatherforecast`) should be removed before production.
- **Design mentions net8.0 but project uses net9.0:** Design doc and proposal reference ASP.NET Core 8.0 / EF Core 8.0 but actual project targets net9.0. Non-blocking but documentation drift.

## Ready for Apply

**Yes.** The orchestrator should:
1. Fix stale checkboxes in `tasks.md` (9.3, 11.5 → `[x]`)
2. Launch `sdd-apply` for Task 12 (payment service, waves 19-21)
3. Consider fixing the `VerifyDatabaseSchema` test to skip gracefully when no DB is available
