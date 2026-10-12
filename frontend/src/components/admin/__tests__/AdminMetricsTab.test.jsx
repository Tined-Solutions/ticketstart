import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminMetricsTab from '../AdminMetricsTab.jsx'

const mockGet = vi.fn()

vi.mock('../../../api/client.js', () => ({
  default: { get: (...args) => mockGet(...args) },
}))

const metricsResponse = {
  charged: 1480000,
  refunded: 234700,
  net: 1245300,
  ticketsSold: 1248,
  refundOperations: 12,
  events: [
    {
      eventId: 'e-1',
      eventName: 'Festival Cordillera',
      eventDate: '2026-09-24T21:00:00Z',
      isPast: false,
      ticketsSold: 380,
      charged: 456000,
      refunded: 36000,
      net: 420000,
    },
    {
      eventId: 'e-2',
      eventName: 'Noche de Jazz',
      eventDate: '2026-09-18T20:00:00Z',
      isPast: true,
      ticketsSold: 95,
      charged: 114000,
      refunded: 0,
      net: 114000,
    },
  ],
}

describe('AdminMetricsTab', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockGet.mockResolvedValue({ data: metricsResponse })
  })

  it('renders the monetary KPIs and the per-event net table', async () => {
    render(<AdminMetricsTab />)

    await waitFor(() => {
      expect(screen.getByText('Neto percibido')).toBeInTheDocument()
    })

    // es-AR currency formatting ($ 1.245.300, sin decimales por defecto).
    expect(screen.getByText('$ 1.245.300')).toBeInTheDocument()
    expect(screen.getByText('$ 1.480.000')).toBeInTheDocument()
    expect(screen.getByText('$ 234.700')).toBeInTheDocument()
    expect(screen.getByText('12 devoluciones')).toBeInTheDocument()

    expect(screen.getByText('Festival Cordillera')).toBeInTheDocument()
    expect(screen.getByText('Noche de Jazz')).toBeInTheDocument()
    expect(screen.getByText('Finalizado')).toBeInTheDocument()
    expect(screen.getByText('Activo')).toBeInTheDocument()

    // The fetch carried the default local month range and no lifecycle filter.
    expect(mockGet).toHaveBeenCalledWith(
      '/metrics/admin',
      expect.objectContaining({
        params: expect.objectContaining({ from: expect.any(String), to: expect.any(String) }),
      })
    )
    const sentParams = mockGet.mock.calls[0][1].params
    expect(sentParams.eventState).toBeUndefined()
  })

  it('refetches when the event state filter changes and clears back to defaults', async () => {
    render(<AdminMetricsTab />)
    await waitFor(() => expect(screen.getByText('Neto percibido')).toBeInTheDocument())

    await userEvent.selectOptions(screen.getByLabelText('Estado del evento'), 'past')

    await waitFor(() => {
      expect(mockGet).toHaveBeenLastCalledWith(
        '/metrics/admin',
        expect.objectContaining({ params: expect.objectContaining({ eventState: 'past' }) })
      )
    })

    await userEvent.click(screen.getByRole('button', { name: /limpiar/i }))

    await waitFor(() => {
      const lastParams = mockGet.mock.calls.at(-1)[1].params
      expect(lastParams.eventState).toBeUndefined()
      expect(lastParams.from).toEqual(expect.any(String))
    })
  })

  it('filters by a picked date with the shared calendar (date-only, no time)', async () => {
    render(<AdminMetricsTab />)
    await waitFor(() => expect(screen.getByText('Neto percibido')).toBeInTheDocument())

    await userEvent.click(screen.getByLabelText('Desde'))

    const dialog = await screen.findByRole('dialog', { name: /^seleccionar fecha$/i })
    // Date mode reuses the shared picker but renders no time controls.
    expect(within(dialog).queryByLabelText('Hora')).not.toBeInTheDocument()
    expect(within(dialog).queryByLabelText('Minutos')).not.toBeInTheDocument()

    // Day 2 of the current month always differs from the default "from" (the
    // 1st); the regex tolerates the "Hoy, " prefix when the test runs on the 2nd.
    const now = new Date()
    const targetDay = new Date(now.getFullYear(), now.getMonth(), 2)
    const monthLabel = new Intl.DateTimeFormat('es-AR', { month: 'long' }).format(targetDay)
    await userEvent.click(
      within(dialog).getByRole('button', { name: new RegExp(`\\b2 de ${monthLabel}`, 'i') })
    )
    await userEvent.click(within(dialog).getByRole('button', { name: /^listo$/i }))

    await waitFor(() => {
      expect(mockGet.mock.calls.at(-1)[1].params.from).toBe(targetDay.toISOString())
    })
  })

  it('paginates the per-event table 10 rows per page', async () => {
    mockGet.mockResolvedValue({
      data: {
        ...metricsResponse,
        events: Array.from({ length: 12 }, (_, i) => ({
          eventId: `e-${i}`,
          eventName: `Evento ${i}`,
          eventDate: '2026-09-10T20:00:00Z',
          isPast: true,
          ticketsSold: 1,
          charged: 100 * (12 - i),
          refunded: 0,
          net: 100 * (12 - i),
        })),
      },
    })

    render(<AdminMetricsTab />)

    await waitFor(() => expect(screen.getByText('Página 1 de 2')).toBeInTheDocument())

    const countRows = () => screen.getAllByRole('row').length - 1 // minus the header row
    expect(countRows()).toBe(10)
    expect(screen.getByText('Evento 0')).toBeInTheDocument()
    expect(screen.queryByText('Evento 11')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /página siguiente/i }))

    expect(countRows()).toBe(2)
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByText('Evento 11')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /página anterior/i })).toBeEnabled()
    expect(screen.getByRole('button', { name: /página siguiente/i })).toBeDisabled()
  })
})
