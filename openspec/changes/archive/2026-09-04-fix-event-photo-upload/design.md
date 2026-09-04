# Design: Fix Event Photo Upload (R2 TLS + Honest, Atomic Save Flow)

Branch `fix/r2-upload-linux-tls` · Specs: EIM-001…007, PEM-002 delta · All line numbers verified against current code.

## Technical Approach

Two halves joined by one contract. (1) Transport: `R2StorageClient` drops the `EnabledSslProtocols.Tls12` forcing so Linux/OpenSSL 3.x negotiates TLS 1.3 with R2 (EIM-001). (2) Flow inversion: a new event-agnostic `POST /api/uploads/event-image` exposes the already-event-agnostic `EventService.UploadEventImageAsync` (validate → `events/{guid}.ext` → R2 PUT → public URL; no event row touched); `EventForm` flips to upload-first so a failed upload blocks the save (EIM-002…004); `UpdateEventAsync` absorbs old-image cleanup (EIM-005); `POST /api/events/{id}/image` + `ReplaceEventImageAsync` are removed (EIM-006). No organizer edit escalation (EIM-007): the endpoint accepts no event id, and attaching a URL to an event still requires `POST /events` (creator becomes owner) or `PUT /events/{id}` (`EventOwnership` + `EnsureMutable`, both untouched).

## Architecture Decisions

| # | Decision | Rejected alternatives & why | Rationale |
|---|----------|------------------------------|-----------|
| ADR-1 | New `UploadsController`, route `api/uploads` | Action inside `EventController` (mixes route namespaces); `api/events/image` (implies event coupling) | Event-agnostic resource → own controller, matching the repo's controller-per-route-prefix convention. Makes EIM-007 structural: no event id in the route → no lookup, no ownership check to bypass |
| ADR-2 | Reuse `IEventService.UploadEventImageAsync` | New `IUploadService` (duplicate layer, zero new logic); extract `IImageStorageService` (interface churn across 8+ callers/tests) | The method already implements EIM-002's exact contract (MIME/size validation, GUID key, R2 PUT, public URL) and is covered by `EventImageUploadTests` + `ImageStoragePropertyTests` |
| ADR-3 | Orphan on save-failure accepted | Compensating-delete endpoint (arbitrary-delete surface: any GUID key); sweeper service (out of scope) | GUID-named, unreferenced objects; low likelihood (save rarely fails after upload); PEM-003 holds — 409 writes nothing |
| ADR-4 | Cleanup AFTER `SaveChanges`, best-effort | Pre-save delete (would remove an object the DB still references if the save fails); transactional delete (impossible — R2 and PostgreSQL share no transaction) | Mirrors the deleted `ReplaceEventImageAsync` and `DeleteEventAsync` semantics; delete failure logs a warning, never fails the request (EIM-005) |
| ADR-5 | Role gate = existing `RequireOrganizadorRole` | New combined policy | Policy is `RequireRole("Organizador","Admin")` (Program.cs:156) — exactly EIM-002's set |
| ADR-6 | New rate-limit policy `EventImageUpload`: fixed window, 10/min, `RateLimitPartitioner.AuthenticatedOrIp` | Per-IP-only partition; no limit | 5 MB multipart abuse path gets its own limiter (backend-security gate: "new abuse-prone endpoint → add a policy"); helper matches the Reservations pattern. ⚠ Discovery: `UseRateLimiter` (Program.cs:313) runs BEFORE `UseAuthentication` (:321), so partitioner callbacks see an unauthenticated `context.User` — partitions are effectively per-client-IP at runtime (already true for Reservations today). Reordering the pipeline is out of scope; recorded as risk + open question |
| ADR-7 | TLS fix = remove the `SslOptions` assignment entirely | Keep any protocol list; revert to bare `new HttpClient()` | `new HttpClient(new SocketsHttpHandler())` uses OS-default TLS (1.3 preferred). Constructor comment rewritten with the production evidence (`sslv3 alert handshake failure`, `0A000410` on OpenSSL 3.x; OS defaults succeed) so the forcing is never reintroduced |

## Endpoint Contract (EIM-002)

New `backend/Controllers/UploadsController.cs`:

```csharp
[ApiController]
[Route("api/uploads")]
public class UploadsController : TicketeraControllerBase
{
    [HttpPost("event-image")]
    [Authorize(Policy = "RequireOrganizadorRole")]   // Organizador + Admin
    [EnableRateLimiting("EventImageUpload")]
    public async Task<IActionResult> UploadEventImage(IFormFile image) // binds form field "image"
```

