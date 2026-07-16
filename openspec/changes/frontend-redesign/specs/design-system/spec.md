# Spec: design-system

## Purpose

Tailwind v4 `@theme` token system, dual-theme architecture (`data-theme`), typography, brand identity, glass morphism primitives, motion tokens, and new component contracts. Zero BEM — all styling flows through Tailwind tokens.

## Requirements

### REQ-DS1: `@theme` Token System with Dual-Theme Architecture
**Priority**: P0
**Status**: proposed

The `@theme` block MUST define all color, spacing, and elevation tokens as CSS custom properties driven by `data-theme="dark|light"` on `<html>`. Dark mode is the default; light mode is activated via attribute switch.

> **Scenario: Dark theme is default**
> **Given** no `data-theme` attribute on `<html>`
> **When** any page renders
> **Then** all surfaces use dark palette tokens (`#0a0a0f` bg, `#1a1a2e` surface)

> **Scenario: Light theme activates via data-theme**
> **Given** `<html data-theme="light">`
> **When** any page renders
> **Then** all surfaces use light palette tokens (`#f8fafc` bg, `#ffffff` surface)

> **Scenario: Token custom properties cascade correctly**
> **Given** the `@theme` block defines `--color-surface`
> **When** `data-theme` toggles
> **Then** all elements referencing `--color-surface` update in a single paint frame

---

### REQ-DS2: Typography — Space Grotesk + Inter
**Priority**: P0
**Status**: proposed

Space Grotesk MUST be loaded as the display/heading font. Inter MUST be the body font. Both MUST be available via Google Fonts CDN or `@fontsource`.

> **Scenario: Heading elements use Space Grotesk**
> **Given** any `<h1>`–`<h3>` element
> **When** the element renders
> **Then** `font-family` resolves to `"Space Grotesk", sans-serif`

> **Scenario: Body text uses Inter**
> **Given** any `<p>`, `<span>`, or `<label>` element
> **When** the element renders
> **Then** `font-family` resolves to `"Inter", sans-serif`

---

### REQ-DS3: Brand Tokens — "Ticketera" Identity
**Priority**: P1
**Status**: proposed

The design system MUST encode the "Ticketera" wordmark/logo placement and brand accent color (`#7c3aed` → `#a855f7` gradient) as reusable tokens.

> **Scenario: Brand accent gradient is available as a token**
> **Given** any component referencing the brand accent
> **When** background or border color is applied
> **Then** the `#7c3aed → #a855f7` gradient renders

> **Scenario: Wordmark displays in Navbar and Home hero**
> **Given** the Navbar or Home hero component
> **When** the page renders
> **Then** the "Ticketera" wordmark is visible in Space Grotesk with brand gradient

---

### REQ-DS4: Glass Morphism Utility Classes
**Priority**: P1
**Status**: proposed

The system MUST provide `.glass-surface` and `.glass-navbar` utility classes using `backdrop-blur`, semi-transparent backgrounds, and subtle borders.

> **Scenario: Glass surface renders with blur**
> **Given** a `.glass-surface` element over content
> **When** the element renders
> **Then** `backdrop-filter: blur()` is applied and background is semi-transparent

> **Scenario: Glass navbar has elevated blur**
> **Given** `.glass-navbar` at the top of the viewport
> **When** content scrolls behind it
> **Then** the background content appears blurred with a translucent overlay

---

### REQ-DS5: Gradient Hero Pattern
**Priority**: P1
**Status**: proposed

The system MUST define a gradient hero pattern: dark overlay (`rgba(0,0,0,0.6)`) + brand gradient + event image background.

> **Scenario: Hero renders overlay and gradient**
> **Given** an event image URL
> **When** the GradientHero component renders
> **Then** the image is visible under a dark overlay and brand gradient

---

### REQ-DS6: Motion Tokens
**Priority**: P1
**Status**: proposed

