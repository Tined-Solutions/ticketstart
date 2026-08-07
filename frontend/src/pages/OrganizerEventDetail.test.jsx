import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import OrganizerEventDetail from './OrganizerEventDetail.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'

const mockGet = vi.fn()

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

function renderEventDetail(id = 'evt-1') {
  return renderWithQueryClient(
    <MemoryRouter initialEntries={[`/organizer/events/${id}`]}>
      <Routes>
        <Route path="/organizer/events/:id" element={<OrganizerEventDetail />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('OrganizerEventDetail — fetch URL', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
  })

  it('uses the authenticated manage endpoint for fetching event data', async () => {
    mockGet.mockResolvedValue({
      data: {
        id: 'evt-1',
        name: 'Test Event',
        date: '2026-12-25T20:00:00Z',
        location: 'TEATRO',
        description: 'Great event',
        ticketTypes: [],
      },
    })

    renderEventDetail()

    // The component should call GET /events/{id}/manage, not GET /events/{id}
    await screen.findByText(/editar evento/i)

    const calls = mockGet.mock.calls
    const urlCall = calls.find(([url]) => url.includes('evt-1'))
    expect(urlCall).toBeTruthy()
    expect(urlCall[0]).toBe('/events/evt-1/manage')
  })
})

// ── Visual Regression: Glass & Theme ──────────────────────────────────

describe('OrganizerEventDetail — Visual Regression', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
  })

  it('renders GlassCard in the loading state', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    renderEventDetail()

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/cargando evento/i)).toBeInTheDocument()
  })

  it('renders GlassCard in the error state', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Server error' } } },
    })

    renderEventDetail()

    await waitFor(() => {
      expect(screen.getByText(/server error/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders "Editar evento" heading on successful load', async () => {
    mockGet.mockResolvedValue({
      data: {
        id: 'evt-1',
        name: 'Test Event',
        date: '2026-12-25T20:00:00Z',
        location: 'TEATRO',
        description: 'Great event',
        ticketTypes: [],
      },
    })

    renderEventDetail()

    await screen.findByText(/editar evento/i)
    expect(screen.getByRole('heading', { name: /editar evento/i })).toBeInTheDocument()
  })
})
