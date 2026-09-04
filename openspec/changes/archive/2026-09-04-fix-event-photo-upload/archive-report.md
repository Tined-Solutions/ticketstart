# Archive Report: fix-event-photo-upload

**Change**: fix-event-photo-upload — Fix Event Photo Upload (R2 TLS Failure + Honest, Atomic Save Flow)
**Archived**: 2026-09-04 (matches branch HEAD commit date and orchestrator-specified archive date)
**Archive path**: `openspec/changes/archive/2026-09-04-fix-event-photo-upload/`
**Artifact store**: hybrid (OpenSpec filesystem + Engram persistence, per `openspec/config.yaml` `artifact_store: hybrid`)
**Capabilities**: `event-image-management` (NEW canonical domain), `past-event-mutation-guard` (MODIFIED canonical domain, PEM-002 delta)
**Main specs**: `openspec/specs/{event-image-management,past-event-mutation-guard}/spec.md`

## Final State

**Cycle outcome: COMPLETE.** The change was fully planned, implemented, verified, and archived. All **11/11** implementation tasks (1.1–4.3) are checked in the persisted `tasks.md` (0 unchecked). Formal `sdd-verify` produced `pass_with_warnings` with **0 CRITICAL** findings; native validator `sdd-verify-validate` returned `valid:true` (orchestrator final-state fact).

**Delivery commits on branch `fix/r2-upload-linux-tls` (HEAD `98cfb0a`, 4 commits over base `6f4fe27`, working tree clean at verification):** `47c3ae4` (feat(backend): event-agnostic image upload endpoint with OS-default TLS), `909e427` (refactor(backend): move old-image cleanup into UpdateEventAsync, remove legacy image endpoint), `3d7d34b` (feat(frontend): upload-first image flow with honest errors in EventForm), `98cfb0a` (test(backend): swap revoked-owner probe to uploads endpoint; docs: sync authorization matrix). Note: `apply-progress.md` (intermediate snapshot) listed the fourth commit as "(pendiente)" — stale at snapshot time; the final state per launch-prompt fact (rank 3) is 4 commits delivered.

**What the change delivered:**
- **EIM-001 — OS-default TLS to R2**: `R2StorageClient` no longer forces `EnabledSslProtocols` (was `Tls12` only, which broke the OpenSSL 3.x handshake against Cloudflare R2 on Linux — `sslv3 alert handshake failure`, error `0A000410`); client built as `new HttpClient(new SocketsHttpHandler())`; constructor comment rewritten with the production evidence so the forcing is never reintroduced.
- **EIM-002 — event-agnostic upload endpoint**: new `POST /api/uploads/event-image` (`UploadsController`, route `api/uploads`), role-gated to Organizador + Admin (`RequireOrganizadorRole`), CSRF-protected, rate-limited (`EventImageUpload` policy, 10/min fixed window), MIME ∈ {jpeg,png,webp} + ≤ 5 MB, returns `{ imageUrl }`; reuses the already-event-agnostic `UploadEventImageAsync` (validate → `events/{guid}.ext` → R2 PUT → public URL; no event row touched).
- **EIM-003/004 — upload-first, atomic save flow**: `EventForm` uploads before saving; a failed upload renders a red `role="alert"` error, blocks `POST /events` / `PUT /events/{id}`, and never navigates; phase labels ("Subiendo imagen…" / "Guardando…") with the submit button disabled; the old green false-success catch was deleted; no-photo flow byte-identical to before.
- **EIM-005 — old-image cleanup in `UpdateEventAsync`**: `previousImageUrl` captured before the mutation; best-effort delete of the previous R2 object after `SaveChanges` (failure logs a warning, never fails the request); `old ≠ new` guard; null preserves / `""` clears.
- **EIM-006 — removal**: `POST /api/events/{id}/image` and `ReplaceEventImageAsync` removed entirely (route now 404); `AUTHORIZATION_MATRIX.md` synced.
- **EIM-007 — no organizer edit escalation**: the endpoint accepts no event id (structural), `canEdit = role === 'Admin'` and the `EventOwnership` guard on `PUT /events/{id}` unchanged.
- **PEM-002 delta**: guarded endpoint list 7 → 6; `PUT /events/{id}` (already guarded, `EnsureMutable` before `SaveChanges`) now persists a replaced `imageUrl`; PEM-003 unchanged (no save/audit/notification on 409).

