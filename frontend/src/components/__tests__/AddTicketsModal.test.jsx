import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AddTicketsModal from '../AddTicketsModal.jsx'
import { queryKeys } from '../../lib/queryKeys.js'

const mockPost = vi.fn()
const mockInvalidateQueries = vi.fn()

vi.mock('../../hooks/useEvent.js', () => ({
  useEvent: () => ({
    data: { ticketTypes: [{ id: 'tt-1', name: 'General', quantity: 100, available: 100 }] },
    isLoading: false,
  }),
}))

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: (...args) => mockInvalidateQueries(...args) }),
}))

vi.mock('../../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
  },
}))

function renderModal(props = {}) {
  return render(
    <AddTicketsModal
      eventId="event-1"
      eventName="Festival de Verano"
      onClose={vi.fn()}
      onSuccess={vi.fn()}
      {...props}
    />
  )
}

describe('AddTicketsModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockInvalidateQueries.mockReset()
  })

  it('increments stock (add-only) and invalidates managementEvent, event and events', async () => {
    mockPost.mockResolvedValue({ data: {} })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    await userEvent.selectOptions(await screen.findByLabelText('Tipo de entrada'), 'tt-1')
    await userEvent.type(screen.getByLabelText('Cantidad a sumar'), '50')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/ticket-types/tt-1/stock', {
        additionalQuantity: 50,
      })
    })

    // ATS-007: three-key invalidation, including the management key.
    expect(mockInvalidateQueries).toHaveBeenCalledWith({
      queryKey: queryKeys.managementEvent('event-1'),
    })
    expect(mockInvalidateQueries).toHaveBeenCalledWith({ queryKey: queryKeys.event('event-1') })
    expect(mockInvalidateQueries).toHaveBeenCalledWith({ queryKey: queryKeys.events })
    expect(onSuccess).toHaveBeenCalledTimes(1)
  })

  it('creates a new type (add-only) and invalidates the three queries', async () => {
    mockPost.mockResolvedValue({ data: {} })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo tipo de entrada' }))
    await userEvent.type(screen.getByLabelText('Nombre'), 'VIP')
    await userEvent.type(screen.getByLabelText('Precio ($)'), '150')
    await userEvent.type(screen.getByLabelText('Cantidad'), '20')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/ticket-types', {
        name: 'VIP',
        price: 150,
        quantity: 20,
      })
    })

    expect(mockInvalidateQueries).toHaveBeenCalledWith({
      queryKey: queryKeys.managementEvent('event-1'),
    })
    expect(onSuccess).toHaveBeenCalledTimes(1)
  })

  it('shows the backend error and neither invalidates nor succeeds', async () => {
    mockPost.mockRejectedValue({ response: { data: { error: 'Invalid payload' } } })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    await userEvent.selectOptions(await screen.findByLabelText('Tipo de entrada'), 'tt-1')
    await userEvent.type(screen.getByLabelText('Cantidad a sumar'), '5')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/invalid payload/i)
    expect(mockInvalidateQueries).not.toHaveBeenCalled()
    expect(onSuccess).not.toHaveBeenCalled()
  })
})
