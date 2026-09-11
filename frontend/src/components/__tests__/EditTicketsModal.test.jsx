import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EditTicketsModal from '../EditTicketsModal.jsx'
import { queryKeys } from '../../lib/queryKeys.js'

const mockPut = vi.fn()
const mockInvalidateQueries = vi.fn()
const mockUseManagementEvent = vi.fn()

vi.mock('../../hooks/useManagementEvent.js', () => ({
  useManagementEvent: (...args) => mockUseManagementEvent(...args),
}))

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: (...args) => mockInvalidateQueries(...args) }),
}))

vi.mock('../../api/client.js', () => ({
  default: {
    put: (...args) => mockPut(...args),
  },
}))

const defaultTypes = [
  { id: 'tt-1', name: 'General', price: 1500, quantity: 100, available: 100 },
  { id: 'tt-2', name: 'VIP', price: 3000, quantity: 20, available: 20 },
]

function loadEvent(types = defaultTypes) {
  mockUseManagementEvent.mockReturnValue({
    data: { id: 'event-1', ticketTypes: types },
    isLoading: false,
  })
}

function renderModal(props = {}) {
  return render(
    <EditTicketsModal
      eventId="event-1"
      eventName="Festival de Verano"
      onClose={vi.fn()}
      onSuccess={vi.fn()}
      {...props}
    />
  )
}

describe('EditTicketsModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPut.mockReset()
    mockInvalidateQueries.mockReset()
    mockUseManagementEvent.mockReset()
  })

  it('loads ticket types via useManagementEvent and renders editable rows', async () => {
    loadEvent()
    renderModal()

    expect(mockUseManagementEvent).toHaveBeenCalledWith('event-1')
    expect(await screen.findByLabelText('Nombre de la entrada 1')).toHaveValue('General')
    expect(screen.getByLabelText('Nombre de la entrada 2')).toHaveValue('VIP')
    expect(screen.getByRole('dialog', { name: 'Editar entradas' })).toBeInTheDocument()
  })

  it('shows a loading state while the management event is fetching', () => {
    mockUseManagementEvent.mockReturnValue({ data: undefined, isLoading: true })
    renderModal()

    expect(screen.getByText(/cargando tipos de entrada/i)).toBeInTheDocument()
  })

  it('submits the complete list in one PUT and invalidates the three queries', async () => {
    loadEvent()
    mockPut.mockResolvedValue({ data: [] })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    // Edit row 1
    const name1 = await screen.findByLabelText('Nombre de la entrada 1')
    await userEvent.clear(name1)
    await userEvent.type(name1, 'General Nuevo')
    const price1 = screen.getByLabelText('Precio de la entrada 1')
    await userEvent.clear(price1)
    await userEvent.type(price1, '2000')
    const qty1 = screen.getByLabelText('Cantidad de la entrada 1')
    await userEvent.clear(qty1)
    await userEvent.type(qty1, '80')

    // Add a new type with a $0 price (ATE-009: mirrors price >= 0)
    await userEvent.click(screen.getByRole('button', { name: /agregar tipo de entrada/i }))
    await userEvent.type(screen.getByLabelText('Nombre de la entrada 3'), 'VIP Gold')
    await userEvent.type(screen.getByLabelText('Precio de la entrada 3'), '0')
    await userEvent.type(screen.getByLabelText('Cantidad de la entrada 3'), '5')

    // Delete row 2
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar VIP' }))

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => {
      expect(mockPut).toHaveBeenCalledWith('/admin/events/event-1/ticket-types', {
        ticketTypes: [
          { id: 'tt-1', name: 'General Nuevo', price: 2000, quantity: 80 },
          { name: 'VIP Gold', price: 0, quantity: 5 },
        ],
      })
    })

    expect(mockInvalidateQueries).toHaveBeenCalledWith({
      queryKey: queryKeys.managementEvent('event-1'),
    })
    expect(mockInvalidateQueries).toHaveBeenCalledWith({ queryKey: queryKeys.event('event-1') })
    expect(mockInvalidateQueries).toHaveBeenCalledWith({ queryKey: queryKeys.events })
    expect(onSuccess).toHaveBeenCalledTimes(1)
  })

  it('rejects an empty name and focuses the first invalid field with a role="alert"', async () => {
    loadEvent()
    renderModal()

    const name2 = await screen.findByLabelText('Nombre de la entrada 2')
    await userEvent.clear(name2)

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/nombre es obligatorio/i)
    expect(name2).toHaveFocus()
    expect(mockPut).not.toHaveBeenCalled()
  })

  it('rejects a quantity above the per-type cap without calling the API', async () => {
    loadEvent()
    renderModal()

    const qty1 = await screen.findByLabelText('Cantidad de la entrada 1')
    await userEvent.clear(qty1)
    await userEvent.type(qty1, '1001')

    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/cantidad/i)
    expect(mockPut).not.toHaveBeenCalled()
  })

  it('explains that add-only remains available on a 409 ticket-types-referenced', async () => {
    loadEvent()
    mockPut.mockRejectedValue({
      response: {
        status: 409,
        data: {
          type: 'ticket-types-referenced',
          detail: 'This event already has tickets or reservations.',
        },
      },
    })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    await screen.findByLabelText('Nombre de la entrada 1')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/agregar entradas/i)
    expect(onSuccess).not.toHaveBeenCalled()
  })

  it('surfaces a generic backend error without succeeding', async () => {
    loadEvent()
    mockPut.mockRejectedValue({
      response: { status: 400, data: { error: 'At least one ticket type is required' } },
    })
    const onSuccess = vi.fn()
    renderModal({ onSuccess })

    await screen.findByLabelText('Nombre de la entrada 1')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/at least one ticket type/i)
    expect(onSuccess).not.toHaveBeenCalled()
  })

  it('calls onClose when cancelled', async () => {
    loadEvent()
    const onClose = vi.fn()
    renderModal({ onClose })

    await screen.findByLabelText('Nombre de la entrada 1')
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onClose).toHaveBeenCalledTimes(1)
    expect(mockPut).not.toHaveBeenCalled()
  })
})
