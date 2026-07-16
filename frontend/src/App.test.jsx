import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from './App.jsx'

vi.mock('./context/auth.js', () => ({
  useAuth: vi.fn(),
}))

vi.mock('./hooks/useTheme.jsx', async (importOriginal) => {
  const original = await importOriginal()
  return {
    ...original,
    useTheme: vi.fn(),
  }
})

import { useAuth } from './context/auth.js'
import { useTheme } from './hooks/useTheme.jsx'

describe('App routing', () => {
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

  it('shows 404 page when navigating to /register', () => {
    render(
      <MemoryRouter initialEntries={['/register']}>
        <App />
      </MemoryRouter>
    )

    expect(screen.getByText(/404/i)).toBeInTheDocument()
    expect(screen.getByText(/doesn't exist/i)).toBeInTheDocument()
  })
})
