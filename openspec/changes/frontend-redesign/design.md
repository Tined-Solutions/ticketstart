# Design: Frontend Redesign — "Modern Elegance"

## Technical Approach

Replace the 1369-line legacy BEM layer in `index.css` with a Tailwind v4 `@theme` token system driven by `data-theme="dark|light"` on `<html>`. Add `framer-motion` for route transitions, scroll reveals, and micro-interactions (full by default, `prefers-reduced-motion` opt-out). Introduce a `Layout` shell (`Navbar` + `<main>` + `Footer` + `AnimatePresence`) wrapping every route. Restyle all public pages (Phase 2) to consume new primitives (`GradientHero`, `GlassCard`, `Badge`, `Skeleton`, `EmptyState`) and extended `Button` variants (`glass`, `gradient`). Phase 3 covers organizer/admin; Phase 4 strips legacy BEM; Phase 5 polishes + visual regression tests. Maps to proposal phases and REQ-DS1..DS10, REQ-L1..L8, REQ-P1..P10, REQ-FQ-V1..V4.

## Architecture Overview

```
index.html  (inline theme script → sets data-theme before paint)
     │
     ├── main.jsx  (BrowserRouter → AuthProvider → ToastProvider → App)
     │
     └── App.jsx
            └── Layout  (Navbar + AnimatePresence<main> + Footer)
                  ├── Navbar  (useAuth, useTheme, useReducedMotion, scroll-aware)
                  ├── <main><AnimatePresence mode="wait">{route}</AnimatePresence></main>
                  └── Footer
```

Tokens (single source of truth in `index.css`) cascade to every component via Tailwind utilities (`bg-surface`, `text-secondary`, `bg-brand-gradient`). No component hard-codes a hex value.

## Component Tree

```
Layout
├── Navbar
│    ├── Brand/Wordmark ("Ticketera" — Space Grotesk + brand gradient)
│    ├── NavLinks (Events, My Tickets) — hidden <768px → hamburger drawer
│    ├── ThemeToggle (icon button → useTheme)
│    └── AuthSlot → Login link (guest) | UserMenu (avatar + logout) (authed)
└── Footer
AnimatePresence(mode="wait", key=location.pathname)
  └── <Page/>  (each page replaces BEM-rooted divs with GlassCard/GradientHero/etc.)
```

## CSS / Theme Architecture

**Decision: `data-theme` attribute + `@theme inline` (NOT `darkMode` config).**

| Option | Tradeoff | Decision |
|---|---|---|
| `@theme` static tokens only | Cannot re-pivot by attribute — colors baked at build | Rejected |
| `darkMode:"selector"` + `dark:` utilities | Works, but doubles the authoring surface (`bg-surface dark:bg-surface`) | Rejected as primary |
| `:root`/`[data-theme]` custom props + `@theme inline` bridge | One authoring site per token; attribute switch repaints in one frame; `dark:` variant still available | **Chosen** |

```css
@import "tailwindcss";
@custom-variant dark (&:where([data-theme=dark], [data-theme=dark] *));

:root {                     /* default = dark (satisfies REQ-DS1 "no attribute → dark") */
  --canvas: #0a0a0f;  --surface: #1a1a2e;  --text-1: #f8fafc;  --text-2: #94a3b8;
  --brand-1: #7c3aed;  --brand-2: #a855f7;  --secondary: #06b6d4;
  --glass-bg: rgba(26,26,46,0.55);  --glass-border: rgba(255,255,255,0.08);
}
[data-theme="light"] {
  --canvas: #f8fafc;  --surface: #ffffff;  --text-1: #0f172a;  --text-2: #475569;
  --secondary: #0891b2;
  --glass-bg: rgba(255,255,255,0.6);  --glass-border: rgba(0,0,0,0.08);
}
[data-theme="dark"] { /* mirrors :root so dark: variant keys off attribute */ }

@theme inline {
  --color-canvas: var(--canvas);  --color-surface: var(--surface);
  --color-text-1: var(--text-1);  --color-text-2: var(--text-2);
  --color-secondary: var(--secondary);
  --font-display: "Space Grotesk", sans-serif;  --font-sans: "Inter", sans-serif;
  --ease-micro: cubic-bezier(.2,.6,.2,1);
  --dur-micro: 200ms;  --dur-page: 400ms;  --dur-hero: 600ms;
}
@layer base { body { @apply bg-canvas text-text-1 font-sans; }
  h1,h2,h3 { @apply font-display; } }
@utility glass-surface { backdrop-filter: blur(12px); background: var(--glass-bg);
  border: 1px solid var(--glass-border); }
@utility glass-navbar  { backdrop-filter: blur(16px); /* + shadow on scroll via class */ }
```

