# Skills de Ticketera Online

Convenciones del equipo codificadas como skills LLM-first, compartidas por git.

## Qué son

Cada carpeta es una skill (un contrato de instrucciones en runtime para el AI) que codifica convenciones reales del proyecto:

| Skill | Área |
| --- | --- |
| `dotnet-testing` | Tests backend (.NET): xUnit, Moq, FsCheck, TDD estricto |
| `aspnet-api-design` | Estructura de endpoints: controller→service, ProblemDetails, políticas |
| `efcore-data` | EF Core: migraciones, N+1, AsNoTracking, conexiones |
| `backend-security` | Auth, CSRF, HMAC, rate limiting, redacción de PII |
| `react-testing` | Tests frontend: vitest + Testing Library |
| `a11y` | Accesibilidad: ARIA, foco, live regions, reduced motion |
| `react-patterns` | Estructura de componentes, hooks, context |
| `design-system` | Tokens de "Modern Elegance", motion, theming |
| `ui-review` | Revisión visual: estados empty/loading/error, responsive |

## Instalar localmente

Las skills se cargan desde `~/.config/opencode/skills/` (directorio de usuario, no versionado). Para activarlas en tu máquina después de `git pull`:

**Linux/WSL:**
```bash
cp -r skills/* ~/.config/opencode/skills/
gentle-ai skill-registry refresh --force
```

**Windows (PowerShell):**
```powershell
Copy-Item -Path skills\* -Destination $env:USERPROFILE\.config\opencode\skills\ -Recurse -Force
gentle-ai skill-registry refresh --force
```

> El `skill-registry refresh` regenera `.atl/skill-registry.md` con las rutas locales de tu máquina. No commitees ese archivo con rutas de otra máquina sin regenerarlo primero.

## Convención

- `SKILL.md` es la fuente de verdad; el registry solo indexa.
- Para crear/editar skills, usar la skill `skill-creator` y su guía de estilo (`references/skill-style-guide.md`).
- Tras cambios, correr `gentle-ai skill-registry refresh --force`.
