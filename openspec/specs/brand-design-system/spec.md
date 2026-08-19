# Brand Design System Specification

## Purpose

Frontend design-system contract per the brand decisions doc: palette 2.1, dark variants 2.4, light-only 2.5, Poppins+Inter, Confetti surfaces, category chips, logo, motion ≤300ms. Supersedes unarchived frontend-redesign/design-system (REQ-DS1–DS10). Checkout, roles, navbar role logic: out of scope (regla de oro).

## Requirements

### REQ-BDS-1: Brand Color Tokens (Light-Only)

Tokens MUST define the exact 2.1 palette (Naranja `#F78B2D`, Amarillo `#F5C01F`, Verde `#67CF65`, Cian `#18C8DB`, Púrpura `#B65DC2`, Gris Oscuro `#4A4A4A`) and 2.4 dark variants (`#B45309`, `#6B5300`, `#166534`, `#0B6170`, `#6A2176`) on `:root` only; semantic tokens MUST remap to brand values.

#### Scenario: Exact palette from :root

- GIVEN the stylesheet loads
- WHEN a brand token is used
- THEN the exact 2.1 hex renders, no `data-theme` override

#### Scenario: Dark variant passes AA

- GIVEN text or a button needing brand color
- WHEN the 2.4 dark-variant token is used
- THEN the variant hex renders and passes WCAG AA on white

### REQ-BDS-2: Typography — Poppins + Inter

Display MUST be Poppins (bold for titles, logo, numbers); body MUST be Inter. Space Grotesk MUST NOT load.

#### Scenario: Fonts apply

- GIVEN any heading or body element
- WHEN it renders
- THEN headings use Poppins bold AND body uses Inter

### REQ-BDS-3: Geometry and Interactive States

Cards MUST have generous radii, primary buttons MUST be pill-shaped, inputs MUST be soft; hover/pressed MUST use dark variants, never lightening on white.

#### Scenario: Shapes and states

- GIVEN a Card, primary Button and input
- WHEN rendered and interacted with
- THEN generous radii, pill shape and soft inputs apply AND hover/pressed shift to dark variants

### REQ-BDS-4: Confetti Surfaces

Brand colors MUST fill large areas, gradients and blocks; MUST NOT be normal text (dark variants or Gris Oscuro only).

#### Scenario: Color on surfaces

- GIVEN a hero, gradient or block
- WHEN it renders
- THEN a brand color fills the surface

#### Scenario: No brand hex as text

- GIVEN any normal text
- WHEN it renders
- THEN no raw 2.1 hex is used as text color

### REQ-BDS-5: Light-Only Mode

The app MUST render light-only (`useTheme` pinned light, no toggle SHALL appear); `data-theme` MAY remain for future dark.

#### Scenario: Light default, no toggle

- GIVEN the app boots
- WHEN a page renders
- THEN the light palette applies and no toggle is visible

#### Scenario: data-theme retained

- GIVEN the light-only app
- WHEN `<html>` renders
- THEN `data-theme="light"` is present

### REQ-BDS-6: Logo

`ticketera-logo.webp` MUST render in the Navbar and Home hero.

#### Scenario: Logo in shell

- GIVEN the Navbar and the Home hero
- WHEN they render
- THEN the logo is visible beside the wordmark

### REQ-BDS-7: Home Category Chips

Home hero MUST render five chips — Música=Naranja, Teatro=Púrpura, Deportes=Verde, Stand-up=Amarillo, Festivales=Cian — tint bg + dark-variant text, above the event grid.

#### Scenario: Chips above grid

- GIVEN the Home hero
- WHEN it renders
- THEN five tinted chips appear above the event grid

#### Scenario: Chip text passes AA

- GIVEN any chip
- WHEN contrast is measured
- THEN dark-variant text on tinted bg passes WCAG AA

### REQ-BDS-8: Category Taxonomy (Frontend-Only)

Categories MUST be defined in `src/data/categories.js`; no backend category call SHALL be made.

#### Scenario: Local, decorative

- GIVEN the Home page
- WHEN chips render or are clicked
- THEN they come from the local taxonomy with no network request or filtering

### REQ-BDS-9: Motion

Interactive motion MUST be ≤300ms and MUST respect `prefers-reduced-motion`.

#### Scenario: Duration bound

- GIVEN a hover or state transition
- WHEN it triggers
- THEN duration is ≤300ms

#### Scenario: Reduced motion

- GIVEN `prefers-reduced-motion: reduce`
- WHEN an animation would play
- THEN it is disabled or instant

### REQ-BDS-10: Accessibility

Focus rings MUST use the dark variant (or dual ring); text MUST meet WCAG AA; color MUST NOT be the only channel.

#### Scenario: Visible focus

- GIVEN keyboard focus
- WHEN an element gains focus
- THEN a visible dark-variant ring renders

#### Scenario: Color not sole channel

- GIVEN a status conveyed with color
- WHEN it renders
- THEN an icon or text accompanies it

### REQ-BDS-11: Test Migration

Affected tests (useTheme, ThemeToggle, Navbar, css-migration, Card.glass, GlassCard, Button, Badge, EventList, EventDetail) MUST reflect light-only; the frontend suite MUST pass (`npm test` / `npx vitest run`).

#### Scenario: Suite green

- GIVEN updated components and tokens
- WHEN `npm test` runs
- THEN all frontend tests pass

#### Scenario: Assertions match

- GIVEN theme and palette assertions
- WHEN tests run
- THEN they reflect light-only defaults

### REQ-BDS-12: No-Regression (Golden Rule)

Checkout (Checkout→CheckoutReturn→CheckoutSuccess), roles/permissions and navbar role logic MUST NOT be modified.

#### Scenario: Untouched flows

- GIVEN the checkout flow, ProtectedRoute/RoleGuard and navbar role logic
- WHEN the change is applied
- THEN behavior and styling remain unchanged, no role or permission changes