## Verification Evidence (final)

| Suite | Result at close | Source |
|-------|-----------------|--------|
| Requirements | 8/8 (EIM-001…007 + PEM-002 delta) | `verify-report.md` + launch-prompt fact; validator `valid:true` |
| Scenarios | 31/31 against real code | `verify-report.md` + launch-prompt fact |
| Backend tests | 749 passed / **4 pre-existing failures unresolved** (confirmed identical against base `6f4fe27`; outside scope) | launch-prompt fact (outranks snapshots) |
| Frontend tests | 505/505 passed | launch-prompt fact + `verify-report.md` |
| Backend build | `dotnet build` exit 0 | `verify-report.md` |
| Frontend build | `npx vite build` exit 0 (only pre-existing >500 kB chunk warning) | `verify-report.md` |

**The 4 pre-existing backend failures (confirmed against base `6f4fe27`; not introduced by this change; follow-up pending):**
1. `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized` — **security-relevant**: expects 401, receives `OkObjectResult`; signature validation may be ineffective in the current environment.
2. `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized` — **security-relevant**: same webhook invalid-signature class.
3. `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted` — non-security.
4. `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader` — non-security (order/env-sensitive, depends on `HasLiveDatabase()`).

Per the Final-State Authority, these facts come from the orchestrator's launch prompt (rank 3) corroborated by `verify-report.md` (rank 4 snapshot whose numbers match at close); the pre-existing nature is corroborated by apply-progress (base-worktree confirmation against `6f4fe27`).

**W-2 unresolved (documented, not a regression):** EIM-002 says "per-user rate limit", but `UseRateLimiter` runs BEFORE `UseAuthentication`, so `RateLimitPartitioner.AuthenticatedOrIp` sees an empty `context.User` and partitions are effectively **per-client-IP at runtime** — pre-existing pattern (Reservations), documented in `Program.cs:246-251` and design ADR-6 as a follow-up (pipeline reorder out of scope).

**SUGGESTIONS pending (follow-ups, not blocking):** S-1 confirm the 10/min upload quota with product (design open question); S-2 R2 orphan sweeper for upload-OK / save-fail objects (ADR-3 accepted); S-3 frontend code-splitting for the >500 kB chunk.

**Committed secrets NOT resolved (product decision, out of scope to avoid invalidating QR/JWT):** `backend/appsettings.Development.json` secrets remain; registered as a **high-priority follow-up** (rotate + gitignore + history purge).

## Gates

| Gate | Result | Evidence |
|------|--------|----------|
| Native Review Receipt | Pass (structural absence) | No `reviewGate` in the orchestrator's structured status and no review artifacts exist for this candidate (no `state.yaml`, no receipt/ledger/transaction files or Engram review topics). Archive proceeds under ordinary repository policy. |
| Task Completion | Pass | Persisted `tasks.md`: **11/11 `[x]`, 0 unchecked** (verified via grep before the move: 11 checked, 0 unchecked; re-verified on the archived copy after the move). |
| CRITICAL verification issues | None | `verify-report.md` `critical_findings: 0`, "### CRITICAL — None". No CRITICAL ever recorded by any artifact. |
| Action Context | Pass | No `actionContext.mode: workspace-planning`; all operations inside workspace root `C:\Users\user\Desktop\ticketstart`; no `allowedEditRoots` restriction violated. |

## Spec Sync

### New canonical domain (mechanical copy — never model Read→Write)

No `openspec/specs/event-image-management/` existed before this archive. The delta IS a full spec (canonical format: `## Purpose`, `## Requirements`, `## Coverage Matrix`). Copied **mechanically** with the shell (`Copy-Item` to temp in target dir → `diff -r` readback → atomic `Move-Item`):

