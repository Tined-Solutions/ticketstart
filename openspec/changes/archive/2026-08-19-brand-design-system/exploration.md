# Exploration: brand-design-system

## Current State

The frontend was redesigned under `openspec/changes/frontend-redesign` ("Modern Elegance"): a **dark-first, glass-morphism, motion-driven** design system built on Tailwind v4 `@theme inline` tokens plus `data-theme="dark|light"` on `<html>`. `index.html` hardcodes `<html data-theme="dark">` and runs an inline FOUC guard reading `localStorage['ticketera-theme']` (default `'dark'`). A `ThemeProvider`/`useTheme`/`ThemeToggle` exposes dark↔light switching.

**Token architecture** (`frontend/src/tokens.css`, imported by `index.css`):
- `@theme inline` bridges CSS vars → Tailwind utilities (`--color-primary`, `--color-brand-1`, `--color-canvas`, `--color-surface`, `--color-text-1`, `--color-text-2`, `--color-text-muted`, `--color-glass-bg`, etc.).
- `:root` = **dark default**; `[data-theme="light"]` and `[data-theme="dark"]` override blocks.
- Typography: `--font-sans: "Inter"` (body), `--font-display: "Space Grotesk"` (headings), `--font-mono`.
- Motion: `--ease-micro/smooth`, `--dur-micro: 200ms`, `--dur-normal: 400ms`, `--dur-slow: 600ms`.
- Radius: only `--radius-glass: 1rem`; Tailwind defaults (`rounded`, `rounded-md/lg/xl`) used elsewhere.
- Legacy semantic tokens retained for backward compat (`--primary` indigo `#4f46e5`, `--accent` purple, `--success/warning/danger/info`).

**Palette today**: indigo/violet/purple accents (`#4f46e5`, `#7c3aed`, `#a855f7`, `#c084fc`) on near-black dark surfaces (`#0a0a0f`, `#1a1a2e`). **No Naranja/Amarillo/Verde/Cian/Púrpura from the brand doc.** Dark-first, glass, gradient (`from-indigo-500 to-violet-500`, `from-brand-1 to-brand-2`).

## Affected Areas

- `frontend/src/tokens.css` — the entire palette/theme block must move to light-only + brand palette + dark variants (2.4). Currently dark-first.
- `frontend/index.html` — `<html data-theme="dark">` + FOUC script + Google Fonts `<link>` (Space Grotesk + Inter) need updating (display font swap to Poppins/Baloo/Sora candidates).
- `frontend/src/hooks/useTheme.jsx` — dark/light toggle logic and `default 'dark'`. Light-only MVP may keep the hook for future dark mode but default must become light and/or the toggle removed.
- `frontend/src/components/layout/ThemeToggle.jsx` — dark↔light toggle button; conflicts with "modo claro solamente" MVP.
- `frontend/src/components/layout/Navbar.jsx` — indigo/violet wordmark gradient, `text-brand-1`, `bg-brand-1/10`; logo (ticketera-logo.webp) currently **not used**.
- `frontend/src/components/Button.jsx` — `gradient` variant hardcodes `from-indigo-500 to-violet-500`; `primary` uses `--primary` (indigo). Must map to brand dark-variant buttons.
- `frontend/src/components/Card.jsx` / `ui/GlassCard.jsx` — glass surfaces on dark; brand wants "Confetti" color surfaces + generous card radii.
- `frontend/src/components/ui/GradientHero.jsx` — dark gradient overlay + `text-white` + `from-brand-1 to-brand-2`; brand hero wants category chips + event grid (new structure).
- `frontend/src/components/ui/Badge.jsx` — emerald/amber/rose/sky semantic badges (Tailwind defaults), plus `dark:` variants. Needs brand colors.
- `frontend/src/components/FormField.jsx`, `Modal.jsx` — hardcoded `bg-white`, `text-gray-900`, `border-border`; light-only OK but must adopt brand radii/inputs suaves.
- `frontend/src/pages/Home.jsx` — currently a single GradientHero (brand gradient title) + 4 feature cards. No category chips; brand hero structure (chips + event grid) is entirely new.
- `frontend/src/pages/EventList.jsx`, `EventDetail.jsx` — use `text-brand-1` (purple) for prices/accents, glass cards, `Button variant="gradient"`. Event has **no category** field.
- `frontend/src/index.css` — base `h1/h2` use `--font-display` (Space Grotesk), `button` radius `6px` (should become pill for primary CTAs).
- `frontend/src/lib/motion.js` — mirrors `--dur-*`/`--ease-*`; durations already within 150–300ms micro band, `prefers-reduced-motion` respected.

