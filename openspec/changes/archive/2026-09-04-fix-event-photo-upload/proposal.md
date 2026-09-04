# Proposal: Fix Event Photo Upload (R2 TLS Failure + Honest, Atomic Save Flow)

## Intent

Event photo upload fails in production (Render/Linux): `POST /api/events/{id}/image` returns 500 with `sslv3 alert handshake failure`. Root cause confirmed by production log and live probe: `backend/Services/R2StorageClient.cs:43-48` forces `SslProtocols.Tls12` only, which breaks the OpenSSL 3.x handshake against Cloudflare R2 on Linux (Windows/Schannel masked it in dev). The comment blaming TLS 1.3 (lines 40-42) is disproven. Compounding this, `EventForm.jsx` shows a green "success" message when the upload fails and saves the event anyway — a false success that hid the outage.

## Scope

### In Scope
- Remove TLS 1.2 forcing in `R2StorageClient` (OS default, TLS 1.3 preferred); rewrite the comment with the production evidence so the forcing is not reintroduced.
- New event-agnostic endpoint `POST /api/uploads/event-image`: role-gated (Organizador + Admin — create is organizer-only, edit is admin-only in the current UI per EA-009/D-8), CSRF-protected, per-user rate limit, MIME (jpeg/png/webp) + 5 MB validation, returns `{ imageUrl }`. Organizers MUST NOT gain edit capability from this change: Organizador access exists solely for the create flow; the Admin-only edit UI (`canEdit = role === 'Admin'`) and the `EventOwnership` guard on `PUT /events/{id}` remain unchanged (EIM-007).
- Remove `POST /api/events/{id}/image` + `ReplaceEventImageAsync`; image persistence flows through existing `POST /events` (`CreateEventRequest.ImageUrl`) and `PUT /events/{id}` (null preserves / '' clears / value replaces).
- Move old-image cleanup into `UpdateEventAsync` (verified: PUT today never deletes the previous object — cleanup exists only in `ReplaceEventImageAsync`/`DeleteEventAsync`).
- `EventForm`: upload first, then save; phase labels ("Subiendo imagen…" / "Guardando…"); any failure → red error (`role="alert"`), no navigation, event NOT saved; delete the green false-success catch.

### Out of Scope
- Committed secrets in `backend/appsettings.Development.json` — rotation/purge deliberately deferred (would invalidate issued QR tickets and JWT sessions); registered as follow-up risk.
- R2 config fail-fast at startup; removal of unused `AWSSDK.S3` package; R2 orphan sweeper service.

## Capabilities

### New Capabilities
- `event-image-management`: upload-first event image flow — TLS transport, endpoint contract, error surfacing, save blocking, old-image cleanup, orphan policy (EIM-001…EIM-006).

### Modified Capabilities
- `past-event-mutation-guard`: PEM-002 endpoint list drops `POST /events/{id}/image` (seven → six). `PUT /events/{id}` — already listed and guarded (`EnsureMutable` before `SaveChanges`) — becomes the mutation that persists `imageUrl`. PEM-003 unchanged: no DB save/audit/notification on 409.

## Approach

Reuse what exists. `UploadEventImageAsync` is already event-agnostic (validate → `events/{guid}.ext` → R2 PUT → public URL); the new endpoint exposes it without any event lookup. Create/edit DTOs already accept `ImageUrl`. `EventForm` (consumed by `OrganizerEventNew` create, `OrganizerEventDetail` edit, `EventReadOnlyView` read-only) flips the order: with `imageFile` → upload → POST/PUT carrying the returned URL; without → current behavior. `UpdateEventAsync` gains `ReplaceEventImageAsync`'s best-effort delete of the previous object after `SaveChanges`.

Edge cases: (1) save fails after upload (finalized-event race → 409) leaves one unreferenced GUID-named R2 object — accepted bounded risk (PEM-003 holds; no DB write); a compensating-delete endpoint was rejected as an arbitrary-delete attack surface; optional sweeper follow-up. (2) `image/jpg` MIME variants rejected with visible errors on both sides. (3) The manual multipart `Content-Type` header is harmless (axios sets the boundary) — removed for clarity.

