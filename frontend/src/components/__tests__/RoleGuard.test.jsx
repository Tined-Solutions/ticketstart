import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import RoleGuard from '../RoleGuard.jsx'

vi.mock('../../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

import { useAuth } from '../../context/auth.js'

describe('RoleGuard', () => {
  it('renders children when user has the allowed role', () => {
    useAuth.mockReturnValue({
      user: { id: 'u1', email: 'staff@test.com', role: 'Staff' },
      isAuthenticated: true,
    })

    render(
      <MemoryRouter>
        <RoleGuard allowedRoles={['Staff', 'Admin']}>
          <p>Protected content</p>
        </RoleGuard>
      </MemoryRouter>
    )

    expect(screen.getByText('Protected content')).toBeInTheDocument()
  })

  it('renders a 403 page when user lacks the required role', () => {
    useAuth.mockReturnValue({
      user: { id: 'u1', email: 'user@test.com', role: 'Comun' },
      isAuthenticated: true,
    })

    render(
      <MemoryRouter>
        <RoleGuard allowedRoles={['Staff', 'Admin']}>
          <p>Protected content</p>
        </RoleGuard>
      </MemoryRouter>
    )

    // Should render a 403 page, NOT a redirect/Navigate
    expect(screen.getByText(/403/i)).toBeInTheDocument()
    expect(screen.getByText(/acceso denegado/i)).toBeInTheDocument()
    // Should NOT render the protected children
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })

  it('redirects unauthenticated users to login', () => {
    useAuth.mockReturnValue({
      user: null,
      isAuthenticated: false,
    })

    render(
      <MemoryRouter>
        <RoleGuard allowedRoles={['Staff', 'Admin']}>
          <p>Protected content</p>
        </RoleGuard>
      </MemoryRouter>
    )

    // Should redirect to /login (Navigate component renders nothing visible)
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })
})
