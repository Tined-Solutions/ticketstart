# Event Image Management Specification

**Requirements covered**: EIM-001 … EIM-007

## Purpose

Event photos upload reliably to Cloudflare R2 from the Linux production deployment (OS-default TLS), through a new event-agnostic upload endpoint consumed by `EventForm` in an upload-first, atomic save flow: a failed upload blocks the event save and shows an honest red error; a successful upload is persisted via the existing `POST /events` / `PUT /events/{id}` mutations, which own old-image cleanup. `POST /api/events/{id}/image` is removed. The change MUST NOT grant organizers any edit capability.

## Requirements

### Requirement: EIM-001: OS-default TLS transport to R2

`R2StorageClient` MUST NOT force any `EnabledSslProtocols` value — the HTTP client SHALL negotiate TLS via OS defaults (TLS 1.3 preferred). The constructor comment MUST be rewritten to document the production evidence: forcing `Tls12` alone breaks the OpenSSL 3.x handshake against Cloudflare R2 on Linux (`sslv3 alert handshake failure`, error `0A000410`) and OS defaults succeed, so the forcing MUST NOT be reintroduced.

#### Scenario: Linux upload succeeds via OS-default TLS

- GIVEN `R2StorageClient` on Linux with OpenSSL 3.x and no `EnabledSslProtocols` override
- WHEN an image is uploaded to R2
- THEN the handshake negotiates TLS 1.3 by default and the PUT succeeds (verified by live probe)

#### Scenario: No protocol forcing remains

- GIVEN the `R2StorageClient` constructor
- WHEN `SslOptions` is inspected
- THEN `EnabledSslProtocols` is unset (OS default), not `Tls12`

#### Scenario: Comment documents the evidence

- GIVEN the rewritten constructor comment
- WHEN a developer reads it
- THEN it states that TLS 1.2 forcing breaks the OpenSSL 3.x handshake (`sslv3 alert handshake failure`, `0A000410`) and that OS defaults (TLS 1.3) work

### Requirement: EIM-002: Event-agnostic upload endpoint

The system MUST provide `POST /api/uploads/event-image` (multipart/form-data, field `image`) that accepts no event identifier and performs no event lookup or ownership check. It MUST enforce: role authorization restricted to Organizador and Admin; the `X-CSRF-PROTECT` header; a per-user rate limit; MIME ∈ {image/jpeg, image/png, image/webp}; size ≤ 5 MB. Success MUST return 200 with `{ "imageUrl": "…" }` pointing at the stored `events/{guid}.{ext}` object. Validation and storage reuse the event-agnostic upload path currently behind `ReplaceEventImageAsync`.

#### Scenario: Organizer uploads a valid image

- GIVEN an authenticated Organizador and a valid JPEG under 5 MB
- WHEN the organizer POSTs it as `image` with `X-CSRF-PROTECT`
- THEN the response is 200 with `{ imageUrl }` and no event id is accepted in the request

#### Scenario: Admin uploads a valid image

- GIVEN an authenticated Admin and a valid PNG
- WHEN the admin POSTs it
- THEN the response is 200 with `{ imageUrl }`

#### Scenario: Unauthorized role rejected

- GIVEN an unauthenticated request or a non-Organizador/non-Admin role
- WHEN the endpoint is called
- THEN the request is rejected (401/403) and no object is uploaded

#### Scenario: Missing CSRF header rejected

- GIVEN an authenticated organizer
- WHEN the POST omits `X-CSRF-PROTECT`
- THEN the CSRF middleware rejects it with 400 and no upload occurs

#### Scenario: Rate limit exceeded

- GIVEN a user who has exhausted the per-user upload quota
- WHEN the endpoint is called again
- THEN the response is 429

#### Scenario: Invalid MIME variant rejected

- GIVEN a file with `Content-Type: image/jpg` (variant of image/jpeg)
- WHEN it is posted
- THEN the response is 400 and no R2 object is created

#### Scenario: Oversize file rejected

- GIVEN a file larger than 5 MB
- WHEN it is posted
- THEN the response is 400 and no R2 object is created

#### Scenario: Missing file part rejected

- GIVEN a multipart POST without an `image` part
- WHEN it is posted
- THEN the response is 400

### Requirement: EIM-003: Honest error surfacing

`EventForm` MUST render an upload failure as a red error with `role="alert"` and MUST NOT render any success-styled message when the upload failed — the current green "event created/updated, but the image could not be uploaded" catch MUST be removed. While the upload or save is in flight the submit button MUST be disabled with phase labels ("Subiendo imagen…" / "Guardando…").

#### Scenario: Failed upload in create shows red error

- GIVEN a create form with a selected photo
- WHEN the upload fails
- THEN a red `role="alert"` error appears and no success-styled message is rendered

#### Scenario: Failed upload in edit shows red error

- GIVEN an edit form with a selected photo
- WHEN the upload fails
- THEN a red `role="alert"` error appears and the form does not navigate away

#### Scenario: Success shows green only after save

- GIVEN a photo that uploads and saves successfully
- WHEN the save completes
- THEN the success message renders and `onSuccess` navigates

### Requirement: EIM-004: Upload-first, save-blocking flow

