# Design: Brand Design System

## Technical Approach

Full token rebase onto the brand decisions doc. Rewrite `tokens.css` to a light-only `:root` (drop `[data-theme="light"]`/`[data-theme="dark"]` override blocks; keep `@custom-variant dark` + `data-theme="light"` on `<html>` for future dark). Cascade: tokens → `index.html`/`index.css` → `useTheme`/`ThemeToggle`/`Navbar` → `Button`/`Card`/`GlassCard`/`Badge`/`FormField`/`Modal` → `GradientHero`/`Home` → `EventList`/`EventDetail` → `motion.js` + tests. Add `src/data/categories.js` + shared `EventCard`. Implements REQ-BDS-1..12. No backend, checkout, or role-logic changes (REQ-BDS-12).

## Token Architecture

Raw palette → dark variants → semantic remap → component tokens. Semantic indirection preserves legacy `--primary`/`--accent`/`--brand-1`/`--surface`/`--text-*` consumers without per-component edits.

```
Raw (surfaces/gradient)        Dark variant (text/btn/focus)   Semantic (compat)
--naranja:  #F78B2D   →        --naranja-dark:  #B45309   →    --primary, --brand-2
--amarillo: #F5C01F   →        --amarillo-dark: #6B5300
--verde:    #67CF65   →        --verde-dark:    #166534
--cian:     #18C8DB   →        --cian-dark:     #0B6170
--purpura:  #B65DC2   →        --purpura-dark:  #6A2176   →    --accent, --brand-1
--gris-oscuro: #4A4A4A                                     →    --text-1, --text-2 base
```

## Token Mapping

| New token (`--*` / `--color-*`) | Value | Replaces |
|---|---|---|
| `--naranja` | `#F78B2D` | new (surface) |
| `--amarillo` | `#F5C01F` | new |
| `--verde` | `#67CF65` | new |
| `--cian` | `#18C8DB` | new |
| `--purpura` | `#B65DC2` | `--brand-1` surface `#7c3aed` |
| `--gris-oscuro` | `#4A4A4A` | `--text-1` `#f8fafc` |
| `--naranja-dark` | `#B45309` | `--primary` `#4f46e5`; `--brand-2` `#a855f7` |
| `--amarillo-dark` | `#6B5300` | new |
| `--verde-dark` | `#166534` | new |
| `--cian-dark` | `#0B6170` | new |
| `--purpura-dark` | `#6A2176` | `--accent` `#c084fc`; `--brand-1` `#7c3aed` |
| `--primary-hover` | `#8F4208` | `--primary-hover` `#4338ca` |
| `--accent-hover` | `#5A1B64` | `--accent-hover` `#a855f7` |
| `--accent-bg` | `rgba(182,93,194,0.12)` | (was undefined) |
| `--canvas` | `#FFFFFF` | `#0a0a0f` |
| `--surface` | `#FFFFFF` | `#1a1a2e` |
| `--surface-elevated` | `#F5F5F5` | `#1e293b` |
| `--text-1` | `#4A4A4A` (Gris Oscuro) | `#f8fafc` |
| `--text-2` | `#6B6B6B` | `#94a3b8` |
| `--text-muted` | `#9CA3AF` | `#64748b` |
| `--text-h` | `#1A1A1A` | `#f3f4f6` |
| `--glass-bg` | `rgba(255,255,255,0.7)` | `rgba(26,26,46,0.55)` |
| `--glass-border` | `rgba(0,0,0,0.08)` | `rgba(255,255,255,0.08)` |
| `--font-display` | `"Poppins", ui-sans-serif` | `"Space Grotesk"` |
| `--radius-glass` | `1.25rem` | `1rem` |
| `--radius-card` | `1.25rem` | new (cards) |
| `--radius-pill` | `9999px` | new (buttons) |
| `--radius-input` | `0.5rem` | new (soft inputs) |
| `--dur-micro` | `150ms` | `200ms` |
| `--dur-normal` | `250ms` | `400ms` (violated ≤300) |
| `--dur-slow` | `300ms` | `600ms` |

`@theme inline` exposes every `--color-*` (brand + dark + gris-oscuro) so `bg-naranja/15`, `text-purpura-dark`, `bg-cian/20` resolve in Tailwind v4. `:root` is light-only; `[data-theme="light"]`/`[data-theme="dark"]` override blocks DELETED.

## Component Restyle Plan

