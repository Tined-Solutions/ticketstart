import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import OrganizerEventNew from './OrganizerEventNew.jsx'

vi.mock('../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

// Only the wiring is under test here: which roles get the confirmation step.
vi.mock('../components/EventForm.jsx', () => ({
  default: ({ confirmBeforeCreate }) => (
    <div data-testid="event-form" data-confirm={String(Boolean(confirmBeforeCreate))} />
  ),
}))

import { useAuth } from '../context/auth.js'

function renderPage(role) {
  useAuth.mockReturnValue({ user: { role }, isAuthenticated: true })
  return render(
    <MemoryRouter>
      <OrganizerEventNew />
    </MemoryRouter>,
  )
}

describe('OrganizerEventNew', () => {
  it('asks organizers to confirm before creating (they cannot edit afterwards)', () => {
    renderPage('Organizador')
    expect(screen.getByTestId('event-form')).toHaveAttribute('data-confirm', 'true')
  })

  it('lets admins create directly (they can still edit the event)', () => {
    renderPage('Admin')
    expect(screen.getByTestId('event-form')).toHaveAttribute('data-confirm', 'false')
  })
})
