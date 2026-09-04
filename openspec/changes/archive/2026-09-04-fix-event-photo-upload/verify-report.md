```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:40c934b16d5f3d1d991e99e3dc833f259a2f24343f548f35349d2f2ef3f6bf4d
verdict: pass_with_warnings
blockers: 0
critical_findings: 0
requirements: 8/8
scenarios: 31/31
test_command: dotnet test --filter "FullyQualifiedName!~PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized&FullyQualifiedName!~PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted&FullyQualifiedName!~AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader" (cwd backend/); npx vitest run --pool=forks --maxWorkers=1 (cwd frontend/)
test_exit_code: 0
test_output_hash: sha256:c42d6e19fff9fe34db8ff039016829698fb878765107697118f07596b78ed828
build_command: dotnet build (cwd backend/) exit 0; npx vite build (cwd frontend/) exit 0
build_exit_code: 0
build_output_hash: sha256:a9ad00ffdb606539a1ec56fd1d2f2b2c52a877a2388f0b5e9e10b67037563c65
```

# Verify Report: fix-event-photo-upload

Branch `fix/r2-upload-linux-tls` · HEAD `98cfb0a6d21c4c675b456b9f30a83f826450641b` (4 commits over `6f4fe27`, working tree clean) · Hybrid artifact store (OpenSpec + Engram) · STRICT TDD active.

## Verdict

**PASS WITH WARNINGS.** The implementation matches spec acceptance criteria EIM-001…007 and the PEM-002 delta (8 requirements / 31 scenarios), the design ADRs 1–7, and all 11 tasks marked `[x]` by apply. **No CRITICAL findings.** Two WARNINGs and three SUGGESTIONs are recorded below; none blocks archiving from a verification standpoint, but the pre-existing webhook-signature failures are security-relevant and must be tracked.

## Evidence (commands executed during verification)

Envelope command = the change-scope suite (full runs minus exactly the 4 pre-existing baseline tests, each excluded by full FQN in the filter — same convention as the admitted `dynamic-refund-amount` verification). Evidence preimage preserved at `C:\Users\user\AppData\Local\Temp\opencode\ts-verify-*.log`; `evidence_revision` = `cat ts-verify-backend-test-filtered.log ts-verify-frontend-test.log ts-verify-backend-build.log ts-verify-frontend-build.log | sha256sum`.

| Suite | Command (cwd) | Exit | Result | Output hash (SHA-256) |
|---|---|---|---|---|
| Backend tests (envelope, change-scope) | `dotnet test --filter "<4 baseline FQNs excluded>"` (backend) | 0 | **749 passed / 0 failed / 749** | `21D09C41…2B55` |
| Backend tests (full unfiltered) | `dotnet test` (backend) | 1 | 749 passed / 4 failed / 753 — failures pre-existing, see W-1 | `00DF4C32…FFB7A` |
| Frontend tests (envelope + full) | `npx vitest run --pool=forks --maxWorkers=1` (frontend; `npm test` needs WSL bash, unavailable on win32) | 0 | 48 files / 505 tests / 0 failures | `98206578…3171E2` |
| Backend build | `dotnet build` (backend) | 0 | 0 errors | `54DCAD82…261D9` |
| Frontend build | `npx vite build` (frontend) | 0 | built OK; only a >500 kB chunk warning (pre-existing) | `70BBD076…55F31` |

`test_output_hash` = sha256(concat filtered-backend + frontend test logs) = `c42d6e19…ed828`; `build_output_hash` = sha256(concat backend + frontend build logs) = `a9ad00ff…63c65`. Backend unfiltered totals match apply-progress exactly (`749/4/753`); frontend matches (`505/505`). No additional failures appeared.

## Requirements verification (against real code, not just tests)

### EIM-001 — OS-default TLS transport to R2 ✅
- `backend/Services/R2StorageClient.cs:40-46` — constructor comment documents the production evidence (`sslv3 alert handshake failure`, OpenSSL error `0A000410`, Linux/OpenSSL 3.x, OS defaults TLS 1.3 preferred) and the client is built as `new HttpClient(new SocketsHttpHandler())` with **no `EnabledSslProtocols` assignment** (scenarios 1–3).
- `backend/Tests/R2StorageClientTests.cs` — 2/2: reflection asserts `handler.SslOptions.EnabledSslProtocols == SslProtocols.None` and the handler type is `SocketsHttpHandler` (walks the `HttpMessageInvoker` hierarchy for the private `_handler` field).

