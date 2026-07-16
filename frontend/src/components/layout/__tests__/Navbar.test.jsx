import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import userEvent from '@testing-library/user-event'
import Navbar from '../Navbar.jsx'

vi.mock('../../../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

vi.mock('../../../hooks/useTheme.jsx', async (importOriginal) => {
  const original = await importOriginal()
  return {
    ...original,
    useTheme: vi.fn(),
  }
})

import { useAuth } from '../../../context/auth.js'
import { useTheme } from '../../../hooks/useTheme.jsx'

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
    useTheme.mockReturnValue({
      theme: 'dark',
      setTheme: vi.fn(),
      toggle: vi.fn(),
    })
  })

  it('renders Sign In link when unauthenticated', () => {
    renderNavbar()

    expect(screen.getByRole('link', { name: /sign in/i })).toBeInTheDocument()
    expect(screen.queryByText('Sign Out')).not.toBeInTheDocument()
    expect(screen.queryByText('U')).not.toBeInTheDocument()
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
    expect(screen.queryByRole('link', { name: /sign in/i })).not.toBeInTheDocument()
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
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument()

    await userEvent.click(document.body)
    expect(screen.queryByRole('button', { name: /sign out/i })).not.toBeInTheDocument()
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

  it('removes the scroll listener on unmount', () => {
    const removeListenerSpy = vi.spyOn(window, 'removeEventListener')
    const { unmount } = renderNavbar()

    unmount()
    expect(removeListenerSpy).toHaveBeenCalledTimes(1)
    expect(removeListenerSpy).toHaveBeenCalledWith('scroll', expect.any(Function))

    removeListenerSpy.mockRestore()
  })

  it('renders Staff link only for staff users', () => {
    useAuth.mockReturnValue({
      user: { email: 'user@example.com', name: 'User', role: 'Comun' },
      isAuthenticated: true,
      logout: vi.fn(),
    })
    const { rerender } = renderNavbar()
    expect(screen.queryByRole('link', { name: /scan/i })).not.toBeInTheDocument()

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
    expect(screen.getByRole('link', { name: /scan/i })).toBeInTheDocument()
  })
})
