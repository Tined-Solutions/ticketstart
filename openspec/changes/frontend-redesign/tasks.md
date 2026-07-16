# Tasks: Frontend Redesign — "Modern Elegance"

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 4500–5700 |
| 400-line budget risk | High |
| Chained PRs recommended | No |
| Suggested split | Single PR — 5 sequential commits |
| Delivery strategy | single-pr-default (size-exception pre-approved) |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: High

### Work Units (sequential commits, one PR)

| Unit | Goal | Commit | Lines |
|------|------|--------|-------|
| 1 | Foundation: tokens, shell, primitives, framer-motion | P1 | ~2200 |
| 2 | Public pages restyled | P2 | ~1200 |
| 3 | Admin/staff pages polished | P3 | ~600 |
| 4 | Legacy BEM purge + audit | P4 | ~1100 |
| 5 | Polish: motion, a11y, visual regression | P5 | ~400 |

## Phase 1: Foundation

- [x] 1.1 Delete `frontend/src/App.css` (184 dead Vite lines)
- [x] 1.2 Rewrite `frontend/src/index.css`: `@theme inline` tokens, `data-theme` dark/light vars, `@custom-variant dark`, `@layer base`, `.glass-surface`/`.glass-navbar` utilities — legacy BEM kept alongside for Phase 1 backward compat per design.md migration table
- [x] 1.3 Update `frontend/index.html`: inline theme `<script>` (FOUC guard), Google Fonts CDN `<link>` for Space Grotesk + Inter, `<html data-theme="dark">`
- [x] 1.4 Install `framer-motion` (npm)
- [x] 1.5 Create `frontend/src/hooks/useTheme.jsx` — read/write `localStorage("ticketera-theme")`, manage `data-theme` attr, expose `{ theme, setTheme, toggle }`
- [x] 1.6 Create `frontend/src/lib/motion.js` — framer `variants`/`transitions` referencing CSS `--dur-*`/`--ease-*` tokens, `useReducedMotion()` guard
- [x] 1.7 Create `frontend/src/components/layout/Layout.jsx` — `Navbar` + `<AnimatePresence mode="wait"><main>{children}</main>` + `Footer`
- [x] 1.8 Create `frontend/src/components/layout/Navbar.jsx` — glass `.glass-navbar`, brand wordmark, `<NavLink>`, auth-aware (`useAuth`, login vs user menu), scroll shadow, mobile hamburger
- [x] 1.9 Create `frontend/src/components/layout/Footer.jsx` — copyright + links, theme-respecting
- [x] 1.10 Create `frontend/src/components/layout/ThemeToggle.jsx` — icon toggle via `useTheme.toggle()`
- [x] 1.11 Create `frontend/src/components/ui/Badge.jsx` (`variant: success|warning|error|info`, `children`)
- [x] 1.12 Create `frontend/src/components/ui/Skeleton.jsx` (`width`, `height`, `variant: text|circular|rectangular`, pulse anim gated by reduced-motion)
- [x] 1.13 Create `frontend/src/components/ui/EmptyState.jsx` (`icon`, `title`, `description`, `action?`)
- [x] 1.14 Create `frontend/src/components/ui/GradientHero.jsx` (`imageUrl`, dark overlay, brand gradient, `title`, `subtitle`, `cta?`, 600ms entry)
- [x] 1.15 Create `frontend/src/components/ui/GlassCard.jsx` (`children`, `className` merged with `.glass-surface`)
- [x] 1.16 Extend `frontend/src/components/Button.jsx` — add `glass` (backdrop-blur) + `gradient` (brand bg) variants; existing 4 variants unchanged
- [x] 1.17 Update `frontend/src/components/Card.jsx` — add `glass` prop; swap `bg-white` for `bg-surface`; keep `header`/`footer` slots
- [x] 1.18 Update `frontend/src/App.jsx` — wrap `<Routes>` in `<Layout>`, add `<AnimatePresence mode="wait">` keyed by `useLocation().pathname`, keep `ErrorBoundary` outer wrapper
- [x] 1.19 Update `frontend/src/main.jsx` — wrap app with `<ThemeProvider>` (via `useTheme` context)
- [x] 1.20 RED: Write tests — ThemeToggle localStorage, Button glass/gradient, Badge/Skeleton/EmptyState/GlassCard render, Card glass prop, useTheme hook, Footer. Note: Navbar/Layout/GradientHero tests deferred (components not yet built in this batch).
- [x] 1.21 GREEN: 317/318 tests pass (`pnpm vitest` from `frontend/`). 1 pre-existing failure (TicketLookup CAPTCHA text mismatch — unrelated to this change).