| Component | Changes |
|---|---|
| `Button` | sizeClasses → all `rounded-full` (pill); `primary` `bg-primary`(naranja-dark)+`hover:bg-primary-hover`(darker, no opacity); `secondary` `bg-primary/10 text-primary hover:bg-primary/20`; `gradient` `from-brand-1 to-brand-2`(purpura→naranja dark)+`hover:brightness-95`; `glass` `bg-white/60 border-gris-oscuro/10 text-gris-oscuro hover:bg-white/80`; focus `ring-primary` |
| `Card` | non-glass `rounded-[var(--radius-card)]`; header `font-display` (was `font-heading`) |
| `GlassCard` | no code change (tokens cascade: light glass-bg, radius 1.25rem) |
| `Badge` | drop `dark:`; success `bg-verde/15 text-verde-dark`; warning `bg-amarillo/15 text-amarillo-dark`; info `bg-cian/15 text-cian-dark`; error stays `bg-rose-100 text-rose-700` (no brand red) |
| `FormField` | `rounded-[var(--radius-input)]`; `text-gris-oscuro`; focus `ring-primary/25`; label `text-gris-oscuro` |
| `Modal` | `rounded-[var(--radius-card)]`; `text-gris-oscuro`; close btn `ring-primary` |
| `Navbar` | remove `ThemeToggle` (desktop+mobile); logo `<img src="/ticketera-logo.webp" class="h-8 w-auto">` + wordmark `font-display font-bold text-gris-oscuro` (drop indigo gradient clip); active NavLink `text-brand-1 bg-brand-1/10` (purpura dark, AA) |
| `GradientHero` | light Confetti hero: drop dark overlay default; title Poppins bold Gris Oscuro; new `chips` + `logo` props; subtitle `text-text-2` |
| `Home` | `<GradientHero logo chips={categories} cta=/>`; replace 4-feature section with featured events grid via `useEvents()` + shared `EventCard` (first 6) + "Ver todos" link |
| `EventList` | import shared `EventCard`; link `rounded-[var(--radius-card)]`; `font-display` headings |
| `EventDetail` | `font-heading`→`font-display`; `text-brand-1`/`border-brand-1`/`ring-brand-1` (purpura dark, AA); image hero keeps dark overlay + `text-white` (image provides contrast); `Button variant="gradient"` pill |
| `index.css` | `button { border-radius: var(--radius-pill) }`; `.button-primary { background: var(--primary) }` + `:hover { background: var(--primary-hover) }` (no opacity); `.button-secondary` uses `--accent-light`/`--accent`; `.form-group input { border-radius: var(--radius-input) }`; `h1,h2 { font-weight: 700 }` (Poppins bold) |

## Home Hero Design

```
┌───────────────────────────────────────┐
│  [logo webp]  TicketStart (Poppins)   │
│   La plataforma mas simple para...    │
│   (Música)(Teatro)(Deportes)          │  chips: bg tint + dark-variant text
│   (Stand-up)(Festivales)              │
│   [ Ver catalogo de eventos → ]       │  gradient pill CTA → /events
├───────────────────────────────────────┤
│   Eventos destacados                  │
│   [EventCard][EventCard][EventCard]   │  featured grid (useEvents, first 6)
│   [EventCard][EventCard][EventCard]   │
│   Ver todos →                          │
└───────────────────────────────────────┘
```

Chip pattern (WCAG AA, REQ-BDS-7): `bg-{colorKey}/15 text-{colorKey}-dark rounded-full px-4 py-1.5 font-medium text-sm`. Tint bg (15% brand) + dark-variant text (≥4.5:1 on white). Chips are `<span>` (decorative, no click, no API — REQ-BDS-8).

## Categories (`src/data/categories.js`)

```js
export const categories = [
  { id: 'musica',     label: 'Música',     colorKey: 'naranja',  hex: '#F78B2D', darkHex: '#B45309' },
  { id: 'teatro',     label: 'Teatro',     colorKey: 'purpura',  hex: '#B65DC2', darkHex: '#6A2176' },
  { id: 'deportes',   label: 'Deportes',   colorKey: 'verde',    hex: '#67CF65', darkHex: '#166534' },
  { id: 'standup',    label: 'Stand-up',   colorKey: 'amarillo', hex: '#F5C01F', darkHex: '#6B5300' },
  { id: 'festivales', label: 'Festivales', colorKey: 'cian',     hex: '#18C8DB', darkHex: '#0B6170' },
]

export const chipClass = {
  naranja:  'bg-naranja/15 text-naranja-dark',
  purpura:  'bg-purpura/15 text-purpura-dark',
  verde:    'bg-verde/15 text-verde-dark',
  amarillo: 'bg-amarillo/15 text-amarillo-dark',
  cian:     'bg-cian/15 text-cian-dark',
}
```

