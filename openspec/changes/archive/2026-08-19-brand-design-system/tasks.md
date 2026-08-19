# Tasks: Brand Design System

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~2200 (1800–2600) |
| Estimated files changed | ~31 (4 new, 2 deleted) |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR (size:exception approved to 4000) |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: Yes
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

(size:exception pre-approved to 4000 lines by maintainer; single-pr still requires the exception gate before apply.)

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test | Runtime harness | Rollback |
|------|------|-----------|--------------|-----------------|----------|
| 1 | Tokens + shell | PR 1 | `npx vitest run src/hooks/__tests__/useTheme.test.jsx` | `npm run dev` → light-only, Poppins | revert tokens.css + index.html |
| 2 | Components + pages | PR 1 | `npx vitest run src/components/__tests__/Button.test.jsx src/components/ui/__tests__/Badge.test.jsx` | `npm run dev` → Home chips + grid | revert component/page files |
| 3 | Test migration | PR 1 | `npm test` | `npx vitest run` | revert test files |

## Phase 1: Foundation — tokens + validator fixes

- [x] 1.1 Rewrite `frontend/src/tokens.css`: light-only `:root` brand palette + 5 dark variants + semantic remap; keep `@theme inline`; delete `[data-theme]` override blocks. Verify: brand hex renders.
- [x] 1.2 Resolve `--brand-1` double mapping: `--brand-1`=`--purpura-dark`(#6A2176), `--brand-2`=`--naranja-dark`; raw `--purpura` surface-only. Verify: grep single mapping.
- [x] 1.3 Define `--accent-light` (light purpura tint) used by `.button-secondary`. Verify: token defined + referenced.
- [x] 1.4 Set `--text-muted` to `#6B7280` (4.83:1 AA). Verify: contrast passes.
- [x] 1.5 `frontend/index.html`: Poppins+Inter (drop Space Grotesk); `data-theme="light"`; FOUC `var t='light'`; preload `/ticketera-logo.webp`. Verify: no Space Grotesk.
- [x] 1.6 `frontend/src/index.css`: `button` pill radius; `.button-primary` `--primary`/`--primary-hover` (no opacity); base `h1,h2` weight 700. Verify: classes apply.
- [x] 1.7 Confirm `--primary-hover`(#8F4208) + `--accent-hover`(#5A1B64) ≥4.5:1 on white; adjust if failing. Verify: contrast check.

## Phase 2: Shell — light-only + logo

- [x] 2.1 `useTheme.jsx`: `readStoredTheme`→`'light'`, toggle no-op; Provider sets `data-theme="light"`. Verify: useTheme test.
- [x] 2.2 Delete `ThemeToggle.jsx` + test; drop Navbar import. Verify: grep no ThemeToggle.
- [x] 2.3 `Navbar.jsx`: logo `<img src="/ticketera-logo.webp">` + Poppins `text-gris-oscuro` wordmark; active NavLink `text-brand-1 bg-brand-1/10`. Verify: Navbar test (logo, no toggle).

## Phase 3: Components

- [x] 3.1 `Button.jsx`: all sizes `rounded-full`; `primary`/`secondary`/`gradient`/`glass` brand variants; focus `ring-primary`. Verify: Button tests.
- [x] 3.2 `Card.jsx`: `rounded-[var(--radius-card)]`; header `font-display`. `GlassCard.jsx`: no-op (tokens cascade). Verify: Card.glass test.
- [x] 3.3 `Badge.jsx`: drop `dark:`; success `bg-verde/15 text-verde-dark`, warning `bg-amarillo/15`, info `bg-cian/15 text-cian-dark`. Verify: Badge test.
- [x] 3.4 `FormField.jsx`: `rounded-[var(--radius-input)]`, `text-gris-oscuro`, focus `ring-primary/25`. `Modal.jsx`: `rounded-[var(--radius-card)]`, close `ring-primary`. Verify: dev.
- [x] 3.5 `GradientHero.jsx`: light Confetti hero (drop dark overlay), Poppins title Gris Oscuro, new `chips`+`logo` props. Verify: Home integration.

## Phase 4: Pages + Categories

- [x] 4.1 Create `frontend/src/data/categories.js` (5 categories + `chipClass`). Verify: shape test.
- [x] 4.2 Create shared `frontend/src/components/events/EventCard.jsx` + test. Verify: EventCard test.
- [x] 4.3 `Home.jsx`: `<GradientHero logo chips={categories} cta/>`; featured grid `useEvents()` first 6 + "Ver todos". Verify: Home renders 5 chips + grid.
- [x] 4.4 `EventList.jsx`: shared `EventCard`, `rounded-[var(--radius-card)]`, `font-display`. Verify: EventList test.
- [x] 4.5 `EventDetail.jsx`: `font-heading`→`font-display`; `text-brand-1` accents; hero keeps dark overlay. Verify: EventDetail test.

## Phase 5: Motion + logo integration

- [x] 5.1 `motion.js` + `--dur-*` ≤300ms; keep `prefers-reduced-motion`. Verify: REQ-BDS-9.
- [x] 5.2 Logo in Navbar (2.3) + Home hero `h-12` (4.3) + preload (1.5). Verify: visual.

## Phase 6: Test migration + suite

- [x] 6.1 Rewrite `useTheme.test.jsx` (light default, toggle no-op); keep `GlassCard.test.jsx`. Verify: run.
- [x] 6.2 Update `css-migration.test.js` (`--color-naranja`, `Poppins`, no `Space Grotesk`), `Card.glass.test.jsx`, `Button.test.jsx`, `Button.variants.test.jsx`, `Badge.test.jsx`, `EventDetail.test.jsx`; review `accessibility.test.jsx`. Verify: run.
- [x] 6.3 `npm test` (fallback `npx vitest run`) all green; checkout + role tests untouched. Verify: full suite.
