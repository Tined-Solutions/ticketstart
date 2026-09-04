# Tasks: Fix Event Photo Upload (R2 TLS + Honest Atomic Save Flow)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~950–1050 (prod ~215, backend tests ~490, frontend ~340) |
| 400-line budget risk | High |
| Effective budget (orchestrator) | 800 — estimate can exceed it |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 transport+endpoint → PR 2 cleanup+removal → PR 3 frontend |
| Delivery strategy | single-pr |
| Chain strategy | pending |

```text
Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High
```

`single-pr` + over-budget → orchestrator MUST require `size:exception` (or user picks a chain) before apply.

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | TLS fix + upload endpoint + rate policy (EIM-001/002) | PR 1 | `dotnet test --filter "UploadsControllerTests\|R2StorageClientTests"` | N/A — live R2 probe is deploy-time (needs real credentials); TLS proven by reflection test | Revert R2StorageClient.cs; delete UploadsController.cs + Program.cs policy |
| 2 | Cleanup in UpdateEventAsync + remove old endpoint/method (EIM-005/006) | PR 2 | `dotnet test --filter "EventServiceTests\|ImageStoragePropertyTests\|EventServiceImmutabilityTests"` | N/A — unit-level, mocked IR2StorageClient; no real R2 boundary | Restore ReplaceEventImageAsync + old route; drop cleanup block |
| 3 | EventForm upload-first + honest errors (EIM-003/004) | PR 3 | `npx vitest run EventForm` | Manual browser create/edit with photo vs dev backend | Revert EventForm.jsx only |

## Phase 1: Backend Transport + Endpoint

- [x] 1.1 RED: new `R2StorageClientTests` — reflection asserts HttpClient `SslOptions.EnabledSslProtocols` unset (EIM-001). GREEN: remove forcing block `R2StorageClient.cs:43-49`; rewrite comment (40-42) with `sslv3 alert handshake failure` / `0A000410` evidence.
- [x] 1.2 RED: new `UploadsControllerTests` (WebApplicationFactory) — EIM-002: 200 organizer+Admin; 401 unauth; 403 staff; 400 missing CSRF / `image/jpg` / >5MB / missing part; 429 on 11th call. GREEN: create `UploadsController.cs` (`POST /api/uploads/event-image`, `RequireOrganizadorRole`, `EnableRateLimiting("EventImageUpload")`, ArgumentException→400, InvalidOperationException→500); add policy in `Program.cs` (10/min, `RateLimitPartitioner.AuthenticatedOrIp`).

## Phase 2: Backend Cleanup + Removal

- [x] 2.1 RED: `EventServiceTests` region — replaced deletes old; same-URL re-send no delete; `""` clears+deletes; null preserves; delete-failure → 200 + warning logged. GREEN: capture `previousImageUrl` before mutation (EventService.cs:516); best-effort delete after `SaveChanges` (:522) with `old ≠ new` guard.
- [x] 2.2 RED: `ImageStoragePropertyTests` — R2 delete invoked iff old non-empty ∧ new non-null ∧ old ≠ new. GREEN: refactor cleanup into property-proven shape.
- [x] 2.3 RED: rewrite `EventServiceImmutabilityTests` PEM region + `ImageStoragePropertyTests` 4 sites against `UpdateEventAsync`; delete `EventControllerTests:619` — compile breaks force removal. GREEN: remove `ReplaceEventImageAsync` (IEventService.cs:88-103, EventService.cs:721-772); remove `EventController.UploadEventImage` (:225-283).
- [x] 2.4 RED: `UploadsControllerTests` — old route `POST /api/events/{id}/image` → 404 (EIM-006). GREEN: already satisfied by 2.3 removal; keep green.

## Phase 3: Frontend Upload-First

- [x] 3.1 RED: `EventForm.test.jsx` — upload-fail blocks `POST /events` (not called), red `role="alert"`, no green; upload-success precedes save carrying `imageUrl`; labels "Subiendo imagen…"/"Guardando…" + disabled; no-photo flow unchanged. GREEN: `EventForm.jsx` — `phase` state, upload step via `/uploads/event-image`, remove manual multipart header + false-success catch (:170-178), payload `imageUrl`.
- [x] 3.2 RED: `EventForm.edit.test.jsx` — edit+photo PUT carries new URL; upload-fail blocks PUT; no-photo preserves `initialData.imageUrl`. GREEN: edit branch payload `uploadedUrl ?? initialData?.imageUrl || ''`.

## Phase 4: Integration + Docs + Verification

- [x] 4.1 Swap `AdminUserManagementIntegrationTests:446-453` — revoked-owner probe → `POST /api/uploads/event-image` (Forbidden); assert old route 404.
- [x] 4.2 Sync `AUTHORIZATION_MATRIX.md`: drop `POST /{id}/image`, add `UploadsController` row. (PEM-002 spec already synced — no task.)
- [x] 4.3 Full suites green: `dotnet test` (backend) + `npm test` (frontend). (Backend: 749 ✅ + 4 pre-existentes ❌ confirmados en base; Frontend: 505/505 ✅.)

Threat matrix: N/A per design (no shell/process/routing-CLI boundary; new HTTP route gated by existing auth/CSRF/rate-limit stack) — no RED-security tasks.