The system MUST define motion tokens: 200ms for micro-interactions, 400ms for page-level transitions, 600ms for hero entries. Easing curves MUST use framer-motion's spring or ease-out presets.

> **Scenario: Micro-interaction completes in 200ms**
> **Given** a button hover or toggle
> **When** the interaction triggers
> **Then** the animation duration is ~200ms

> **Scenario: Page transition completes in 400ms**
> **Given** a route change
> **When** AnimatePresence triggers
> **Then** the exit/enter animation duration is ~400ms

---

### REQ-DS7: Theme Toggle with Persistence
**Priority**: P0
**Status**: proposed

A light/dark toggle in the Navbar MUST switch `data-theme` on `<html>` and persist the preference to `localStorage`.

> **Scenario: Toggle switches theme**
> **Given** the current theme is dark
> **When** the user clicks the theme toggle
> **Then** `<html data-theme>` changes to `light` and all colors update

> **Scenario: Theme preference survives page reload**
> **Given** the user selected `light` in a previous session
> **When** the page reloads
> **Then** `data-theme="light"` is applied from `localStorage`

---

### REQ-DS8: Reduced Motion Respect
**Priority**: P0
**Status**: proposed

When `prefers-reduced-motion: reduce` is active, the system MUST disable all non-essential motion (page transitions, hover lifts, scroll reveals). Essential motion (spinners, progress indicators) MAY continue.

> **Scenario: Reduced motion disables page transitions**
> **Given** `prefers-reduced-motion: reduce` is active
> **When** the user navigates between routes
> **Then** no AnimatePresence transition plays — content swaps instantly

> **Scenario: Skeleton loading still renders**
> **Given** `prefers-reduced-motion: reduce` is active
> **When** content is loading
> **Then** skeleton placeholders render without pulse animation

---

### REQ-DS9: New Component Contracts
**Priority**: P1
**Status**: proposed

The system MUST provide `Badge`, `Skeleton`, `EmptyState`, `GradientHero`, and `GlassCard` components with defined prop contracts.

| Component | Key Props |
|-----------|-----------|
| `Badge` | `variant` (success\|warning\|error\|info), `children` |
| `Skeleton` | `width`, `height`, `variant` (text\|circular\|rectangular) |
| `EmptyState` | `icon`, `title`, `description`, `action` (optional ReactNode) |
| `GradientHero` | `imageUrl`, `title`, `subtitle`, `cta` (optional ReactNode) |
| `GlassCard` | `children`, `className` (merges with glass-surface) |

> **Scenario: Badge renders with variant color**
> **Given** `<Badge variant="success">Confirmed</Badge>`
> **When** the component renders
> **Then** green-tinted background and text are visible

> **Scenario: Skeleton shows loading placeholder**
> **Given** `<Skeleton width="200px" height="20px" variant="text" />`
> **When** the component renders
> **Then** a 200×20px animated placeholder rectangle is visible

> **Scenario: EmptyState shows message and optional action**
> **Given** `<EmptyState title="No events" action={<Button>Create</Button>} />`
> **When** the component renders
> **Then** the title, icon, and action button are visible

---

### REQ-DS10: Button Variant Extensions
**Priority**: P2
**Status**: proposed

The existing `Button` component MUST be extended with `glass` and `gradient` variants alongside existing variants (`primary`, `secondary`, `danger`, `outline`).

> **Scenario: Glass button renders with blur**
> **Given** `<Button variant="glass">Submit</Button>`
> **When** the component renders
> **Then** `backdrop-blur` and semi-transparent background are applied

> **Scenario: Gradient button uses brand gradient**
> **Given** `<Button variant="gradient">Get Started</Button>`
> **When** the component renders
> **Then** the background shows the `#7c3aed → #a855f7` gradient

> **Scenario: Existing variants are unchanged**
> **Given** `<Button variant="primary">Save</Button>`
> **When** the component renders
> **Then** behavior matches the current `primary` variant exactly