Static class strings (Tailwind JIT detects them). Consumed by `GradientHero` chips. No backend call, no filtering.

## Light-Only

`useTheme.jsx`: `readStoredTheme` returns `'light'` always; `toggle`/`setTheme` are no-ops (state fixed to `'light'`). `ThemeProvider` still applies `data-theme="light"` to `<html>` on mount (preserves context for future dark — brand 2.5). `index.html` FOUC guard: `var t = 'light'; document.documentElement.setAttribute('data-theme', t)` (ignores localStorage, keeps guard mechanism for future dark). `data-theme="light"` retained (REQ-BDS-5 scenario 2). `ThemeToggle.jsx` + its test DELETED. Navbar stops importing it.

## Logo

`ticketera-logo.webp` (62KB, `public/`) → `/ticketera-logo.webp`. Navbar: `<img src="/ticketera-logo.webp" alt="" width="32" height="32" class="h-8 w-auto">` + `<span class="font-display font-bold text-xl text-gris-oscuro">TicketStart</span>` in an `inline-flex items-center gap-2`. Home hero: `logo` prop renders `h-12` (48px) above the title. `index.html`: `<link rel="preload" as="image" href="/ticketera-logo.webp">`.

## Motion

`--dur-micro: 150ms`, `--dur-normal: 250ms`, `--dur-slow: 300ms` (all ≤300, REQ-BDS-9). `motion.js` `DUR` mirrors: `0.15 / 0.25 / 0.3`. `prefers-reduced-motion` already handled (index.css media query + `useReducedMotion` hook) — preserved. Framer `whileHover`/`whileTap` transitions ≤200ms.

## Testing Strategy

| Layer | What | How |
|---|---|---|
| Unit | tokens render brand hex; light-only `useTheme`; Button pill + dark-variant hover; Badge brand variants; categories shape | vitest + @testing-library; `npm test` |
| Unit | css-migration: `--color-naranja`, `Poppins`, no `Space Grotesk` | `readFileSync` guards |
| Integration | Home renders 5 chips + featured grid; Navbar logo + no toggle; EventList/EventDetail use shared `EventCard` | vitest + `MemoryRouter` + `renderWithQueryClient` |

### Test-Migration Plan

| Test | Action | New assertions |
|---|---|---|
| `useTheme.test.jsx` | Rewrite | default `'light'`; toggle no-op; `data-theme` stays `light` |
| `ThemeToggle.test.jsx` | Delete | — |
| `Navbar.test.jsx` | Modify | drop `useTheme` mock/toggle; add `src="/ticketera-logo.webp"`; assert no toggle button |
| `css-migration.test.js` | Modify | `toContain('--color-naranja')`, `toContain('Poppins')`, `not.toContain('Space Grotesk')` |
| `Card.glass.test.jsx` | Modify | add `rounded-[var(--radius-card)]` |
| `GlassCard.test.jsx` | Keep | — (tokens cascade) |
| `Button.test.jsx` | Modify | add `rounded-full` pill |
| `Button.variants.test.jsx` | Rewrite | gradient `from-brand-1 to-brand-2`; glass `bg-white/60` |
| `Badge.test.jsx` | Rewrite | `bg-cian/15 text-cian-dark` (info), `bg-verde/15` (success), `bg-amarillo/15` (warning) |
| `EventList.test.jsx` | Keep | text/role assertions unchanged (no class checks) |
| `EventDetail.test.jsx` | Modify | if `font-heading` asserted → `font-display`; ticket row class unchanged |
| `accessibility.test.jsx` | Review/Modify | update if `--accent`/contrast asserted |
| `events/__tests__/EventCard.test.jsx` | Create | shared component: link, image, price, date badge |

