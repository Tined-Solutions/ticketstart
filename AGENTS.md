# AGENTS.md — Guía del repositorio para agentes

Ticketera de eventos full-stack: API ASP.NET Core 9 (`backend/`) + SPA React 19/Vite (`frontend/`), con PostgreSQL (Supabase), Cloudflare R2 (imágenes), pagos Mercado Pago, entradas con QR firmado (HMAC) y envío de emails vía Resend.

## Fuentes canónicas de verdad

| Fuente | Contenido |
|--------|-----------|
| `openspec/specs/` | Especificaciones de comportamiento por capacidad. Se sincronizan al **archivar** un cambio SDD, no antes |
| `skills/` | Convenciones del repo: backend (`aspnet-api-design`, `backend-security`, `efcore-data`), frontend (`react-patterns`, `design-system`), testing (`dotnet-testing`, `react-testing`), accesibilidad y UI (`a11y`, `ui-review`) |

El `README.md` de la raíz y `backend/AUTHORIZATION_MATRIX.md` son **resúmenes**. Cuando un documento discrepe con el código, gana el código y se corrige el documento.

## Comandos de desarrollo y test

```bash
# Backend — suite completa (xUnit + FsCheck)
cd backend && dotnet test

# Frontend — Vitest (npm test envuelve scripts/wsl-test.sh, que hace mirror a
# ext4 automáticamente cuando el repo vive en un montaje lento de WSL)
cd frontend && npm test
```

## Gotchas conocidos

- **Proxy de dev:** Vite apunta al backend en el puerto **5193** (auto-detección de gateway en WSL; override con `VITE_API_TARGET` en `frontend/.env`).
- **Consulta de entradas:** `GET /api/tickets/lookup` exige **email + DNI** — devuelve 400 si falta el DNI.
- **Autenticación:** JWT en **cookie httpOnly + header `X-CSRF-PROTECT`** en mutaciones. No uses headers `Authorization: Bearer`. Detalles en `backend/AUTHORIZATION_MATRIX.md`.
- **Eliminación de eventos: solo Admin.** Refleja un merge pendiente (PR de cambio de organizador); en ramas previas el guard del servicio aún permite también al dueño.
- **Disciplina de specs:** al archivar un cambio SDD, sincronizar los deltas a `openspec/specs/` antes de cerrar. Las specs canónicas no se editan a mano por fuera de ese flujo.

## Artefactos SDD

- Cambios activos (propuesta, specs delta, diseño, tareas): `openspec/changes/<change>/`.
- Cambios archivados: `openspec/changes/archive/<YYYY-MM-DD>-<change>/`.
- Resúmenes históricos de la build original: `openspec/changes/archive/task-completion-summaries/` (solo provenance; supersados por openspec).
