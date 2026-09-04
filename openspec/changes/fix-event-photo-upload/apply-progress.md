# Apply Progress: Fix Event Photo Upload (R2 TLS + Honest Atomic Save Flow)

Branch `fix/r2-upload-linux-tls` · Change `fix-event-photo-upload` · Hybrid artifact store (OpenSpec + Engram topic `sdd/fix-event-photo-upload/apply-progress`) · Delivery: single-pr con `size:exception` aprobado (~1000 líneas < budget 1100).

## Estado por tarea

| # | Tarea | Estado | Evidencia |
|---|-------|--------|-----------|
| 1.1 | R2 TLS: sin forcing + comentario con evidencia | ✅ hecho | `R2StorageClientTests` 2/2 (reflection: `SslOptions.EnabledSslProtocols == None`); forcing removido; comentario documenta `sslv3 alert handshake failure` / `0A000410` |
| 1.2 | `POST /api/uploads/event-image` + policy rate limit | ✅ hecho | `UploadsControllerTests` 9/9: 200 organizer/Admin, 401, 403 Staff, 400 CSRF/MIME/size/missing-part, 429 en la 11ª llamada |
| 2.1 | Cleanup EIM-005 en `UpdateEventAsync` | ✅ hecho | `EventServiceTests` 49/49: reemplazo borra old, same-URL no borra, `""` limpia+borra, null preserva, delete-failure → 200 |
| 2.2 | Propiedad FsCheck del invariante | ✅ hecho | `ImageStoragePropertyTests` 35/35: delete llamado iff old non-empty ∧ new non-null ∧ old ≠ new (generator `R2ImageUrlArb`) |
| 2.3 | Remoción `ReplaceEventImageAsync` + endpoint viejo | ✅ hecho | 4 sites reescritos contra `UpdateEventAsync`; `EventServiceImmutabilityTests` región PEM reescrita; `EventControllerTests` test viejo eliminado |
| 2.4 | Ruta vieja → 404 | ✅ hecho | `UploadsControllerTests.OldEventImageRoute_Returns404` (RED contra ruta viva → GREEN tras remoción) |
| 3.1 | EventForm upload-first create + errores honestos | ✅ hecho | `EventForm.test.jsx` 26/26: upload-first, bloqueo con alert rojo, falso éxito eliminado, labels "Subiendo imagen…"/"Guardando…" |
| 3.2 | EventForm edit + photo | ✅ hecho | `EventForm.edit.test.jsx` 5/5: PUT lleva nueva URL, upload-fail bloquea PUT, sin foto preserva `initialData.imageUrl` |
| 4.1 | Swap probe integración revoked-owner | ✅ hecho | `AdminUserManagementIntegrationTests` → `POST /api/uploads/event-image` 403 + ruta vieja 404 (live-DB; compila y correcto) |
| 4.2 | Sync `AUTHORIZATION_MATRIX.md` | ✅ hecho | Ruta vieja fuera de `EventController`; fila `UploadsController`; política `RequireOrganizadorRole` incluye upload |
| 4.3 | Suites completas | ✅ parcial | Backend 749 ✅ + 4 pre-existentes ❌ (ver abajo); Frontend 505/505 ✅ |

## Resultados de suites completas

- **Backend** `dotnet test` (cwd backend): **749 passed / 4 failed / 753 total**.
  - Los 4 fallos son **PRE-EXISTENTES en la rama** (confirmado ejecutándolos contra `6f4fe27` en worktree con el `appsettings.Development.json` real — fallan igual en base): `PaymentControllerTests.Webhook_InvalidSignature_ReturnsUnauthorized`, `PaymentPropertyTests.Property17_InvalidSignature_ReturnsUnauthorized`, `PendingEmailRetryTests.RetryPendingEmailsAsync_Exhaustion_MarksExhausted`, `AuthCookieIntegrationTests.CsrfMiddleware_AllowsWebhook_WithoutHeader`. Coinciden con el riesgo listado en proposal.md. No fueron tocados por este cambio (los 3 primeros son tests live-DB/lógica de webhook; el de CSRF depende de `HasLiveDatabase()` y es order/env-sensitive). Reportados, no arreglados (fuera de alcance).
- **Frontend** `npx vitest run --pool=forks --maxWorkers=1` (cwd frontend; `npm test` requiere WSL bash no disponible en win32): **48 files / 505 tests / 0 fallos**.

## Notas / descubrimientos

- `HttpClient._handler` es un campo PRIVADO de la base `HttpMessageInvoker` — `GetField("_handler", Instance|NonPublic)` sobre `HttpClient` devuelve null; el helper de `R2StorageClientTests` camina la jerarquía (`FindInstanceField`).
- El test de CSRF webhook pasa "en falso" en worktrees sin `appsettings.Development.json` (early return por `HasLiveDatabase()`) — el falso positivo al validar la base se descartó copiando el appsettings real.
- ADR-6 descubrimiento confirmado: `UseRateLimiter` corre antes de `UseAuthentication` → el partitioner ve `context.User` vacío → particiones efectivas por IP (documentado en Program.cs y design.md como follow-up).
- Runner frontend canónico (`npm test` → wsl-test.sh) no disponible en win32; el runner equivalente usado es `vitest run --pool=forks --maxWorkers=1`.

## Commits

| SHA | Mensaje |
|-----|---------|
| `47c3ae4` | feat(backend): event-agnostic image upload endpoint with OS-default TLS |
| `909e427` | refactor(backend): move old-image cleanup into UpdateEventAsync, remove legacy image endpoint |
| `3d7d34b` | feat(frontend): upload-first image flow with honest errors in EventForm |
| (pendiente) | test(backend): swap revoked-owner probe to uploads endpoint; docs: sync authorization matrix |

## Pendiente / siguiente fase

- `sdd-verify` (todo el cambio implementado; verificación formal de specs/design/tareas).
- Follow-ups registrados: rate-limit por usuario real (reordenar pipeline), secrets rotación, orphans R2 (sweeper), 4 fallos pre-existentes.