When a photo is selected, `EventForm` MUST upload it BEFORE the event save and MUST NOT proceed to `POST /events` or `PUT /events/{id}` if the upload failed — in both create and edit modes. A successful upload MUST carry the returned `imageUrl` into the save payload. Without a selected photo, save behavior MUST be unchanged.

#### Scenario: Create with photo — upload failure blocks save

- GIVEN a valid create form with a selected photo
- WHEN the upload fails
- THEN `POST /events` is not called and no event is created

#### Scenario: Create with photo — upload success precedes save

- GIVEN a valid create form with a selected photo
- WHEN the upload succeeds
- THEN `POST /events` is called with the returned `imageUrl` in the payload

#### Scenario: Edit with photo — PUT carries the new imageUrl

- GIVEN an edit form with a selected photo
- WHEN the upload succeeds
- THEN `PUT /events/{id}` is called with the new `imageUrl`

#### Scenario: Edit without photo — semantics unchanged

- GIVEN an edit form without a selected photo
- WHEN the form is submitted
- THEN `PUT /events/{id}` preserves the existing image (null preserves / '' clears / value replaces)

#### Scenario: Upload OK but save rejected (finalized-event race)

- GIVEN an edit of a past event where the upload succeeds first
- WHEN `PUT /events/{id}` runs
- THEN the PEM-002 guard returns 409 and no DB change or side-effect occurs
- AND the uploaded object may remain as an accepted, unreferenced orphan (PEM-003; no compensating delete)

### Requirement: EIM-005: Old-image cleanup in UpdateEventAsync

When `PUT /events/{id}` persists a replaced or cleared `ImageUrl`, the system SHALL best-effort delete the previous R2 object AFTER `SaveChanges` succeeds; a deletion failure MUST NOT fail the request (it SHALL be logged). A null `ImageUrl` (text-only edit) MUST NOT trigger any deletion.

#### Scenario: Replaced image deletes the old object

- GIVEN an event whose `ImageUrl` points at object A
- WHEN a PUT replaces it with object B and saves
- THEN object A is best-effort deleted after the save

#### Scenario: Text-only edit preserves the image

- GIVEN an event with an existing `ImageUrl`
- WHEN a PUT omits `imageUrl` (null)
- THEN the image is preserved and no object is deleted

#### Scenario: Cleared image deletes the old object

- GIVEN an event with an existing `ImageUrl`
- WHEN a PUT sends `imageUrl: ""`
- THEN the URL is cleared and the previous object is best-effort deleted

#### Scenario: Delete failure does not fail the request

- GIVEN the previous object's deletion fails
- WHEN the PUT has already saved
- THEN the response is 200 and a warning is logged

### Requirement: EIM-006: Removal of the old image endpoint

`POST /api/events/{id}/image` and `ReplaceEventImageAsync` MUST be removed entirely (not deprecated); the SPA is the sole consumer and deploys with the API. The immutability guard that previously ran before the upload in `ReplaceEventImageAsync` no longer runs at upload time — it remains enforced at persist time in `UpdateEventAsync` (PEM-002).

#### Scenario: Old endpoint no longer resolves

- GIVEN the deployed API after this change
- WHEN a client calls `POST /api/events/{id}/image`
- THEN the response is 404 (route removed)

#### Scenario: Service contract drops the method

- GIVEN `IEventService` after this change
- WHEN its members are enumerated
- THEN `ReplaceEventImageAsync` is absent

### Requirement: EIM-007: No organizer edit escalation

This change MUST NOT grant organizers any event-edit capability. The upload endpoint is event-agnostic and MUST NOT accept an event id, so an organizer cannot use it to replace a specific event's image. The Admin-only edit UI (`canEdit = role === 'Admin'`) and the `EventOwnership` guard on `PUT /events/{id}` MUST remain unchanged; organizer access to the upload endpoint exists only to feed the create flow.

#### Scenario: Upload cannot target a specific event

- GIVEN an organizer calling `POST /api/uploads/event-image`
- WHEN the request is inspected
- THEN no event id is accepted and no ownership check runs against any event

#### Scenario: Edit UI stays Admin-only

- GIVEN an organizer viewing any event
- WHEN the UI renders
- THEN no edit entry appears (`canEdit` remains `role === 'Admin'`)

#### Scenario: PUT authorization unchanged

- GIVEN an organizer attempting `PUT /events/{id}` on a non-owned or finalized event
- WHEN the request is made
- THEN it is rejected exactly as before this change (EventOwnership 403 / PEM-002 409)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| EIM-001 | linux-upload-succeeds, no-protocol-forcing, comment-documents-evidence |
| EIM-002 | organizer-upload-ok, admin-upload-ok, unauthorized-role, missing-csrf, rate-limit, invalid-mime, oversize, missing-part |
| EIM-003 | create-failure-red, edit-failure-red, success-green-after-save |
| EIM-004 | create-failure-blocks-save, create-success-carries-url, edit-carries-new-url, edit-without-photo-unchanged, upload-ok-put-409-orphan |
| EIM-005 | replaced-deletes-old, text-only-preserves, cleared-deletes-old, delete-failure-200 |
| EIM-006 | old-endpoint-404, service-contract-drops-method |
| EIM-007 | upload-cannot-target-event, edit-ui-admin-only, put-authorization-unchanged |