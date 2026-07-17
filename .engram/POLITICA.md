# Política de Contexto Compartido — Ticketstart

Esta política es **obligatoria** para todos los integrantes del equipo que trabajen con Gentle AI en este repositorio. Su objetivo es que Martín y Edgar compartan el contexto de sus sesiones de desarrollo sin necesidad de infraestructura cloud adicional.

---

## 🔑 Regla primordial: nombres de archivo

**El nombre en el archivo MD es de la persona que LO ELABORA, no a quién va dirigido.**

Ejemplo: `2026-07-17-martin.md` significa que Martín escribió ese summary. `2026-07-17-edgardo.md` significa que Edgar (o Edgardo) lo escribió. No importa si el contenido está dirigido al otro — el nombre refleja autoría.

---

## ⚠️ Antes de empezar a trabajar (cada sesión)

**El agente DEBE leer este archivo (`POLITICA.md`) al inicio de cada sesión donde se toca código.** Esto asegura que tenga contexto del equipo completo y no se olvide de redactar el session summary al terminar.

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

**Toda sesión en la que se toca código debe terminar con un session summary.** No importa si fue un fix chico, un feature grande, o solo diagnóstico — si se modificaron archivos o se tomaron decisiones técnicas, el summary es innegociable.

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
| Inicio de sesión | Leer `POLITICA.md` → `git pull` → leer summaries nuevos del compañero |
| Durante la sesión | `mem_save` local normal (no se comparte) |
| Fin de sesión | Escribir `.engram/sessions/YYYY-MM-DD-nombre.md` → commit → push |
| ⚠️ Sesión con código | **Session summary obligatorio.** Sin excepciones. |