### EIM-002 — Event-agnostic upload endpoint ✅ (8/8 scenarios)
- `backend/Controllers/UploadsController.cs:34-48` — `POST api/uploads/event-image`, `[Authorize(Policy = "RequireOrganizadorRole")]` (Organizador + Admin), `[EnableRateLimiting("EventImageUpload")]`, binds `IFormFile image` (field `image`, no event id anywhere), 200 `{ imageUrl }`; `ArgumentException` → 400 (invalid MIME / oversize, no R2 object), `InvalidOperationException` → 500.
- `backend/Program.cs:246-261` — `EventImageUpload` policy: fixed window, PermitLimit 10, 1 min, `RateLimitPartitioner.AuthenticatedOrIp`, QueueLimit 0.
- `backend/Services/EventService.cs:677-741` — `UploadEventImageAsync` validates MIME ∈ {jpeg,png,webp} (case-insensitive) and ≤ 5 MB, stores under `events/{guid}{ext}`, returns the public URL; no event row touched.
- `backend/Tests/UploadsControllerTests.cs` — 10/10: organizer 200, admin 200, 401, 403 staff, 400 missing CSRF, 400 `image/jpg`, 400 > 5 MB, 400 missing part, 429 on 11th call, plus EIM-006 404. `factory.R2Mock` proves "no object is uploaded" on every rejection.

### EIM-003 — Honest error surfacing ✅ (3/3)
- `frontend/src/components/EventForm.jsx:241-248` — feedback renders `role="alert"` for `type:'error'` (green `role="status"` only for success); `frontend/src/index.css:97-101` — `.feedback-message--error` is red (`#dc2626`). The old green false-success catch ("…but the image could not be uploaded") is gone (no such string in the file).
- Labels: `EventForm.jsx:512-518` — "Subiendo imagen…" / "Guardando…", button disabled while `submitting`.
- `EventForm.test.jsx:360-392` — upload failure renders red `role="alert"`, asserts `queryByText(/evento creado correctamente/i)` is absent; edit variant in `__tests__/EventForm.edit.test.jsx:127-153`.

### EIM-004 — Upload-first, save-blocking flow ✅ (5/5)
- `EventForm.jsx:141-188` — upload runs BEFORE the save; a failed upload jumps to the catch → red error, `POST /events` / `PUT /events/{id}` never called, no navigation. Create carries `imageUrl: uploadedUrl || ''`; edit carries `imageUrl: uploadedUrl ?? (initialData?.imageUrl || '')`. No photo → no upload call, payload identical to before.
- Tests: `EventForm.test.jsx:326-358` (upload-first order, `FormData` without manual Content-Type header), `360-392` (blocked save), `EventForm.edit.test.jsx:99-125` (PUT carries new URL), `155-174` (no-photo preserves `initialData.imageUrl`).
- Upload-OK / PUT-409 race: `EventService.cs:492-495` — `EnsureMutable` throws before the image mutation and before `SaveChanges`, and the cleanup block (`:537-546`) is unreachable on that path; `EventServiceImmutabilityTests.cs:216-238` proves no R2 delete on 409; orphan accepted per ADR-3 (no compensating delete in code).

### EIM-005 — Old-image cleanup in UpdateEventAsync ✅ (4/4)
- `EventService.cs:512-546` — `previousImageUrl` captured BEFORE the mutation (`:515`), best-effort `DeleteImageAsync` runs AFTER `SaveChanges` (`:527`) guarded by `request.ImageUrl != null && !string.IsNullOrWhiteSpace(previousImageUrl) && old != new (Ordinal)`; failure logs a warning and never fails the request. `null` (text-only) triggers nothing.
- `EventServiceTests.cs:484-824` — 6 new tests: omitted preserves, explicit `""` clears, replaced deletes old, same-URL re-send does NOT delete, cleared deletes, delete-failure → request still succeeds.
- `ImageStoragePropertyTests.cs:722-783` — FsCheck Property 10 `UpdateEvent_CleanupInvariant_DeleteCalledIffOldNonEmptyNewNonNullAndDifferent` (generator `R2ImageUrlArb`).

### EIM-006 — Removal of the old image endpoint ✅ (2/2)
- `EventController.cs` route table: only `GET /`, `GET /{id:guid}`, `GET manage`, `GET {id}/manage`, `POST` (create), `PUT {id}`, `DELETE {id}` — **no** `POST {id}/image`. `ReplaceEventImageAsync` has **0 matches** across `backend/` (interface, service, tests). `IEventService` exposes only `UploadEventImageAsync`.
- `UploadsControllerTests.cs:212-229` — `POST /api/events/{id}/image` → 404, no R2 call.

