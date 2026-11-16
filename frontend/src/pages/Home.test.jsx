import { describe, it, expect, vi, beforeAll } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Home from './Home.jsx'

vi.mock('../hooks/useEvents.js', () => ({
  useEvents: () => ({ data: [], isLoading: false }),
}))

// jsdom has no IntersectionObserver; framer-motion's whileInView needs one.
beforeAll(() => {
  globalThis.IntersectionObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
})

function renderHome() {
  return render(
    <MemoryRouter>
      <Home />
    </MemoryRouter>,
  )
}

describe('Home', () => {
  it('does not render the event category icons in the hero', () => {
    renderHome()

    for (const label of ['Música', 'Stand-up', 'Arte', 'Festivales', 'Teatro']) {
      expect(screen.queryByRole('img', { name: label })).not.toBeInTheDocument()
    }
  })

  it('keeps the catalog call to action linking to /events', () => {
    renderHome()

    const cta = screen.getByRole('link', { name: /ver catálogo de eventos/i })
    expect(cta).toHaveAttribute('href', '/events')
  })
})