```html
<!-- index.html <head> — prevents FOUC -->
<script>
  (function(){var t=localStorage.getItem('ticketera-theme')||'dark';
  document.documentElement.setAttribute('data-theme',t);})();
</script>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="...Space+Grotesk:600;Inter:400,500...">  <!-- or @fontsource -->
```

target: ≥4.5:1 in both themes; `glass-surface` text uses `--text-1` (not `--text-2`) to clear the threshold against blended bg. All interactive elements keep `focus-visible:ring`. Legacy `--accent`/`--text`/`--code-bg` BEM vars live until Phase 4.

## Animation System

| Concern | Mechanism |
|---|---|
| Route transitions | `<AnimatePresence mode="wait">` keyed by `location.pathname` — fade+translateY 400ms `ease-out` |
| Scroll reveal | `motion.div` + `whileInView`, staggered children via `variants` |
| Card hover lift | `whileHover={{ y:-6 }}` transition 200ms `--ease-micro` |
| Quantity / shake / success check | `motion` keyframes, 200ms pulse or 2-iteration shake |
| Reduced motion | `useReducedMotion()` (framer-motion) hook → when true, render plain `<div>`/instant swap; skeleton renders static (no pulse) |

Motion tokens (`--dur-micro/page/hero`, `--ease-micro`) are CSS vars consumed by framer `transition={{ duration: varDur, ease: varEase }}` via a `motion` config helper in `lib/motion.js`.

## Data Flow

**Theme toggle:**
```
ThemeToggle ─onClick→ useTheme.setTheme(next)
   └→ document.documentElement.setAttribute('data-theme', next)
   └→ localStorage.setItem('ticketera-theme', next)
   └→ React state re-render → all token consumers repaint (1 frame)
```

**Auth-aware Navbar:**
```
Navbar ─useAuth()→ { user, isAuthenticated, logout }  (from AuthProvider via /auth/me)
   └ authed ? <UserMenu onLogout={logout}/> : <NavLink to="/login">Login</NavLink>
```

**Route transition:**
```
useLocation().pathname ─key→ AnimatePresence ─exit(old 400ms)→ enter(new 400ms)
   └ useReducedMotion() ? instant swap : animated
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/index.css` | Modify | 1369→<200 lines: `@theme inline` tokens, glass utilities, `@layer base`. Legacy BEM removed in Phase 4. |
| `frontend/src/App.css` | Delete | 184 lines dead Vite boilerplate (Phase 1). |
| `frontend/index.html` | Modify | Inline theme script (FOUC), Google Fonts/`@fontsource` links, `<html data-theme="dark">`. |
| `frontend/package.json` | Modify | Add `framer-motion`. |
| `frontend/src/App.jsx` | Modify | Wrap `<Routes>` in `Layout` + `AnimatePresence` keyed by `useLocation().pathname`. Keep `ErrorBoundary`, `ProtectedRoute`, `RoleGuard` intact. |
| `frontend/src/components/layout/Layout.jsx` | Create | Shell: Navbar + main + Footer, mounts AnimatePresence. |
| `frontend/src/components/layout/Navbar.jsx` | Create | Glass, auth-aware, scroll-aware, mobile hamburger. |
| `frontend/src/components/layout/Footer.jsx` | Create | Branding + links, theme-respecting. |
| `frontend/src/components/layout/ThemeToggle.jsx` | Create | Icon toggle button. |
| `frontend/src/components/ui/Badge.jsx` | Create | `variant` prop per REQ-DS9. |
| `frontend/src/components/ui/Skeleton.jsx` | Create | `width`/`height`/`variant`, pulse animation (disabled under reduced-motion). |
| `frontend/src/components/ui/EmptyState.jsx` | Create | icon/title/description/action. |
| `frontend/src/components/ui/GradientHero.jsx` | Create | imageUrl + dark overlay + brand gradient + CTA, 600ms entry. |
| `frontend/src/components/ui/GlassCard.jsx` | Create | `children`/`className` merged with `.glass-surface`. |
| `frontend/src/hooks/useTheme.js` | Create | read/write `localStorage`, manage `data-theme`. |
| `frontend/src/lib/motion.js` | Create | framer variants/transitions referencing CSS tokens; reduced-motion guard. |
| `frontend/src/components/Button.jsx` | Modify | Add `glass`,`gradient` variants; preserve `primary`/`secondary`/`danger`/`ghost`. |
| `frontend/src/components/Card.jsx` | Modify | Optional `glass` prop; current `bg-white` → `bg-surface`. |
| `frontend/src/pages/*` (14 files) | Modify | Replace BEM-rooted markup with design-system components per Phase 2/3. |
| `frontend/src/**/*.test.jsx` | Modify | Add visual-regression DOM assertions (selectors, theme attr, class presence). |

