import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import OrganizerEventDetail from './OrganizerEventDetail.jsx'

const mockGet = vi.fn()

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

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

    render(
      <MemoryRouter initialEntries={['/organizer/events/evt-1']}>
        <Routes>
          <Route path="/organizer/events/:id" element={<OrganizerEventDetail />} />
        </Routes>
      </MemoryRouter>
    )

    // The component should call GET /events/{id}/manage, not GET /events/{id}
    await screen.findByText(/editar evento/i)

    const calls = mockGet.mock.calls
    const urlCall = calls.find(([url]) => url.includes('evt-1'))
    expect(urlCall).toBeTruthy()
    expect(urlCall[0]).toBe('/events/evt-1/manage')
  })
})