## Approaches

1. **Full token rebase (brand tokens as new source of truth)** — Replace `@theme inline` + `data-theme` blocks with a light-only brand palette (5 brand colors + Gris Oscuro `#4A4A4A` + documented dark variants), swap display font, and remap semantic tokens (`--color-brand-1` etc.) to brand values. Keep legacy vars only where absolutely needed.
   - Pros: Single coherent source of truth; aligns with brand doc 2.5 (light-only, dark future-proof via semantic tokens); cleanest.
   - Cons: Broad blast radius across all components/pages; many snapshot/class assertions and the `data-theme` architecture change; biggest effort.
   - Effort: High

2. **Additive layer — brand tokens alongside current, migrate incrementally** — Introduce new `--color-brand-*` (orange/yellow/green/cyan/purple + dark variants) and display font without removing dark-first system; restyle page-by-page.
   - Pros: Lower risk per step; preserves working dark system during migration.
   - Cons: Two palettes coexist → drift; still must eventually remove dark/light toggle; does not fully satisfy light-only decision.
   - Effort: Medium

3. **Targeted component + page restyle (minimal token surgery)** — Keep token file mostly intact, hard-replace accent hex in the few components/pages that carry color (Navbar wordmark, Button gradient, GradientHero, Home hero), add category chips + event grid.
   - Pros: Smallest diff, fastest.
   - Cons: Leaves dark-first + duplicate palettes; contradicts brand doc's semantic-token architecture goal (2.5); color scattered → future dark mode hard.
   - Effort: Low

### Recommendation

**Approach 1 (full token rebase)** — it is the only one that satisfies the brand doc: light-only MVP (2.5), exact palette (2.1), dark variants as additional tokens (2.4), Confetti surface language (9), and future dark-mode readiness via semantic tokens. The proposal should scope the token file rewrite as the foundation unit, then cascade to Navbar/Button/Home/EventList/EventDetail, keeping checkout and role logic untouched per the brand doc's "regla de oro".

### Risks

- **HIGH — Theme toggle removal vs. existing tests**: `useTheme.test.jsx` (7 tests) asserts default `'dark'` and dark↔light toggling; `ThemeToggle.test.jsx` and `Navbar.test.jsx` assert toggle presence/`aria-label`. Light-only MVP forces these to change (default light, toggle removed or hidden) → several tests must be rewritten or removed.
- **MEDIUM — Token/class assertions**: `css-migration.test.js` asserts `tokens.css` contains `data-theme`, `--color-canvas`, `@theme inline`; `Card.glass.test.jsx`/`GlassCard.test.jsx` assert `bg-surface`/`glass-surface` classes. Replacing the theme architecture will break these guards → must be updated intentionally.
- **MEDIUM — Category chips need a category concept that does not exist**: `backend/Models/Event.cs` has no category/tag field, and no `Category` model/controller/service exists; no frontend category concept either. The Home hero "category chips" require either (a) adding a backend category field (out of brand doc's stated scope — it says nothing about backend), or (b) a frontend-only hardcoded taxonomy (no API support). **Proposal must decide this explicitly.**
- **MEDIUM — Accessibility**: brand colors fail WCAG AA as normal text (2.42:1 etc.); all text-on-brand and buttons must use documented dark variants (2.4). Risk of regression if components reuse raw brand hex as text.
- **MEDIUM — Display font swap**: Google Fonts currently loads Space Grotesk + Inter. Swapping display to Poppins/Baloo/Sora changes `--font-display`; headings across all pages restyle (base `h1/h2`). Slight visual regression risk, not test-breaking.
- **LOW — `ticketera-logo.webp` unused**: new asset (62KB) in `public/` but no component references it. Navbar uses a text wordmark. Proposal should decide whether/where the logo is used.
- **LOW — Motion**: current `--dur-normal: 400ms` exceeds the brand's 150–300ms micro-interaction band for some transitions; `lib/motion.js` mirrors these. Should be tightened to ≤300ms where interactive.

## Ready for Proposal

Yes. The orchestrator should tell the user: the current system is **dark-first** with an indigo/purple palette and **no category concept anywhere** (backend or frontend) — the biggest architectural gaps vs. the brand doc. The proposal must choose: (1) full token rebase vs. additive layer, and (2) how to source category data for the Home hero chips (backend field vs. frontend taxonomy), since the brand doc doesn't define categories. Theme-toggle removal and `useTheme`/`css-migration` test updates are the main test-migration risks.