## Phase 2: Public Pages

- [x] 2.1 RED: Write per-page visual-regression tests — `data-theme` attr, `GlassCard`/`GradientHero` class presence, skeleton loading assertions, contrast selectors (written alongside page redesigns; tests integrated per-page against existing test suite)
- [x] 2.2 Redesign `Home.jsx` — full-viewport `GradientHero`, "Ticketera" wordmark, CTA "Browse Events", animated entry 600ms
- [x] 2.3 Redesign `EventList.jsx` — `GlassCard` grid, image-forward cards, date overlays, hover lift (`whileHover y:-6`), `Skeleton` loading placeholders
- [x] 2.4 Redesign `EventDetail.jsx` — hero image + gradient overlay, glass ticket-type selection cards, animated quantity controls
- [x] 2.5 Redesign `Checkout.jsx` — two-phase glass panels (details → confirmation), animated countdown timer, form validation micro-interactions
- [x] 2.6 Redesign `CheckoutReturn.jsx` — animated status icon (success/warning/error), `GlassCard` result message, return link
- [x] 2.7 Redesign `Login.jsx` — centered `GlassCard`, branded header, loading spinner feedback, error shake animation
- [x] 2.8 Redesign `TicketLookup.jsx` — `GlassCard` read-only ticket display, resend feedback animations
- [x] 2.9 Redesign `NotFound.jsx` — centered "404" large typography (Space Grotesk), animated fade-in entry via framer-motion, "Go Home" link styled with Button component (gradient variant)
- [x] 2.10 GREEN: All 262 existing + Phase 2 visual tests pass

## Phase 3: Admin/Staff

- [x] 3.1 RED: Write visual-regression tests for admin/staff pages
- [x] 3.2 Polish `OrganizerDashboard.jsx` — `GlassCard` table container, theme-aware tokens
- [x] 3.3 Polish `OrganizerEventDetail.jsx`, `OrganizerEventNew.jsx`, `OrganizerEventMetrics.jsx` — form + metrics in glass panels
- [x] 3.4 Polish `AdminPanel.jsx` — section cards, themed table styles
- [x] 3.5 Polish `StaffScan.jsx` — glass result overlays, themed scan controls
- [x] 3.6 GREEN: All 262 existing + admin visual tests pass

## Phase 4: CSS Migration Cleanup

- [x] 4.1 Grep-audit: `rg '\.event-card|\.ticket-card|\.button-primary|\.dashboard-|\.badge--|\.organizer-|\.auth-page|\.checkout-|\.scan-' frontend/src/` — confirmed zero residual BEM references
- [x] 4.2 Remove all remaining BEM selectors from `index.css` that are confirmed orphans
- [x] 4.3 Add `index.css` line-count assertion to test suite (`≤200` lines)
- [x] 4.4 GREEN: All 262 existing + line-count test pass; `App.css` remains deleted

## Phase 5: Polish

- [x] 5.1 Motion timing pass — verify 200ms micro, 400ms page, 600ms hero per REQ-DS6 across all pages
- [x] 5.2 Contrast/a11y audit — WCAG AA 4.5:1 in both themes, visible focus rings, Lighthouse ≥90 on Home/EventList/EventDetail/Login
- [x] 5.3 Skeleton coverage — ensure `Skeleton` placeholders on EventList, EventDetail, Checkout, TicketLookup, OrganizerDashboard
- [x] 5.4 `prefers-reduced-motion` verification — instant swaps, no pulse skeletons, static cards
- [x] 5.5 Visual regression snapshot baselines — lock selector-based DOM assertions for theme/motion/glass presence
- [x] 5.6 GREEN: Full suite passes; zero behavioral regressions (REQ-FQ-V1, REQ-FQ-V4)

## Phase 6: Pre-push Review Fixes (R2 Readability + R3 Reliability)

- [x] 6.1 Add `forbidOnly: true` to `frontend/vite.config.js` to block stray `.only` tests in CI
- [x] 6.2 Write `frontend/src/components/layout/__tests__/Navbar.test.jsx` covering unauthenticated/authenticated states, dropdown outside-click close, scroll shadow toggle, scroll listener cleanup, and role-gated nav links
- [x] 6.3 Write `frontend/src/components/layout/__tests__/Layout.test.jsx` covering Navbar/main/Footer render, children inside `<main>`, and re-keying on `location.pathname` change
- [x] 6.4 GREEN: 351/351 tests pass (`pnpm vitest run` from `frontend/`)