```
=== VERBATIM diff -r OUTPUT (event-image-management copy readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Result: `openspec/specs/event-image-management/spec.md` — 7 requirements (EIM-001..007), 27 scenarios, coverage matrix.

### MODIFIED canonical domain (delta merged into existing main spec)

| Domain | Main spec before | Merge action | Result |
|--------|------------------|--------------|--------|
| past-event-mutation-guard | `openspec/specs/past-event-mutation-guard/spec.md` (PEM-001..005) | Replaced the full PEM-002 requirement block with the delta's full MODIFIED block, matched by **ID prefix** (repo convention — headings renamed within MODIFIED blocks; precedent EHE-006 in remove-organizer-delete-metrics); updated the PEM-002 coverage-matrix row | PEM-001/003/004/005 byte-preserved; PEM-002: heading renamed to "All six mutation endpoints reject past events", endpoint list 7 → 6 (`POST /events/{id}/image` dropped), **ED-001 clarification paragraph preserved verbatim** (byte-identical), delta `(Previously: ...)` change-history note carried per current convention, third scenario `put-persists-replaced-image-url` added; coverage matrix row updated to `each-mutation-409, rfc7807-problem-details, put-persists-replaced-image-url` |

Merge normalization (recorded, intentional): delta `## MODIFIED Requirements` framing resolves to the canonical `## Requirements` section; requirements not mentioned in the delta are byte-preserved. Canonical spec title left untouched (delta title describes the change's focus). One consistency edit beyond the requirement block: the canonical **Purpose** sentence "All seven event-mutation endpoints" → "All six event-mutation endpoints" — a one-word factual correction required because the synced endpoint list is now six; recorded here as intentional (a source-of-truth spec must not contradict its own merged requirement).

## Archive Move

`openspec/changes/fix-event-photo-upload/` → `openspec/changes/archive/2026-09-04-fix-event-photo-upload/` via **`git mv`** (7 tracked files staged as renames; `verify-report.md` untracked rode along in the directory rename). Recursive pre-move snapshot (temp dir) compared against the archived tree; `archive-report.md` is additive and excluded (it did not exist in the source snapshot).

Verbatim `diff -r` readback (mandatory):

```
=== VERBATIM diff -r OUTPUT (archive move readback) ===
=== diff exit: 0 — EMPTY DIFF, byte-identical ===
```

Archived contents: `proposal.md`, `design.md`, `explore.md`, `tasks.md` (11/11 `[x]`, 0 unchecked), `apply-progress.md`, `verify-report.md`, `specs/{event-image-management,past-event-mutation-guard}/spec.md` + this report. Active `openspec/changes/` no longer contains the change. No `state.yaml` exists in this repo's change folders (consistent with prior archives).

## Decisions & Deviations (final)

1. **`git mv` used for the archive move** (repo precedent from `remove-organizer-delete-metrics`); the untracked `verify-report.md` moved along via the directory rename and remains untracked at the new path.
2. **PEM-002 matched by ID prefix, not heading** — repo convention (the delta renames the heading inside the MODIFIED block; precedent EHE-006). The **ED-001 clarification paragraph was preserved verbatim** (byte-identical between delta and canonical, verified by reading both).
3. **`(Previously: ...)` change-history note carried into the canonical spec** — current convention (established by the 2026-08-21 archive, superseding the older drop-them practice).
4. **Canonical Purpose "seven" → "six"** — one-word consistency normalization tied directly to the merged endpoint list; recorded here as intentional.
5. **Coverage-matrix row updated** for the merged requirement (precedent: EHE-006 matrix row updated in remove-organizer-delete-metrics).
6. **Archive date 2026-09-04** — orchestrator-specified; matches the date of the branch HEAD commit `98cfb0a` and of all change artifacts.
7. **Pre-existing test failures / W-2 / suggestions / secrets recorded as follow-ups, not resolved** — out of scope by product decision (secrets: would invalidate issued QR tickets and JWT sessions). The webhook-signature failures are security-relevant and flagged as high-priority follow-up.
8. **No migration/rollback exposure** — `ImageUrl` semantics unchanged; rollback is `git revert` of the 4 commits (or the PR as a whole). No DB or config changes.

## Engram Traceability

Artifacts read for this archive (hybrid mode per `openspec/config.yaml` `artifact_store: hybrid`):

| Artifact | Engram observation | Sync ID |
|----------|--------------------|---------|
| explore | #254 | `obs-57ab6d51f08ab68e` |
| proposal | #258 | `obs-d452a5cf4ebb4f4d` |
| spec | #259 | `obs-807206a34a39a9fd` |
| design | #261 | `obs-bbd7065787f69336` |
| tasks | #263 | `obs-7f44551dd86cc012` |
| apply | #265 | `obs-be52503f32793180` |
| verify-report | #267 | `obs-1c105adee7c21fcd` |

This archive report persisted as Engram topic `sdd/fix-event-photo-upload/archive-report` (hybrid mode), including the final-state facts, gates, spec-sync details, and follow-ups above.