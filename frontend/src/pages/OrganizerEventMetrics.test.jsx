import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import OrganizerEventMetrics from './OrganizerEventMetrics.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
  useParams: () => ({ id: 'event-1' }),
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

const mockMetrics = {
  id: 'metrics-1',
  eventId: 'event-1',
  eventName: 'Recital de Rock Nacional',
  eventDate: '2026-08-15T21:00:00Z',
  ticketsSold: 120,
  totalRevenue: 1800000,
  remainingInventory: 30,
  ticketsScanned: 45,
}

describe('OrganizerEventMetrics', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockNavigate.mockReset()
  })

  it('renders loading state initially', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<OrganizerEventMetrics />)

    expect(screen.getByText(/cargando metricas/i)).toBeInTheDocument()
  })

  it('fetches and displays metrics data correctly', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Check all metric fields are displayed
    expect(screen.getByText('120')).toBeInTheDocument() // ticketsSold
    expect(screen.getByText('$ 1.800.000,00')).toBeInTheDocument() // totalRevenue
    expect(screen.getByText('30')).toBeInTheDocument() // remainingInventory
    expect(screen.getByText('45')).toBeInTheDocument() // ticketsScanned

    // Check date is formatted
    expect(screen.getByText(/agosto/i)).toBeInTheDocument()
    expect(screen.getByText(/2026/i)).toBeInTheDocument()

    // Check that the correct endpoint was called
    expect(mockGet).toHaveBeenCalledWith(
      '/metrics/events/event-1',
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    )
  })

  it('shows error state on API failure', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    // "Volver al dashboard" button should be present
    const backBtn = screen.getByRole('button', { name: /volver al dashboard/i })
    expect(backBtn).toBeInTheDocument()
  })

  it('shows "Evento no encontrado" on 404', async () => {
    mockGet.mockRejectedValue({
      response: { status: 404, data: {} },
    })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/evento no encontrado/i)).toBeInTheDocument()
    })

    const backBtn = screen.getByRole('button', { name: /volver al dashboard/i })
    expect(backBtn).toBeInTheDocument()
  })

  it('"Volver al dashboard" button navigates to /organizer/dashboard', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const backBtn = screen.getByRole('button', { name: /volver al dashboard/i })
    await userEvent.click(backBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/dashboard')
  })

  it('handles AbortController cleanup on unmount', async () => {
    const abortSpy = vi.spyOn(AbortController.prototype, 'abort')
    mockGet.mockResolvedValue({ data: mockMetrics })

    const { unmount } = render(<OrganizerEventMetrics />)

    unmount()

    expect(abortSpy).toHaveBeenCalled()
    abortSpy.mockRestore()
  })

  it('formats currency as Argentine pesos', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText('$ 1.800.000,00')).toBeInTheDocument()
    })
  })

  it('formats date with locale string', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      // Date format: "15 de agosto de 2026" (es-AR full date format)
      expect(screen.getByText(/15 de agosto de 2026/i)).toBeInTheDocument()
    })
  })

  it('displays zero values correctly', async () => {
    mockGet.mockResolvedValue({
      data: { ...mockMetrics, ticketsSold: 0, totalRevenue: 0, ticketsScanned: 0 },
    })

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Zero values should render as "0" and "$ 0,00"
    expect(screen.getByText('$ 0,00')).toBeInTheDocument()

    // Check zero numeric displays - the metrics grid shows three zeros
    const zeroElements = screen.getAllByText('0')
    expect(zeroElements.length).toBeGreaterThanOrEqual(2) // ticketsSold=0, ticketsScanned=0
  })

  it('handles fallback error message when no response data', async () => {
    mockGet.mockRejectedValue(new Error('Network Error'))

    render(<OrganizerEventMetrics />)

    await waitFor(() => {
      expect(screen.getByText(/network error/i)).toBeInTheDocument()
    })
  })
})
