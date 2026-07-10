import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventDetail from './EventDetail.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useParams: () => ({ id: 'event-1' }),
  useNavigate: () => mockNavigate,
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

const mockEvent = {
  id: 'event-1',
  name: 'Recital de Rock Nacional',
  description: 'Una noche imperdible de rock argentino.',
  date: '2026-08-15T21:00:00Z',
  location: 'Estadio Luna Park, Buenos Aires',
  imageUrl: 'https://example.com/rock.jpg',
  organizerId: 'user-1',
  ticketTypes: [
    { id: 'tt-1', name: 'Platea', price: 15000, quantity: 100, available: 80 },
    { id: 'tt-2', name: 'Campo', price: 25000, quantity: 200, available: 150 },
  ],
}

describe('EventDetail', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockNavigate.mockReset()
  })

  it('renders event info from API data', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /recital de rock nacional/i })).toBeInTheDocument()
    })
    expect(screen.getByText(/una noche imperdible/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
    expect(screen.getByAltText(/recital de rock nacional/i)).toHaveAttribute(
      'src',
      'https://example.com/rock.jpg'
    )
  })

  it('shows ticket types with prices and availability', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/\$\s*15\.000,00/)).toBeInTheDocument()
    expect(screen.getByText(/80 disponibles de 100/i)).toBeInTheDocument()
    expect(screen.getByText(/campo/i)).toBeInTheDocument()
    expect(screen.getByText(/\$\s*25\.000,00/)).toBeInTheDocument()
    expect(screen.getByText(/150 disponibles de 200/i)).toBeInTheDocument()
  })

  it('shows loading state while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<EventDetail />)

    expect(screen.getByText(/cargando evento/i)).toBeInTheDocument()
  })

  it('shows error state for a non-existent event', async () => {
    mockGet.mockRejectedValue({ response: { status: 404 } })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/el evento no existe o no esta disponible/i)).toBeInTheDocument()
    })
  })

  it('shows error state with retry button for generic failures', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de servidor' } } },
    })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/error de servidor/i)).toBeInTheDocument()
    })

    mockGet.mockResolvedValue({ data: mockEvent })
    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /recital de rock nacional/i })).toBeInTheDocument()
    })
  })

  it('increments and decrements ticket quantity', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    const plateaSection = screen.getByText(/platea/i).closest('.ticket-type-row')
    const [decreaseButton, increaseButton] = plateaSection.querySelectorAll('button')
    const quantityDisplay = plateaSection.querySelector('span[aria-live="polite"]')

    expect(quantityDisplay).toHaveTextContent('0')

    await userEvent.click(increaseButton)
    expect(quantityDisplay).toHaveTextContent('1')

    await userEvent.click(increaseButton)
    expect(quantityDisplay).toHaveTextContent('2')

    await userEvent.click(decreaseButton)
    expect(quantityDisplay).toHaveTextContent('1')
  })

  it('disables increment when reaching available quantity', async () => {
    mockGet.mockResolvedValue({
      data: {
        ...mockEvent,
        ticketTypes: [{ id: 'tt-3', name: 'VIP', price: 50000, quantity: 2, available: 2 }],
      },
    })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/vip/i)).toBeInTheDocument()
    })

    const vipSection = screen.getByText(/vip/i).closest('.ticket-type-row')
    const [, increaseButton] = vipSection.querySelectorAll('button')

    await userEvent.click(increaseButton)
    await userEvent.click(increaseButton)

    expect(increaseButton).toBeDisabled()
  })

  it('navigates to checkout with selected quantities when clicking reserve', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    const plateaSection = screen.getByText(/platea/i).closest('.ticket-type-row')
    const [, increaseButton] = plateaSection.querySelectorAll('button')
    await userEvent.click(increaseButton)
    await userEvent.click(increaseButton)

    const reserveButton = screen.getByRole('button', { name: /reservar entradas/i })
    await userEvent.click(reserveButton)

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/checkout', {
        state: expect.objectContaining({
          eventId: 'event-1',
          eventName: 'Recital de Rock Nacional',
          eventDate: '2026-08-15T21:00:00Z',
          eventLocation: 'Estadio Luna Park, Buenos Aires',
          eventImageUrl: 'https://example.com/rock.jpg',
          totalTickets: 2,
          totalPrice: 30000,
          selections: [
            {
              ticketTypeId: 'tt-1',
              name: 'Platea',
              price: 15000,
              quantity: 2,
            },
          ],
        }),
      })
    })
  })

  it('disables reserve button when no tickets are selected', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    const reserveButton = screen.getByRole('button', { name: /reservar entradas/i })
    expect(reserveButton).toBeDisabled()
  })

  it('renders back link to the event catalog', async () => {
    mockGet.mockResolvedValue({ data: mockEvent })

    render(<EventDetail />)

    await waitFor(() => {
      expect(screen.getByRole('link', { name: /volver al catalogo/i })).toHaveAttribute(
        'href',
        '/events'
      )
    })
  })
})
