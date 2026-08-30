import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import userEvent from '@testing-library/user-event'
import Navbar from '../Navbar.jsx'

vi.mock('../../../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

import { useAuth } from '../../../context/auth.js'

function renderNavbar() {
  return render(
    <MemoryRouter>
      <Navbar />
    </MemoryRouter>
  )
}

describe('Navbar', () => {
  beforeEach(() => {
    useAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
      logout: vi.fn(),
    })
  })

  it('does not render a login link when unauthenticated', () => {
    renderNavbar()

    expect(screen.queryByRole('link', { name: /iniciar sesión/i })).not.toBeInTheDocument()
    expect(screen.queryByText('Cerrar sesión')).not.toBeInTheDocument()
    expect(screen.queryByText('U')).not.toBeInTheDocument()
  })

  it('renders the brand logo and no theme toggle', () => {
    renderNavbar()

    // The brand is the ticketera logo image (stacked TICKET/START wordmark).
    const logo = document.querySelector('img[src="/ticketera-logo.webp"]')
    expect(logo).not.toBeNull()
    expect(logo).toHaveAttribute('alt', '')
    expect(screen.getByRole('link', { name: 'TicketStart' })).toBeInTheDocument()
    // Light-only MVP: no theme toggle button should be present.
    expect(
      screen.queryByRole('button', { name: /cambiar a modo/i })
    ).not.toBeInTheDocument()
  })

  it('renders user dropdown trigger when authenticated', () => {
    useAuth.mockReturnValue({
      user: { email: 'test@example.com', name: 'Test User', role: 'Comun' },
      isAuthenticated: true,
      logout: vi.fn(),
    })
    renderNavbar()

    expect(screen.getByText('Test User')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /test user/i })).toHaveAttribute('aria-haspopup', 'true')
    expect(screen.queryByRole('link', { name: /iniciar sesión/i })).not.toBeInTheDocument()
  })

  it('closes the dropdown when clicking outside', async () => {
    useAuth.mockReturnValue({
      user: { email: 'test@example.com', name: 'Test User', role: 'Comun' },
      isAuthenticated: true,
      logout: vi.fn(),
    })
    renderNavbar()

    const trigger = screen.getByRole('button', { name: /test user/i })
    await userEvent.click(trigger)
    expect(screen.getByRole('button', { name: /cerrar sesión/i })).toBeInTheDocument()

    await userEvent.click(document.body)
    expect(screen.queryByRole('button', { name: /cerrar sesión/i })).not.toBeInTheDocument()
  })

  it('toggles scroll shadow class based on window.scrollY', () => {
    Object.defineProperty(window, 'scrollY', {
      value: 0,
      writable: true,
      configurable: true,
    })
    renderNavbar()
    const nav = screen.getByRole('navigation')
    expect(nav).not.toHaveClass('shadow-lg')

    Object.defineProperty(window, 'scrollY', { value: 12, writable: true, configurable: true })
    fireEvent.scroll(window)
    expect(nav).toHaveClass('shadow-lg')

    Object.defineProperty(window, 'scrollY', { value: 0, writable: true, configurable: true })
    fireEvent.scroll(window)
    expect(nav).not.toHaveClass('shadow-lg')
  })

  it('removes the scroll and resize listeners on unmount', () => {
    const removeListenerSpy = vi.spyOn(window, 'removeEventListener')
    const { unmount } = renderNavbar()

    unmount()
    expect(removeListenerSpy).toHaveBeenCalledTimes(2)
    expect(removeListenerSpy).toHaveBeenCalledWith('scroll', expect.any(Function))
    expect(removeListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))

    removeListenerSpy.mockRestore()
  })

  it('renders Staff link only for staff users', () => {
    useAuth.mockReturnValue({
      user: { email: 'user@example.com', name: 'User', role: 'Comun' },
      isAuthenticated: true,
      logout: vi.fn(),
    })
    const { rerender } = renderNavbar()
    expect(screen.queryByRole('link', { name: /escanear/i })).not.toBeInTheDocument()

    useAuth.mockReturnValue({
      user: { email: 'staff@example.com', name: 'Staff', role: 'Staff' },
      isAuthenticated: true,
      logout: vi.fn(),
    })
    rerender(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    )
    expect(screen.getByRole('link', { name: /escanear/i })).toBeInTheDocument()
  })
})
