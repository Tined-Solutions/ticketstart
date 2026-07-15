import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import EventForm from '../EventForm.jsx'

const mockPost = vi.fn()
const mockPut = vi.fn()
const mockOnSuccess = vi.fn()

vi.mock('../../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
    put: (...args) => mockPut(...args),
  },
}))

describe('EventForm — eventId validation before PUT', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPut.mockReset()
    mockOnSuccess.mockReset()
  })

  it('does not send a PUT request when eventId is missing in edit mode', async () => {
    // initialData without an id
    const event = {
      name: 'Test Event',
      date: '2026-12-25T20:00:00Z',
      location: 'Somewhere',
      description: 'Test',
      ticketTypes: [{ id: 'tt-1', name: 'General', price: 5000, quantity: 100 }],
    }

    render(
      <EventForm mode="edit" initialData={event} onSuccess={mockOnSuccess} />
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // Should NOT call PUT since eventId is undefined
    expect(mockPut).not.toHaveBeenCalled()
    // Should show validation error
    expect(screen.getByText(/no se pudo identificar el evento/i)).toBeInTheDocument()
  })

  it('shows error feedback when catch block is entered in edit mode', async () => {
    mockPut.mockRejectedValueOnce(new Error('Server error'))

    const event = {
      id: 'event-1',
      name: 'Test Event',
      date: '2026-12-25T20:00:00Z',
      location: 'Somewhere',
      description: 'Test',
      ticketTypes: [{ id: 'tt-1', name: 'General', price: 5000, quantity: 100 }],
    }

    render(
      <EventForm mode="edit" initialData={event} onSuccess={mockOnSuccess} />
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // The feedback should be rendered with role="alert" for error type
    const feedback = screen.getByRole('alert')
    expect(feedback).toBeInTheDocument()
    expect(feedback.textContent).toBeTruthy()
    expect(mockOnSuccess).not.toHaveBeenCalled()
  })
})
