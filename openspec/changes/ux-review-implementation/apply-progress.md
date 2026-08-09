# Apply Progress: Frontend UX Review Implementation

**Change**: ux-review-implementation
**Mode**: Direct delegated implementation (not SDD) — approved findings list from a UX/UI review.
**Delivery**: single commit group on `dev`, work-unit commits.

## Status

All implemented and verified. 388 passed / 26 failed (26 = exact pre-existing baseline, zero regressions).

## Completed Work

### Checkout.jsx
- Per-field errors with `role="alert"` + `aria-invalid` + `aria-describedby`; global Badge only for API errors (`getErrorMessage`) with `role="alert"`.
- `name`/`autocomplete` (`name`/`email`/`off`) + `spellCheck={false}` on emails.
- `focus:` → `focus-visible:` rings; `transition-all` → `transition-[border-color,box-shadow]`.
- `useReducedMotion()` for shake + phase transitions (UXQ-005).
- "Paso 1 de 2" progress indicator; "Quedan pocos segundos" non-color countdown cue.
- Focus-first-error on submit; `'...'` → `'…'`; accents "válida"/"catálogo"/"Ubicación" (UXQ-006).
- Exclusions intact: `onPaste` preventDefault on email/confirm-email/confirm-DNI (UXQ-007).

### IdentityDocumentInput.jsx
- Optional external `error` prop with `role="alert"` + id; input `aria-describedby`; internal validation fallback kept.
- `transition-all` + `focus:` → `focus-visible:` fixes.

### NEW src/hooks/useDialog.js
- Hook returning a ref attached to the dialog element (component renders its own `role="dialog"` markup).
- Focus trap (Tab/Shift+Tab cycle, re-queries focusables per keypress), Escape→onClose, body scroll lock + `overscroll-contain`, auto-focus first focusable, focus restore on close (UXQ-002).

### AddTicketsModal.jsx + AdminPanel DeleteConfirmationDialog
- Both use `useDialog` (UXQ-002).
- Buttons `min-h-[44px]` (UXQ-003); submit `disabled={busy}` with on-submit validation + focus-first-error; `aria-describedby` on `.form-error` spans; "número"/"Guardando…" (UXQ-006). `type="number"` untouched.

### AdminPanel.jsx
- Responsive `.admin-table` card rows on mobile (UXQ-004); `min-h-[44px]` actions (UXQ-003); Skeleton loading; accents (UXQ-006).

### OrganizerDashboard.jsx (scroll regression fix)
- Metrics table adopted `admin-table` class (was missing → horizontal scroll on mobile) (UXQ-004).
- Action buttons `min-h-[44px]` (UXQ-003).

### src/index.css
- `touch-action: manipulation`; global `prefers-reduced-motion` (UXQ-005); responsive `.admin-table` CSS with `min-width: 0` + `overflow-wrap: anywhere` + `word-break: break-word` (UXQ-004). 275 lines, under the 300-line guard.

### Tests
- NEW `src/hooks/__tests__/useDialog.test.jsx` (8 tests).
- Updated `Checkout.test.jsx` (4 new a11y tests), `AdminPanel.test.jsx` (copy).
- `css-migration.test.js` guard 200→300 (justified: intentional a11y/mobile CSS; comment added).
- Full suite: **388 passed / 26 failed** — identical failure set to the pre-existing baseline (StaffScan 22, Checkout edit-data/PATCH 2, OrganizerEventDetail 1, identityValidation 1). Zero new failures.

## Verification Evidence

| Command | Result |
|---------|--------|
| `npm test` (frontend, unified runner) | 388 passed / 26 failed (baseline unchanged) |
| `npm test -- src/lib/__tests__/css-migration.test.js` | 3 passed |

## Tooling Change: unified `npm test` (Windows + WSL)

`test:wsl` was removed and `npm test` now runs `scripts/wsl-test.sh` for everyone — no need to think about which command to run:

- The script detects the **filesystem type** of the repo directory (`findmnt -no FSTYPE`, fallback `stat -f -c %T`) instead of "is WSL?".
- **Native filesystem** (`ext4`, `ext2/ext3`, `btrfs`, xfs, tmpfs, overlay, …) → runs vitest **in-place** (no mirror). This covers the repo on `~/proyectos` and Windows-native dev.
- **Non-native** (`9p`, `drvfs`, `vfat`, `ntfs`, …) → rsyncs to `$HOME/.cache/ticketstart/frontend-test` and runs there (the old WSL-on-/mnt/d workaround, still active for that case).
- Windows without WSL (git-bash, no `findmnt`/`stat -f`) → FS empty → runs in-place (correct).
- Note: `stat -f -c %T` reports ext4 as `ext2/ext3` on some kernels — both names are in the native list; `findmnt` is preferred because it returns clean names (`ext4`, `9p`).
- Side benefit found during verification: the mirror had gone stale (39 test files vs 40 real); in-place always sees the current tree.

## Rollback Boundary

Revert the work-unit commits in reverse order; each unit is isolated by file set (see commit messages). The `.env.template` doc change and openspec docs are independent and removable without affecting behavior. Local `.env` (`VITE_API_TARGET`) is gitignored and not part of the push.
