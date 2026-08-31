---
name: design-system
description: "Trigger: design system, Tailwind token, theme, motion, styling, glass. Apply the TICKETSTART brand design tokens and styling conventions."
license: Apache-2.0
metadata:
  author: gentleman-programming
  version: "1.0"
---

## Activation Contract

Load when styling UI, adding tokens, or animating with the design system.

## Hard Rules

- Tailwind v4; tokens live in `frontend/src/tokens.css` (`@theme inline` bridging CSS custom props). No BEM, no raw arbitrary colors.
- Use semantic tokens (`text-1`, `text-2`, `text-muted`, `canvas`, `surface`, `brand-1`, `brand-2`, `glass-bg`) — never hardcoded hex in JSX.
- Light-only app (brand 2.5): `useTheme` pins `data-theme="light"` on `<html>`; its toggle/setTheme are no-ops. Do not build dark-mode variants.
- Motion via `lib/motion.js` presets (`fadeIn`, `fadeInUp`, `staggerContainer`) and `DUR`/`EASE` tokens; respect reduced motion.
- Typography: `font-display` (Poppins) for headings/display text, `font-sans` (Inter) for body.

## Decision Gates

| Need | Token / mechanism |
|------|-------------------|
| Elevated surface | `glass-surface` / `GlassCard` |
| Text hierarchy | `text-text-1` / `text-2` / `text-muted` |
| Brand accent | `brand-1` / `brand-2` |
| Entrance animation | `motion.js` preset |

## Execution Steps

1. Find the existing token for the value.
2. Add a token only if none exists (in `tokens.css`).
3. Use the matching `motion.js` preset instead of inline transitions.

## Output Contract

Return the tokens/presets used and any new token added.

## References

- `frontend/src/tokens.css`, `frontend/src/lib/motion.js`, `frontend/src/hooks/useTheme.jsx`.
- `ticketstart-brand-decisions.md` — TICKETSTART brand palette and identity decisions (supersedes the old "Modern Elegance" tokens).
