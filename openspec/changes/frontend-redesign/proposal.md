# Proposal: Frontend Redesign — "Modern Elegance"

## Intent

The ticketera-online frontend has a working component library (Button, Card, Modal, FormField, Spinner) that **pages do not use**. Pages render against 1369 lines of legacy BEM CSS in `index.css`, plus 184 lines of dead Vite boilerplate in `App.css`. Visual quality averages **2.3/5 across 14 pages** (Home 1/5, NotFound 1/5, Login 2/5; best is StaffScan 4/5). There is no Navbar, no Footer, no Layout, no page transitions, no animation library. Two styling systems fight each other. This change replaces the legacy visual layer with a cohesive **dark-first, glass-morphism, motion-driven** design system — without touching backend contracts or product behavior.

## Scope

### In Scope
- Tailwind v4 `@theme` token system (color, typography, elevation, motion) in `index.css`, with `data-theme` architecture for dark + light mode
- Brand identity: "Ticketera" logo, name treatment, brand tokens in the design system
- Light mode toggle with full dual-theme support (dark default, light selectable)
- `Layout` shell: `Navbar` (glass, auth-aware, branded), `Footer`, route-transition wrapper
- Adoption of existing components on all 14 pages + new primitives (`Badge`, `Skeleton`, `EmptyState`, `GradientHero`, `GlassCard`)
- framer-motion: full animations by default (page transitions, hover lifts, scroll reveals, skeleton loading); `prefers-reduced-motion` as opt-out
- Per-page visual restyle: **public pages prioritized** — Home hero, EventList cards, EventDetail, Checkout flow, Login, NotFound, TicketLookup. Organizer dashboard suite and AdminPanel as fast follow-on (Phase 3).
- Delete legacy BEM from `index.css` and dead `App.css`
- Visual regression tests (vitest + DOM assertions) alongside existing 262 tests

### Out of Scope
- Backend API, endpoints, contracts, auth policies, Cloudflare R2
- New product features or data flows
- Responsive breakpoint overhaul beyond current
- i18n / routing changes / TypeScript migration
- Design tokens as a separate published package
- Event image placeholder/fallback system (organizer-supplied images are guaranteed)

## Capabilities

### New Capabilities
- `design-system`: Tailwind v4 `@theme` tokens (palette, typography, elevation, motion), base styles, reusable visual primitives and their contracts.
- `app-shell-layout`: `Navbar`, `Footer`, `Layout` wrapper, route-transition orchestration, auth-aware navigation state.
- `page-visual-design`: Per-page visual treatment (heroes, glass cards, imagery, empty states, skeletons, micro-interactions) and motion behavior.

### Modified Capabilities
- `frontend-quality`: existing behavioral REQs (W6–W15, SG3–SG12) are preserved; extended with visual acceptance criteria (dark-theme contrast, glass surfaces, motion presence) layered over current behavioral scenarios.

## Approach

Five phases, each a reviewable work unit:

1. **Foundation** — `@theme` tokens, fonts (Space Grotesk + Inter), base styles, framer-motion install, `Layout`/`Navbar`/`Footer`, route transitions. Delete `App.css`.
2. **Public Pages** — Home hero, EventList cards, EventDetail, Checkout/Return, Login, TicketLookup, NotFound.
3. **Admin/Staff** — OrganizerDashboard, OrganizerEventDetail, OrganizerEventNew, OrganizerEventMetrics, AdminPanel, StaffScan polish.
4. **Migration** — Strip legacy BEM from `index.css`, remove orphan utilities, audit dead selectors.
5. **Polish** — Motion timing pass, contrast/a11y audit, skeleton coverage, visual regression tests, snapshot baselines.

## Design System

