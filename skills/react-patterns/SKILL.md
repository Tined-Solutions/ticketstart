---
name: react-patterns
description: "Trigger: React component, hook, context, atomic design, component pattern. Apply Ticketera's React structure and component conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when adding or changing React components, hooks, context, or structure.

## Hard Rules

- `export default function ComponentName({ ... })` with `className = ''` + `...rest`; merge className via template literal (no clsx).
- Polymorphic components use `as: Component = 'div'`.
- Place files by role: `components/ui/` (primitives), `components/layout/`, `pages/` (routes).
- Custom hooks in `hooks/` (`useXxx`); server state via `@tanstack/react-query` with keys in `lib/queryKeys.js`.
- App-wide state via context in `context/`; expose via hooks (`useToast`) rather than raw `useContext`.
- Formatting/utilities in `lib/` (`format.js`, `apiError.js`).

## Decision Gates

| Need | Location |
|------|----------|
| Reusable UI primitive | `components/ui/` |
| Route screen | `pages/` |
| Shared layout | `components/layout/` |
| Cross-cutting logic | `hooks/` |
| App-wide state | `context/` |

## Execution Steps

1. Pick the correct location from the table.
2. Follow the component contract (default export, `className`, `...rest`).
3. Add a test in the matching `__tests__/` folder.

## Output Contract

Return file path(s) and the pattern applied.

## References

- `frontend/src/components/ui/GlassCard.jsx`, `frontend/src/hooks/useTheme.jsx`, `frontend/src/context/AuthProvider.jsx`.
