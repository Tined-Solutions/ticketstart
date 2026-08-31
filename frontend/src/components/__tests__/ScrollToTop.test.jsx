import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route, Link } from 'react-router-dom'
import userEvent from '@testing-library/user-event'
import ScrollToTop from '../ScrollToTop.jsx'

// Wrap a tiny app with two routes so we can navigate and assert the scroll reset.
function renderApp() {
  return render(
    <MemoryRouter initialEntries={['/a']}>
      <ScrollToTop />
      <Routes>
        <Route
          path="/a"
          element={
            <div>
              <h1>Page A</h1>
              <Link to="/b">ir a B</Link>
              <Link to="/a">recargar A</Link>
            </div>
          }
        />
        <Route
          path="/b"
          element={
            <div>
              <h1>Page B</h1>
              <Link to="/a">ir a A</Link>
            </div>
          }
        />
      </Routes>
    </MemoryRouter>
  )
}

describe('ScrollToTop', () => {
  beforeEach(() => {
    window.scrollTo = vi.fn()
    // jsdom's History does not implement scrollRestoration; define it so the
    // component's `manual` assignment is observable in tests.
    Object.defineProperty(window.history, 'scrollRestoration', {
      value: 'auto',
      configurable: true,
      writable: true,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('scrolls to the top on the first render', () => {
    renderApp()
    expect(window.scrollTo).toHaveBeenCalledWith(0, 0)
  })

  it('scrolls to the top when navigating between routes', async () => {
    renderApp()
    window.scrollTo.mockClear()

    await userEvent.click(screen.getByRole('link', { name: 'ir a B' }))
    expect(screen.getByRole('heading', { name: 'Page B' })).toBeInTheDocument()
    expect(window.scrollTo).toHaveBeenCalledWith(0, 0)
  })

  it('scrolls to the top when navigating to the SAME path (navbar re-click)', async () => {
    renderApp()
    window.scrollTo.mockClear()

    // Clicking a link to the current path does not change location.key, so the
    // effect alone cannot fire — the capture-phase click handler must reset.
    await userEvent.click(screen.getByRole('link', { name: 'recargar A' }))
    expect(window.scrollTo).toHaveBeenCalledWith(0, 0)
  })

  it('disables native browser scroll restoration so it cannot fight the reset', () => {
    renderApp()
    expect(window.history.scrollRestoration).toBe('manual')
  })
})