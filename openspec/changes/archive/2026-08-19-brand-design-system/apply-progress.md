# Apply Progress: brand-design-system

Status: **all tasks complete** (25/25) — single PR batch, delivery `single-pr` with `size:exception` pre-approved to 4000 lines.

## Verdict Summary

Implemented the full brand token rebase (light-only), Poppins+Inter typography, brand component restyle, category chips + featured grid, shared EventCard, motion ≤300ms, and light-only test migration. All 5 validator fixes resolved. Frontend suite green except 3 pre-existing baseline failures (2 in Checkout.test.jsx, 1 in identityValidation.test.js) verified as failing before this change (via stash). Checkout, roles/permissions, navbar role logic, and backend untouched (regla de oro).

## Phase 1: Foundation — tokens + validator fixes (1.1–1.7) ✅

- 1.1 ✅ Rewrote `tokens.css`: light-only `:root` brand palette + 5 dark variants + semantic remap; kept `@theme inline`; deleted `[data-theme]` override blocks; kept `@custom-variant dark` for future dark.
- 1.2 ✅ Resolved `--brand-1` double mapping: `--brand-1: var(--purpura-dark)` (#6A2176), `--brand-2: var(--naranja-dark)`; raw `--purpura` surface-only.
- 1.3 ✅ Defined `--accent-light: rgba(182,93,194,0.12)` (light purpura tint); `.button-secondary` uses `--accent-light`/`--accent`.
- 1.4 ✅ `--text-muted: #6B7280` (4.83:1 AA on white).
- 1.5 ✅ `index.html`: Poppins+Inter (dropped Space Grotesk), `data-theme="light"`, FOUC `var t='light'`, preload `/ticketera-logo.webp`.
- 1.6 ✅ `index.css`: button pill radius (`--radius-pill`), `.button-primary` `--primary`/`--primary-hover` (no opacity), `.form-group` input `--radius-input`; base `h1,h2` weight 700 in tokens base layer.
- 1.7 ✅ Contrast confirmed: `--primary-hover` #8F4208 (7.12:1 on white; 7.12:1 white text), `--accent-hover` #5A1B64 (11.83:1 both) — both pass AA.

## Phase 2: Shell — light-only + logo (2.1–2.3) ✅

- 2.1 ✅ `useTheme.jsx`: pinned to `'light'`, toggle/setTheme no-ops, Provider applies `data-theme="light"` on mount.
- 2.2 ✅ Deleted `ThemeToggle.jsx` + its test; dropped Navbar import (both desktop + mobile).
- 2.3 ✅ `Navbar.jsx`: logo `<img src="/ticketera-logo.webp">` + Poppins `text-gris-oscuro` wordmark; active NavLink `text-brand-1 bg-brand-1/10`. Role-aware link logic unchanged.

## Phase 3: Components (3.1–3.5) ✅

- 3.1 ✅ `Button.jsx`: all sizes `rounded-full`; primary/secondary/gradient/glass brand variants; focus `ring-primary`; glass hover darkens (validator fix #4).
- 3.2 ✅ `Card.jsx`: `rounded-[var(--radius-card)]`, header `font-display`. `GlassCard.jsx` no-op (tokens cascade).
- 3.3 ✅ `Badge.jsx`: dropped `dark:`; success `bg-verde/15 text-verde-dark`, warning `bg-amarillo/15 text-amarillo-dark`, info `bg-cian/15 text-cian-dark`, error `bg-rose-100 text-rose-700`.
- 3.4 ✅ `FormField.jsx`: `rounded-[var(--radius-input)]`, `text-gris-oscuro`, focus `ring-primary/25`, label `text-gris-oscuro`. `Modal.jsx`: `rounded-[var(--radius-card)]`, `text-gris-oscuro`, close `ring-primary`.
- 3.5 ✅ `GradientHero.jsx`: light Confetti hero (soft brand tint, no dark overlay), Poppins title Gris Oscuro, new `chips` + `logo` props.

## Phase 4: Pages + Categories (4.1–4.5) ✅

- 4.1 ✅ Created `src/data/categories.js` (5 categories + `chipClass`).
- 4.2 ✅ Created shared `src/components/events/EventCard.jsx` + test.
- 4.3 ✅ `Home.jsx`: `<GradientHero logo chips cta/>` + featured grid `useEvents()` first 6 + "Ver todos".
- 4.4 ✅ `EventList.jsx`: shared `EventCard`, `font-display` headings.
- 4.5 ✅ `EventDetail.jsx`: `font-heading`→`font-display`; `text-brand-1` accents; hero keeps dark overlay.

## Phase 5: Motion + logo (5.1–5.2) ✅

- 5.1 ✅ `motion.js` `DUR` = 0.15/0.25/0.3; `--dur-*` ≤300ms; `prefers-reduced-motion` preserved; tightened hardcoded 0.4/0.6 page transitions to ≤0.3.
- 5.2 ✅ Logo in Navbar (2.3) + Home hero `h-12` (4.3) + preload (1.5).

## Phase 6: Test migration + suite (6.1–6.3) ✅

- 6.1 ✅ Rewrote `useTheme.test.jsx` (light default, toggle/setTheme no-op, data-theme stays light). Kept `GlassCard.test.jsx`.
- 6.2 ✅ Updated `css-migration.test.js` (`--color-naranja`, `Poppins`, no `Space Grotesk`), `Card.glass.test.jsx`, `Button.test.jsx` (pill), `Button.variants.test.jsx` (brand gradient/glass), `Badge.test.jsx` (brand variants), `Navbar.test.jsx` (logo, no toggle), `NotFound.test.jsx` (gradient `to-brand-2`). Reviewed `accessibility.test.jsx` — no change needed.
- 6.3 ✅ `npm test` (native ext4 → `npx vitest run` in-place) = 449/452 passing. Only 3 pre-existing baseline failures (2 Checkout + 1 identityValidation) verified failing before this change via `git stash`. Build (`npx vite build`) succeeds. ESLint: zero new errors introduced.

## Validator Fixes (5)

1. ✅ `--brand-1` single mapping = `--purpura-dark`.
2. ✅ `--accent-light` defined (light purpura tint).
3. ✅ `--text-muted` `#6B7280` (4.83:1 AA).
4. ✅ glass hover darkens (`hover:bg-gris-oscuro/10`) instead of `bg-white/80` — never lightens on white.
5. ✅ `--primary-hover` #8F4208 (7.12:1) + `--accent-hover` #5A1B64 (11.83:1) confirmed ≥4.5:1 on white.

## Deviations from Design

- None material. Glass Button hover changed from design's `bg-white/80` to `hover:bg-gris-oscuro/10` per validator fix #4 (never lighten on white).
- Note: Naranja chip at `/15` tint gives 4.43:1 (marginally below 4.5 AA); design-specified `/15` retained — chips are decorative labels (REQ-BDS-8). Minor.

## Golden Rule Verification

- Checkout (Checkout→CheckoutReturn→CheckoutSuccess): **untouched** (only Checkout.test.jsx in the diff list is absent — Checkout page files unchanged).
- Roles/permissions (ProtectedRoute, RoleGuard, auth): **untouched**.
- Navbar role logic (`showStaff`/`showOrganizer`/`showAdmin`): **unchanged**.
- Backend: **zero changes** (no `backend/` files touched).

## Workload / PR Boundary

- Mode: single-pr, size:exception (pre-approved to 4000 changed lines).
- Authored changed lines: ~907 (332 insertions + 575 deletions across 25 files) + ~250 new-file lines. Well under 4000.
- Commits: 3 work-unit commits (shell, components, pages) + this apply-progress/docs commit.
- Rollback: `git revert` restores dark-first tokens + `data-theme="dark"`; revert components before tokens if partial.

## Next

`sdd-verify` — run full frontend suite + build; confirm requirements/scenarios.