Body: `using var stream = image.OpenReadStream(); var url = await _eventService.UploadEventImageAsync(stream, image.FileName, image.ContentType); return Ok(new { imageUrl = url });`

| Failure | Enforced by | Status |
|---|---|---|
| Missing/empty `image` part | controller null-check | 400 |
| MIME ∉ {jpeg,png,webp} or > 5 MB | service `ArgumentException` → catch → 400 | 400 |
| Missing `X-CSRF-PROTECT` | `CsrfHeaderMiddleware` — unchanged; covers every POST except webhook/login | 400 |
| No JWT cookie | auth middleware | 401 |
| Role outside Organizador/Admin | policy | 403 |
| Quota exhausted | limiter (`RejectionStatusCode = 429`) | 429 |
| R2 transport failure | `InvalidOperationException` → catch → 500 | 500 |

Program.cs — the only wiring change (no DI change; `IEventService` already registered; the `apiClient` request interceptor already sends `X-CSRF-PROTECT` on POST):

```csharp
options.AddPolicy("EventImageUpload", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitioner.AuthenticatedOrIp(context),
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
```

## Frontend — upload-first (EIM-003/004)

`EventForm.handleSubmit` gains a `phase` state (`''` | `'uploading'` | `'saving'`); `submitting = phase !== ''`:

1. Client validation unchanged; early return on errors.
2. If `imageFile`: `phase='uploading'` → `apiClient.post('/uploads/event-image', formData)` (manual `Content-Type: multipart/form-data` header REMOVED — axios sets the boundary). On failure: `setFeedback({type:'error', message: getErrorMessage(error)})` and RETURN — no save call, no navigation. On success: `uploadedUrl`.
3. `phase='saving'` → create: `POST /events` with `imageUrl: uploadedUrl || ''` (CreateEventRequest.ImageUrl is non-nullable). Edit: `PUT /events/{id}` with `imageUrl: uploadedUrl ?? (initialData?.imageUrl || '')`.
4. Success → green feedback + `onSuccess(eventId)`.

Submit label: `'Subiendo imagen…'` / `'Guardando…'` / default, disabled while submitting. The green false-success catch (EventForm.jsx:170-178) is deleted. Existing feedback markup already renders `role="alert"` for `type:'error'` (line 244). Without `imageFile`, the flow is byte-identical to today.

### Sequence — create with photo (organizer)

```
EventForm → POST /api/uploads/event-image (multipart, CSRF)   [Subiendo imagen…]
          ← 200 { imageUrl }
EventForm → POST /api/events {…, imageUrl}                    [Guardando…]
          ← 201
EventForm → onSuccess(eventId) → navigate                     (green feedback)
```

### Sequence — edit with photo (admin)

```
EventForm → POST /api/uploads/event-image                     [Subiendo imagen…]
          ← 200 { imageUrl }
EventForm → PUT /api/events/{id} {…, imageUrl}                [Guardando…]
  UpdateEventAsync: FindAsync → ownership → EnsureMutable (PEM-002)
    → ImageUrl = new → SaveChanges
    → best-effort DeleteImageAsync(previousImageUrl)          (EIM-005)
          ← 200 → onSuccess → navigate
```

### Sequence — upload fails (blocks save; both modes)

```
EventForm → POST /api/uploads/event-image
          ← 4xx/5xx
red role="alert" error · POST/PUT never called · no navigation · phase='' (button re-enabled)
```

### Sequence — edit without photo (unchanged)

```
EventForm → PUT /api/events/{id} {…, imageUrl: currentUrl || ''}   (no upload call)
```

## Cleanup in `UpdateEventAsync` (EIM-005)

Capture `var previousImageUrl = eventEntity.ImageUrl;` BEFORE the `request.ImageUrl != null` mutation (mirrors `ReplaceEventImageAsync:752`). Immediately after `SaveChanges` (line 522), before the EDC-001 date-change block:

```csharp
if (request.ImageUrl != null
    && !string.IsNullOrWhiteSpace(previousImageUrl)
    && !string.Equals(previousImageUrl, request.ImageUrl, StringComparison.Ordinal))
{
    var deleted = await DeleteImageAsync(previousImageUrl);
    if (!deleted) _logger.LogWarning(
        "Failed to delete previous image for event {EventId}; new image already persisted", eventId);
}
```

`DeleteImageAsync` (private, reused unchanged) already handles every edge: missing bucket/PublicUrl config → false+log; URL not matching the `PublicUrl` base (e.g. `https://example.com/…` test seeds) → warning + false, no R2 call; empty extracted key → false; R2 failure → false while the request still returns 200. The `old ≠ new` guard covers two critical cases: an edit without a photo re-sends the CURRENT URL as a value — it MUST NOT delete the object the event still points at; and `null` (text-only edit) preserves.