## Requirements (high-level)

| ID | Requirement |
|----|-------------|
| EIM-001 | R2 client MUST use OS-default TLS (no protocol forcing); comment MUST document the OpenSSL 3.x handshake-failure evidence |
| EIM-002 | `POST /api/uploads/event-image` MUST enforce role auth (Organizador/Admin), CSRF, per-user rate limit, MIME + ≤5 MB; SHALL return `{ imageUrl }` |
| EIM-003 | Upload failure MUST surface a red error (`role="alert"`) in `EventForm`; success-styled failure messages are forbidden |
| EIM-004 | With a selected photo, event save MUST NOT proceed if upload failed (upload-first, both modes) |
| EIM-005 | `PUT /events/{id}` replacing `ImageUrl` SHALL best-effort delete the previous image object after `SaveChanges` |
| EIM-006 | `POST /api/events/{id}/image` + `ReplaceEventImageAsync` are REMOVED; PEM-002 delta syncs the endpoint list |
| EIM-007 | Organizers MUST NOT acquire event-edit capability from this change: the Admin-only edit UI (`canEdit = role === 'Admin'`) and the `EventOwnership` guard on `PUT /events/{id}` are unchanged; Organizador access to the upload endpoint exists only for the create flow |

## Affected Areas

| Area | Impact | Change |
|------|--------|--------|
| `backend/Services/R2StorageClient.cs` | Modified | Drop TLS forcing; fix comment |
| `backend/Controllers/` (new upload route; `EventController.cs`) | New/Modified | Add upload endpoint; remove image endpoint |
| `backend/Services/EventService.cs`, `IEventService.cs` | Modified | Cleanup in `UpdateEventAsync`; remove `ReplaceEventImageAsync` |
| `frontend/src/components/EventForm.jsx` (+ existing `EventForm.test.jsx`, `__tests__/EventForm.edit.test.jsx`) | Modified | Order, states, honest errors |
| `openspec/specs/past-event-mutation-guard/` | Modified | PEM-002 delta |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Committed secrets remain exposed (out of scope by product decision) | High | Follow-up change: rotate + gitignore + history purge |
| Orphaned R2 object when save fails after upload | Low | Accepted; GUID keys, unreferenced; optional sweeper |
| Finalized-event race: upload succeeds, PUT 409 (guard no longer fires pre-upload) | Low | Edit UI already blocks past events; PEM-003 unchanged |
| Public API removal of the image endpoint | Low | SPA is sole consumer (verified); same-repo deploy |
| 4 pre-existing test failures on branch (1 security-relevant: MP webhook 200 on invalid signature) | Medium | Fix or acknowledge before promoting the branch |

## Rollback Plan

Revert this change's commits on `fix/r2-upload-linux-tls`. No migration is involved (`ImageUrl` semantics unchanged); GUID-named R2 objects make any orphan harmless; SPA and API deploy together from the same repo.

## Dependencies

Cloudflare R2 env vars verified correct in Render; deploy matches `6f4fe27`. No new dependencies.

## Success Criteria

- [ ] Live photo upload from the Linux deployment succeeds against real R2 (TLS 1.3).
- [ ] Failed upload shows a red error and blocks the save in create AND edit; no green false-success.
- [ ] `dotnet test` and `npm test` green, including new endpoint, cleanup, and EventForm flow tests.
- [ ] PEM-002 spec synced to six endpoints.

## Proposal Question Round (assumptions to confirm at spec/design)

1. Endpoint path `POST /api/uploads/event-image` (alternative: `POST /api/events/image`) — assumed.
2. Full removal, not deprecation, of `POST /api/events/{id}/image` — assumed; no other consumers found.
3. Orphan policy: accepted bounded risk, no compensating-delete endpoint — assumed.