## Interfaces / Contracts

```jsx
// useTheme
[{ theme: 'dark'|'light', setTheme(t), toggle() }, 'dark'|'light' as initial]

// Layout — wraps children (App passes the routed element tree)
<Layout>{outlet}</Layout>

// New primitives — props per REQ-DS9 table (Badge/Skeleton/EmptyState/GradientHero/GlassCard)

// Button extension — additive variants only; existing variants byte-identical
const variantClasses = { /* existing 4 unchanged */ glass: '...', gradient: 'bg-[linear-gradient(...)] ...' }
```

No backend contract, API call, route, or auth-policy signature changes (REQ-FQ-V1 enforces).

## Migration Plan (phase-by-phase, tests green each phase)

| Phase | Scope | Legacy CSS impact | Test guard |
|---|---|---|---|
| 1 Foundation | `@theme` tokens, fonts, `Layout`/`Navbar`/`Footer`/`ThemeToggle`, framer-motion install, `AnimatePresence` in `App.jsx`, delete `App.css`, inline theme script | Add new `@theme` + utilities alongside existing BEM (no removal yet) | Run 262 existing; add Navbar/Layout/theme tests |
| 2 Public pages | Home, EventList, EventDetail, Checkout/Return, Login, TicketLookup, NotFound restyled with primitives | Legacy BEM for those pages becomes unused but stays until Phase 4 | 262 green + new per-page visual tests |
| 3 Admin/Staff | OrganizerDashboard, OrganizerEventNew, OrganizerDetail, OrganizerMetrics, AdminPanel, StaffScan | Same — leftover BEM unused | 262 green + admin visual tests |
| 4 Migration | Grep-audit every legacy selector for residual references; delete confirmed-orphan BEM; `index.css` 1369→<200 | **All** legacy BEM removed; only tokens + base + glass utilities remain | 262 green; snapshot of `index.css` line count assertion |
| 5 Polish | Motion timing pass, contrast/a11y audit (Lighthouse ≥90), skeleton coverage, visual regression baselines | None | 262 green + regression suite locked |

**Per-phase test gate:** `npm test` MUST return 262+ passing before the phase commit. Phase 4 deletion is preceded by `grep -r '\.event-card\|\.ticket-card\|\.button-primary' src/` returning zero hits — proving no page still references the legacy selector. Phase 5 adds `index.css` line-count assertion (`wc -l < 200`) to fail future BEM reintroduction.

## Testing Strategy

| Layer | What | Approach |
|---|---|---|
| Unit | `useTheme`, `useReducedMotion`, Button variants, primitives render props | Existing vitest + RTL; assert `data-theme` flips, localStorage writes, variant classes apply, reduced-motion renders static |
| Integration | Navbar auth states, Layout wraps route, route transition mounts/unmounts | RTL render `AuthProvider`+`BrowserRouter`; assert Login link vs user menu; assert AnimatePresence exit/enter sequence with fake timers |
| Visual regression | Page token usage, theme attr, glass class presence, contrast  | DOM-assertion tests over pixel diffs (stable selectors): assert `data-theme`, `bg-surface`/`text-text-1` classes, ring visibility, no `App.css` import remains |

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Legacy CSS removal breaks page relying on orphan utility | Med | High | Phase 4 only after Phase 2/3 prove pages work BEM-free; grep audit before any deletion |
| `dark:` utilities silently wrong when no `data-theme` | Med | Med | `:root` mirrors `[data-theme="dark"]`; `index.html` ships explicit `data-theme="dark"` default |
| framer-motion perf on low-end devices | Low | Med | `useReducedMotion` gate; GPU transforms only (`y`/`opacity`); `mode="wait"` limits concurrent animations |
| FOUC on initial paint | Med | Low | Inline head script reads localStorage before React mounts |
| 262 test regression | Low | High | Per-phase test gate; tests adjusted only when behavior genuinely changed, never to accommodate visuals (REQ-FQ-V4) |
| Contrast failure on glass surfaces | Med | High | REQ-FQ-V2 audit; glass text uses `--text-1`; Lighthouse ≥90 in Phase 5 |
| Reviewer overload (large diff) | High | Med | 5 phased work units; `single-pr` size-exception pre-approved per proposal |

## Open Questions

- [ ] Enforce `index.css < 200` lines via lint rule/pre-commit hook now or in Phase 5?
- [ ] Font source: `@fontsource/*` (self-host, offline, no CDN dependency) vs Google Fonts CDN `<link>` (simpler, needs network). Leaning `@fontsource` for reliability.
- [ ] Are `dark:` variant utilities authored anywhere, or is theme driven purely by `data-theme` token swap (single token source)? Design assumes swap-only.

## Next Step

Ready for `sdd-tasks` — phase decomposition into reviewable work units.