| Token | Dark Mode | Light Mode |
|-------|-----------|------------|
| Background | `#0a0a0f` (near-black) | `#f8fafc` (slate-50) |
| Surface | `#1a1a2e` + glass | `#ffffff` + glass |
| Primary accent | `#7c3aed → #a855f7` gradient | `#7c3aed → #a855f7` gradient (same) |
| Secondary | `#06b6d4` cyan | `#0891b2` cyan-600 |
| Text primary | `#f8fafc` | `#0f172a` |
| Text secondary | `#94a3b8` | `#475569` |
| Display font | Space Grotesk (Google Fonts) | same |
| Body font | Inter (Tailwind default) | same |
| Motion lib | framer-motion — full by default, reduced-motion opt-out |
| Component pattern | glass surfaces, gradient heroes, 3D card hover, micro-interactions |
| Theme switching | `data-theme="dark|light"` on `<html>`, CSS custom properties, toggle in Navbar |

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/index.css` | Modified | `@theme` tokens replace legacy BEM (1369→<200 lines) |
| `frontend/src/App.css` | Removed | Dead Vite boilerplate (184 lines) |
| `frontend/src/App.jsx` | Modified | Wrap routes in `Layout`, add `AnimatePresence` transitions |
| `frontend/src/components/*` | New/Modified | Add `Navbar`, `Footer`, `Layout`, `Badge`, `Skeleton`, `EmptyState`, `GradientHero`, `GlassCard`; restyle existing primitives |
| `frontend/src/pages/*` | Modified | All 14 pages restyled to consume design system |
| `frontend/package.json` | Modified | Add `framer-motion` dependency |
| `frontend/src/**/*.test.jsx` | Modified | Extend with visual regression assertions |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Legacy CSS removal breaks pages relying on orphan utilities | Med | Phase 4 only after Phase 2/3 prove pages work without BEM; grep-referenced class audit first |
| framer-motion perf on low-end devices | Low | Gate motion with `prefers-reduced-motion`; keep transforms GPU-friendly |
| Visual regression flakiness | Med | DOM-assertion tests over pixel diffs; stable selectors |
| Test count drop / behavioral regressions | Low | All 262 existing tests MUST stay green each phase |
| Reviewer overload (large diff) | High | 5 phased work units; size-exception `single-pr-default` pre-approved |

## Rollback Plan

Phases land as ordered commits on `dev`. Revert per phase via `git revert <phase-sha>` — each phase is self-contained. Foundation phase can be reverted independently by restoring `index.css`/`App.css` from `git` and `npm uninstall framer-motion`. No backend or DB changes exist, so no data migration rollback is needed.

## Dependencies

- `framer-motion` (new npm dependency)
- Space Grotesk + Inter via Google Fonts (CDL `<link>` in `index.html` or `@fontsource`)

## Success Criteria

- [ ] Every page scores ≥4/5 on visual quality audit (current avg 2.3/5)
- [ ] All 262 existing frontend tests pass + new visual tests added per phase
- [ ] `index.css` reduced to `<200` lines (token-only, zero BEM)
- [ ] `App.css` deleted (0 lines)
- [ ] Lighthouse accessibility ≥90 on Home, EventList, EventDetail, Login
- [ ] `prefers-reduced-motion` disables all non-essential motion
- [ ] No behavioral REQ from `frontend-quality` regression (W6–W15, SG3–SG12)

## Non-Goals

- No backend API, endpoint, contract, or auth-policy changes
- No new product features or data flows
- No responsive breakpoint overhaul beyond current set
- No i18n, routing, or URL changes
- No TypeScript migration (JSX stays)
- No published standalone design-token package

## Proposal Question Round

The context is rich; these product questions would sharpen the proposal before specs. User may answer, skip, or request a second round.

1. **Brand vs. product positioning** — should "Modern Elegance" carry the ticketera brand identity (logo, name treatment) or stay a neutral dark system the brand sits on top of? Affects whether `design-system` spec encodes brand tokens.
2. **Imagery sourcing** — Phase 2 relies on event photos as heroes. Are organizer-supplied images guaranteed per event, or do we need a **default gradient/placeholder** system for events with no image (common case)?
3. **Motion tolerance** — is there an accessibility or audience constraint (e.g., older ticket buyers) that should make **reduced-motion the default** rather than the opt-out? Affects `page-visual-design` motion requirements.
4. **Light mode** — dark is default; is a light-mode toggle a **non-goal for this change** or a deferred follow-up the design system must **keep room for**? Affects token architecture (`@theme` vs `data-theme`).
5. **Organizer dashboard scope** — should the dashboard suite get **equal** visual investment to public event browsing, or is public-facing the priority and organizer pages a faster follow-on? Affects phase sizing between Phase 2 and 3.

**Decisions (user-confirmed)**: (1) branded system with "Ticketera" logo and name treatment; (2) no placeholder system — organizer-supplied images guaranteed; (3) full animations by default, `prefers-reduced-motion` as opt-out; (4) light mode in scope now with `data-theme` architecture and toggle; (5) public pages prioritized, organizer dashboard as fast follow-on (Phase 3).