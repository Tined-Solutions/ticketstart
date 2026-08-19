# Proposal: Brand Design System

## Intent

Frontend is dark-first, indigo/purple, has no category concept, and ships an unused logo — none match the approved brand decisions (vibrant 5-color Confetti palette, light-only MVP, festival display type, Home category chips). Rebase the design system onto the brand source of truth.

## Scope

### In Scope
- Token rebase: `tokens.css` → light-only brand palette (5 colors + Gris Oscuro `#4A4A4A` + 5 WCAG-AA dark variants, doc 2.4); semantic tokens remapped for compat
- Poppins (display) + Inter (body); rounded cards + pill buttons + soft inputs; Confetti surfaces
- Light-only: pin `useTheme` to light, remove ThemeToggle; `data-theme` kept for future dark mode (2.5)
- Logo `ticketera-logo.webp` in Navbar + Home
- Home hero: 5 frontend-only category chips + event grid
- Motion ≤300ms; reduced-motion preserved; update affected tests

### Out of Scope
- Checkout (Checkout→CheckoutReturn→CheckoutSuccess), roles/permissions + navbar role logic — regla de oro, untouched
- Backend (no category field/API) — documented debt; dark mode, PWA, i18n

## Capabilities

### New Capabilities
- `brand-design-system`: brand tokens, light-only theme, typography, geometry, Confetti surfaces, category taxonomy, logo, motion, WCAG-AA focus. Supersedes unarchived `frontend-redesign/design-system` delta (dark-first REQ-DS1–DS10).

### Modified Capabilities
- None — no frontend specs in `openspec/specs/`; frontend-redesign deltas never archived.

## Decisions

| # | Decision | Why |
|---|-----------|-----|
| 1 | Full token rebase | Only option satisfying brand 2.1/2.4/2.5/9; single source of truth |
| 2 | 5 frontend-only categories 1:1 with brand colors (Música=Naranja, Teatro=Púrpura, Deportes=Verde, Stand-up=Amarillo, Festivales=Cian) | Chip = brand-tint bg + dark-variant text (WCAG AA); backend unsupported = debt; Cultura deferred |
| 3 | Poppins display | Geometric, bold, "redondas y gruesas tipo afiche"; versatile for headings/logo/numbers. Baloo too round, Sora too technical |
| 4 | Logo in Navbar + Home | Replaces text-only wordmark; 62KB webp OK |
| 5 | Pin `useTheme` light, remove ThemeToggle | Honors 2.5; semantic tokens stay for future dark; tests updated to reality |
| 6 | WCAG AA via dark variants; brand colors never as normal text | Focus rings use dark variant; body = Gris Oscuro |

## Approach

Rewrite `tokens.css` first (`:root` light palette; drop dark-default + override blocks; keep `@theme inline` + semantic indirection). Cascade: `index.html` (Poppins, `data-theme="light"`), `useTheme`/`ThemeToggle`/`Navbar`, `Button`/`Card`/`GlassCard`/`Badge`/`FormField`/`Modal`, `GradientHero`+`Home`, `EventList`/`EventDetail`, `motion.js`. Add `src/data/categories.js`.

## Affected Areas

| Area | Impact |
|------|--------|
| `tokens.css` | Modified |
| `index.html`, `index.css` | Modified |
| `useTheme`, `ThemeToggle`, `Navbar` | Modified |
| `Button`/`Card`/`GlassCard`/`Badge`/`FormField`/`Modal`/`GradientHero` | Modified |
| `Home`/`EventList`/`EventDetail` | Modified |
| `src/data/categories.js` | New |
| `lib/motion.js` | Modified |
| `__tests__/` (useTheme, ThemeToggle, Navbar, css-migration, Card.glass, GlassCard, Button.*, Badge, EventList, EventDetail) | Modified |

## Risks

| Risk | Sev | Mitigation |
|------|-----|------------|
| Theme toggle removal breaks useTheme/ThemeToggle/Navbar tests | High | Rewrite in same change; pin light |
| css-migration/Card.glass/GlassCard/Button/Badge assertions break | Med | Update with rebase |
| Category chips: no backend support | Med | Frontend-only; decorative, no API filtering; debt |
| Brand hex as normal text → WCAG fail | Med | Dark-variant/Gris Oscuro only; review |
| Font swap restyles headings | Med | Visual-only; verify pages |
| Logo webp paint | Low | Preload; 62KB OK |

## Rollback Plan

Frontend-only, single branch. `git revert` restores dark-first `tokens.css` + `data-theme`. No migrations. Revert components before tokens if partial.

## Dependencies

- Google Fonts Poppins (CDN) or `@fontsource/poppins`; approved brand decisions doc.

## Success Criteria

- [ ] 5 brand colors + Gris Oscuro + 5 dark variants as tokens; `:root` light-only
- [ ] Poppins + Inter loaded; no Space Grotesk
- [ ] Home: 5 WCAG-AA category chips + event grid; Navbar: logo + wordmark, no toggle
- [ ] No raw brand hex as normal text; focus rings = dark variants; motion ≤300ms
- [ ] Frontend tests green; checkout + role tests + backend unchanged

## Next Steps

1. **sdd-spec** → new `specs/brand-design-system/spec.md` (Given/When/Then + RFC 2119)
2. **sdd-design** → token mapping table + component restyle + test-migration plan
3. **sdd-tasks** → phase tokens→shell→components→pages→tests; 400-line review forecast