Command: `npm test` (fallback `npx vitest run`).

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|---|---|---|---|
| 1 | Token rebase | Full `:root` light-only rewrite | Additive layer; targeted hex swap | Brand 2.5 + single source of truth; only option satisfying 2.1/2.4/2.5/9 |
| 2 | Display font | Poppins | Baloo (too round), Sora (too technical) | Geometric bold "afiche de festival" per brand 9; versatile for headings/logo/numbers |
| 3 | `useTheme` | Pin light, keep Provider, no-op toggle | Delete hook | Preserves future dark mode (2.5); minimizes consumer blast radius |
| 4 | `ThemeToggle` | Delete + delete test | Hide | Unused artifact; clean removal |
| 5 | Categories | Frontend-only `categories.js`, decorative `<span>` chips | Backend Category model/API | Backend out of scope (regla de oro); no filtering; debt documented |
| 6 | Brand tokens | Raw palette + separate dark-variant tokens | Single token set | Brand 2.4 "variants are additional tokens"; raw for surfaces, dark for text/btn |
| 7 | Semantic remap | `--primary`/`--accent`/`--brand-1/2` → dark variants | Rename tokens | Legacy compat: Button/Toast/Navbar/EventList/EventDetail unchanged structurally |
| 8 | Button shape | `rounded-full` for all sizes | Per-variant radius | Brand 9 "botones pill"; uniform |
| 9 | Home hero | Rework `GradientHero` (chips + logo props) | New `HomeHero` component | Reuses existing single-consumer component |
| 10 | EventCard | Extract to `src/components/events/EventCard.jsx` | Duplicate in Home | DRY; shared by EventList + Home featured grid |

## Data Flow

```
categories.js ─┐
               ├→ GradientHero ─→ Home ─→ useEvents() ─→ EventCard[]
index.html ────→ tokens.css ─→ @theme inline ─→ Tailwind utilities
                                    ↓
              Button/Card/Badge/FormField/Modal/Navbar/EventList/EventDetail
```

## File Changes

| File | Action |
|---|---|
| `frontend/src/tokens.css` | Modify |
| `frontend/index.html` | Modify |
| `frontend/src/index.css` | Modify |
| `frontend/src/hooks/useTheme.jsx` | Modify |
| `frontend/src/components/layout/ThemeToggle.jsx` | Delete |
| `frontend/src/components/layout/__tests__/ThemeToggle.test.jsx` | Delete |
| `frontend/src/components/layout/Navbar.jsx` | Modify |
| `frontend/src/components/Button.jsx` | Modify |
| `frontend/src/components/Card.jsx` | Modify |
| `frontend/src/components/ui/GlassCard.jsx` | Modify (no-op, tokens cascade) |
| `frontend/src/components/ui/Badge.jsx` | Modify |
| `frontend/src/components/ui/GradientHero.jsx` | Modify |
| `frontend/src/components/FormField.jsx` | Modify |
| `frontend/src/components/Modal.jsx` | Modify |
| `frontend/src/pages/Home.jsx` | Modify |
| `frontend/src/pages/EventList.jsx` | Modify |
| `frontend/src/pages/EventDetail.jsx` | Modify |
| `frontend/src/components/events/EventCard.jsx` | Create |
| `frontend/src/components/events/__tests__/EventCard.test.jsx` | Create |
| `frontend/src/data/categories.js` | Create |
| `frontend/src/lib/motion.js` | Modify |
| `frontend/src/hooks/__tests__/useTheme.test.jsx` | Rewrite |
| `frontend/src/components/layout/__tests__/Navbar.test.jsx` | Modify |
| `frontend/src/lib/__tests__/css-migration.test.js` | Modify |
| `frontend/src/components/__tests__/Card.glass.test.jsx` | Modify |
| `frontend/src/components/__tests__/Button.test.jsx` | Modify |
| `frontend/src/components/__tests__/Button.variants.test.jsx` | Rewrite |
| `frontend/src/components/ui/__tests__/Badge.test.jsx` | Rewrite |
| `frontend/src/pages/EventList.test.jsx` | Modify (minor) |
| `frontend/src/pages/EventDetail.test.jsx` | Modify (minor) |
| `frontend/src/components/__tests__/accessibility.test.jsx` | Review/Modify |

~31 files (4 new, 2 deleted, rest modified). Exceeds 400-line review budget → chained PRs recommended (tasks phase forecasts).

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

No data migration. Single feature branch; `git revert` restores dark-first `tokens.css` + `data-theme="dark"`. Revert components before tokens if partial. No feature flags (frontend-only).

## Open Questions

- [ ] `--primary-hover` exact hex (`#8F4208` is an estimated darker step below naranja-dark `#B45309`) — confirm or derive via contrast check in apply.
- [ ] `accessibility.test.jsx` — need to read before apply to know whether it asserts specific contrast ratios or only focus-ring presence.
- [ ] Home featured grid: first 6 from `useEvents()` vs a dedicated "featured" flag — backend has no featured field, so first-6 is the MVP choice; confirm acceptable.
