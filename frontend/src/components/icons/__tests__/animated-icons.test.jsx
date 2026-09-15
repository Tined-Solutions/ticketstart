import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, fireEvent } from '@testing-library/react'
import { Disc3Icon } from '../disc-3.jsx'
import { ClapIcon } from '../clap.jsx'
import { LaughIcon } from '../laugh.jsx'
import { PartyPopperIcon } from '../party-popper.jsx'
import { PaletteIcon } from '../palette.jsx'
import { TicketIcon } from '../ticket.jsx'
import { SearchIcon } from '../search.jsx'
import { CalendarDaysIcon } from '../calendar-days.jsx'

const ICONS = [
  ['Disc3Icon', Disc3Icon],
  ['ClapIcon', ClapIcon],
  ['LaughIcon', LaughIcon],
  ['PartyPopperIcon', PartyPopperIcon],
  ['PaletteIcon', PaletteIcon],
  ['TicketIcon', TicketIcon],
  ['SearchIcon', SearchIcon],
  ['CalendarDaysIcon', CalendarDaysIcon],
]

function stubMatchMedia(reducedMotion) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((query) => ({
      matches: reducedMotion && query === '(prefers-reduced-motion: reduce)',
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))
  )
}

describe('Animated category icons', () => {
  beforeEach(() => {
    stubMatchMedia(false)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it.each(ICONS)('%s renders a decorative svg', (_name, Icon) => {
    const { container } = render(<Icon />)

    const svg = container.querySelector('svg')
    expect(svg).toBeTruthy()
    expect(svg).toHaveAttribute('aria-hidden', 'true')
  })

  it('skips the hover animation when prefers-reduced-motion is reduce', () => {
    stubMatchMedia(true)

    for (const [, Icon] of ICONS) {
      const { container, unmount } = render(<Icon />)
      const wrapper = container.firstChild

      expect(() => fireEvent.mouseEnter(wrapper)).not.toThrow()
      expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')

      unmount()
    }
  })
})