### EIM-007 — No organizer edit escalation ✅ (3/3)
- Structural: the upload route accepts no event id and the service path touches no event row — no ownership check exists to bypass. Attach still requires `POST /events` or `PUT /events/{id}`.
- `frontend/src/pages/OrganizerDashboard.jsx:29` — `canEdit = user?.role === 'Admin'` unchanged.
- `EventController.cs:118-167` — `PUT /events/{id}` keeps `EventOwnership` policy + service ownership check (`EventService.cs:485-490`) + `EnsureMutable` → 409 RFC 7807 (`type: "event-finalized"`, title "Event has already finished").

### PEM-002 — All six mutation endpoints reject past events (delta) ✅ (3/3)
- Guard placement: `EventService.cs:492-495` — `EnsureMutable` before every mutation and `SaveChanges`; unchanged in the other five endpoints.
- Coverage: `EventServiceImmutabilityTests.cs` PEM regions — PUT-with-new-image past event → `EventFinalizedException`, no save, no R2 delete (`:216-238`); DELETE organizer → 403 (`:179-195`, ED-001), DELETE admin → 409 (`:198-209`, ED-002); `AddTicketStockAsync_PastEvent…` (`:245-258`); approve/reject past → `AdminServiceTests.cs:82-137`. RFC 7807 shape proven by `EventController.UpdateEvent` catch → `Problem(…, 409, type "event-finalized")`.

## Design ADR coherence

| ADR | Decision | Status |
|---|---|---|
| 1 | New `UploadsController`, route `api/uploads` | ✅ `UploadsController.cs:18` |
| 2 | Reuse `IEventService.UploadEventImageAsync` | ✅ `UploadsController.cs:47` |
| 3 | Orphan on save-failure accepted | ✅ no compensating delete anywhere; PEM-003 holds |
| 4 | Cleanup AFTER `SaveChanges`, best-effort | ✅ `EventService.cs:531-546` |
| 5 | Role gate = `RequireOrganizadorRole` (Organizador+Admin) | ✅ policy at `Program.cs:156`, controller `:35` |
| 6 | `EventImageUpload` 10/min fixed window | ✅ `Program.cs:252-261`; per-IP runtime nuance documented (`:246-251`) — see W-2 |
| 7 | TLS: remove `SslOptions` assignment entirely | ✅ `R2StorageClient.cs:46` |

## Findings

### CRITICAL
None.

### WARNING

- **W-1 (4 pre-existing backend failures, security-relevant):** `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`, `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` fail identically to apply-progress and to the `6f4fe27` base (apply verified them in a base worktree with the real `appsettings.Development.json`). None of those test files is touched by this change (diffstat confirms). The two webhook-signature failures are security-relevant: `Webhook_InvalidSignature_ReturnsUnauthorized` expects 401 but receives `OkObjectResult` — signature validation may be ineffective in the current environment; must be tracked as a follow-up outside this change's scope.
- **W-2 (spec vs runtime rate-limit partition):** EIM-002 says "per-user rate limit", but `UseRateLimiter` (`Program.cs:330`) runs before `UseAuthentication` (`:338`), so `RateLimitPartitioner.AuthenticatedOrIp` sees an empty `context.User` and partitions are effectively per-client-IP at runtime. Pre-existing pattern (Reservations), documented in `Program.cs:246-251` and design ADR-6 as a follow-up. Not introduced as a regression, but the spec wording is stricter than runtime behavior.

### SUGGESTION

- **S-1:** Confirm the 10/min upload quota with product (design open question).
- **S-2:** R2 orphan sweeper for upload-OK / save-fail objects (ADR-3 accepted; follow-up).
- **S-3:** Frontend `vite build` chunk >500 kB warning is pre-existing; consider code-splitting when touching the bundle.

## No-regression

- Create-without-photo: `EventForm.test.jsx:247-273` — `POST /events` payload `imageUrl: ''` unchanged; `EventService.CreateEventAsync` untouched.
- Read flows (`GetEventByIdAsync`, `GetAllPublishedEventsAsync`, `GetScannableEventsAsync`) untouched by the diff.
- Edit-without-photo: `EventForm.test.jsx:530-561` + `EventForm.edit.test.jsx:155-174` — PUT re-sends the current URL; the `old ≠ new` guard prevents deletion.
- `DeleteEventAsync` image cleanup untouched (`EventService.cs:622-668`); `EventControllerTests` delete/409/403 matrix intact (old upload test at former `:619` removed by design — PUT-409 covered elsewhere).
- Scan/lookup flows (`TicketController`) untouched.
- `AUTHORIZATION_MATRIX.md` synced: `POST /{id}/image` dropped from `EventController` row, `UploadsController` row added, `RequireOrganizadorRole` notes the event-agnostic upload (`:18`, `:53-54`, `:38`).

## Next

`ready-for-archive` from verification's standpoint — subject to the orchestrator's review-gate/ledger state before `sdd-archive` (canonical specs must be synced at archive time per repo convention).