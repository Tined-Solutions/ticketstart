# Política de Contexto Compartido — Ticketstart

Esta política es **obligatoria** para todos los integrantes del equipo que trabajen con Gentle AI en este repositorio. Su objetivo es que Martín y Edgar compartan el contexto de sus sesiones de desarrollo sin necesidad de infraestructura cloud adicional.

---

## ⚠️ Antes de empezar a trabajar (cada sesión)

```bash
git pull origin dev
ls .engram/sessions/
```

Leer los summaries del compañero que no se hayan leído todavía. Esto te pone al día con decisiones de arquitectura, bugs descubiertos, patrones establecidos y trabajo completado por el otro.

---

## 🚫 Durante la sesión (cuando el agente guarda en Engram)

Tu agente (Gentle AI) guarda observaciones en **tu Engram local** (`mem_save`) automáticamente. Eso está bien y debe seguir haciéndolo. Esas observaciones **no se comparten automáticamente** — solo se comparte lo que explícitamente escribas en `.engram/sessions/` al finalizar.

---

## 📝 Al terminar la sesión (OBLIGATORIO)

Antes de cerrar la sesión, escribir un summary en:

```
.engram/sessions/YYYY-MM-DD-nombre.md
```

Ejemplo: `.engram/sessions/2026-07-13-martin.md`

### Formato del summary

```markdown
# Sesión — YYYY-MM-DD — [Nombre]

## Objetivo
[Qué se estaba construyendo/trabajando en esta sesión]

## Instrucciones
[Preferencias o restricciones del usuario descubiertas — omitir si ninguna]

## Descubrimientos
- [Hallazgo técnico, gotcha o aprendizaje 1]
- [Hallazgo técnico 2]

## Completado
- ✅ [Tarea completada — con detalles clave de implementación]
- ✅ [Otra tarea — mencionar archivos modificados]

## Próximos pasos
- [Lo que queda por hacer para la próxima sesión]

## Archivos relevantes
- ruta/al/archivo.ts — [qué hace o qué cambió]
```

### Commiteo y push

```bash
git add .engram/sessions/
git commit -m "docs(engram): session summary — [breve descripción]"
git push origin dev
```

---

## 🔧 Configuración para Edgar (una sola vez)

Edgar debe asegurarse de que su agente (Gentle AI / OpenCode) siga esta misma política. Las instrucciones para el agente son:

> Al iniciar una sesión en el proyecto `ticketstart`, hacer `git pull` y leer los summaries en `.engram/sessions/` que no haya leído todavía. Al terminar la sesión, escribir un summary en `.engram/sessions/YYYY-MM-DD-edgar.md` usando el formato de esta política, commitearlo y pushearlo. Esto es obligatorio — no cerrar la sesión sin hacerlo.

Recomendación: agregar esto como un recordatorio en el `AGENTS.md` o en la configuración de OpenCode de Edgar para que el agente lo tenga siempre presente.

---

## 📋 Resumen rápido

| Momento | Acción |
|---------|--------|
| Inicio de sesión | `git pull` → leer summaries nuevos del compañero |
| Durante la sesión | `mem_save` local normal (no se comparte) |
| Fin de sesión | Escribir `.engram/sessions/YYYY-MM-DD-nombre.md` → commit → push |
