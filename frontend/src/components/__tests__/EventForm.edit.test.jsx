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

describe('EventForm — edit mode with photo (EIM-004 upload-first)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPut.mockReset()
    mockOnSuccess.mockReset()
  })

  function buildEvent(overrides = {}) {
    return {
      id: 'event-1',
      name: 'Test Event',
      date: '2026-12-25T20:00:00Z',
      location: 'Somewhere',
      description: 'Test',
      imageUrl: 'https://example.com/old.jpg',
      ticketTypes: [{ id: 'tt-1', name: 'General', price: 5000, quantity: 100 }],
      ...overrides,
    }
  }

  it('uploads the image first and PUTs the new imageUrl', async () => {
    mockPost.mockResolvedValueOnce({ data: { imageUrl: 'https://r2.example.com/new.jpg' } })
    mockPut.mockResolvedValueOnce({ data: {} })

    render(
      <EventForm mode="edit" initialData={buildEvent()} onSuccess={mockOnSuccess} />
    )

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    fireEvent.change(screen.getByLabelText(/imagen del evento/i), {
      target: { files: [file] },
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // EIM-004: upload-first — POST upload, then PUT carrying the returned URL
    expect(mockPost).toHaveBeenCalledTimes(1)
    expect(mockPost.mock.calls[0][0]).toBe('/uploads/event-image')
    expect(mockPost.mock.calls[0][1]).toBeInstanceOf(FormData)
    expect(mockPut).toHaveBeenCalledTimes(1)
    expect(mockPut.mock.calls[0][0]).toBe('/events/event-1')
    expect(mockPut.mock.calls[0][1].imageUrl).toBe('https://r2.example.com/new.jpg')
    expect(mockOnSuccess).toHaveBeenCalledWith('event-1')
  })

  it('blocks the PUT with a red error when the image upload fails', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'Subida fallida' } } },
    })

    render(
      <EventForm mode="edit" initialData={buildEvent()} onSuccess={mockOnSuccess} />
    )

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    fireEvent.change(screen.getByLabelText(/imagen del evento/i), {
      target: { files: [file] },
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // EIM-003/004: honest red alert, PUT never called, no navigation
    expect(screen.getByRole('alert')).toHaveTextContent(/subida fallida/i)
    expect(mockPut).not.toHaveBeenCalled()
    expect(mockOnSuccess).not.toHaveBeenCalled()
    expect(
      screen.getByRole('button', { name: /guardar cambios/i })
    ).not.toBeDisabled()
  })

  it('preserves the current imageUrl when no photo is selected', async () => {
    mockPut.mockResolvedValueOnce({ data: {} })

    render(
      <EventForm mode="edit" initialData={buildEvent()} onSuccess={mockOnSuccess} />
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // No photo → no upload call, PUT keeps the existing imageUrl (EIM-004)
    expect(mockPost).not.toHaveBeenCalled()
    expect(mockPut).toHaveBeenCalledWith(
      '/events/event-1',
      expect.objectContaining({ imageUrl: 'https://example.com/old.jpg' })
    )
    expect(mockOnSuccess).toHaveBeenCalledWith('event-1')
  })
})