## Elimination (EIM-006) + PEM-002

Removed: `EventController.UploadEventImage` action (lines 225-283, including the now-false "409 fires before any R2 upload" comment — the guard timing moves from upload time to persist time), `IEventService.ReplaceEventImageAsync` (88-103), `EventService.ReplaceEventImageAsync` (721-772). Route gone → old callers get 404; SPA is the sole consumer and deploys with the API. Test touch-points: `EventServiceImmutabilityTests` PEM region (213+), `ImageStoragePropertyTests` 4 call sites (778/841/886/905) → rewritten against `UpdateEventAsync`, `EventControllerTests:619` → removed (PUT-409 already covered), `AdminUserManagementIntegrationTests:446-453` → ownership-probe swapped (live-DB test; the removed route now 404s). `AUTHORIZATION_MATRIX.md:52` drops `POST /{id}/image` and gains an `UploadsController` row. The PEM-002 delta (seven → six endpoints) is already written in this change's spec; `EnsureMutable` already runs before `SaveChanges` in `UpdateEventAsync` — no guard code moves, only the upload-time pre-check disappears.

## EIM-007 — no organizer escalation

Structural: the route accepts no event id and the service path touches no event row, so there is no ownership check to bypass and no way to target a specific event's image. Attach remains gated by `POST /events` (RequireOrganizadorRole + ownership assignment) and `PUT /events/{id}` (EventOwnership + service ownership check + EnsureMutable). Frontend: `canEdit = role === 'Admin'` (OrganizerDashboard.jsx:29) unchanged; `EventReadOnlyView` renders `EventForm readOnly` (no submit path, no API call).

## File Changes

| File | Action | Change |
|---|---|---|
| `backend/Services/R2StorageClient.cs` | Modify | Remove `SslOptions` forcing (43-49); rewrite constructor comment with production evidence |
| `backend/Controllers/UploadsController.cs` | Create | `POST /api/uploads/event-image` |
| `backend/Program.cs` | Modify | Add `EventImageUpload` rate-limit policy |
| `backend/Controllers/EventController.cs` | Modify | Remove image endpoint (225-283) |
| `backend/Services/IEventService.cs` | Modify | Remove `ReplaceEventImageAsync` (88-103) |
| `backend/Services/EventService.cs` | Modify | Remove `ReplaceEventImageAsync` (721-772); add cleanup to `UpdateEventAsync` |
| `backend/AUTHORIZATION_MATRIX.md` | Modify | Sync endpoint list (docs are summaries; code wins) |
| `frontend/src/components/EventForm.jsx` | Modify | Upload-first phases, honest errors, payload `imageUrl` |
| Backend + frontend test files (below) | Modify | RED-first (strict TDD per config) |

## Testing Strategy

| Layer | What | How |
|---|---|---|
| Unit (xUnit + Moq + InMemory) | EIM-005: replaced deletes old; same-URL re-send no delete; `""` clears + deletes; null preserves; delete-failure → request succeeds + warning logged | New region in `EventServiceTests`; mock `IR2StorageClient` |
| Property (FsCheck) | Cleanup invariant: R2 delete invoked iff old non-empty ∧ new non-null ∧ old ≠ new | Extend `ImageStoragePropertyTests` |
| Integration (`WebApplicationFactory`) | EIM-002 all 8 scenarios (200 organizer/admin; 401/403; 400 CSRF/MIME/size/missing-part; 429 on 11th call); EIM-006 old route 404 | New `UploadsControllerTests` |
| Component (Vitest + RTL) | EIM-003/004: upload failure blocks save (assert POST/PUT not called), success carries `imageUrl`, phase labels + disabled button, no-photo flow unchanged, no green-on-failure | Update `EventForm.test.jsx`, `__tests__/EventForm.edit.test.jsx`; `vi.mock` the api client |

## Threat Matrix

N/A — no shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary. The matrix's "routing" row targets CLI command routing; this change adds one HTTP route gated by the existing auth/CSRF/rate-limit middleware stack.

## Migration / Rollout

No migration required. No schema change (`ImageUrl` semantics unchanged); SPA and API deploy together from this repo; rollback = revert the change's commits.

## Open Questions

- [ ] Rate-limit numbers (proposed 10/min) — confirm with product.
- [ ] `UseRateLimiter` before `UseAuthentication` defeats claims-based (per-user) partitioning — pre-existing, also affects Reservations. Follow-up pipeline-order change?
