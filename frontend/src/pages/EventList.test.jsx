import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventList from './EventList.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'

const mockGet = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children, ...props }) => (
    <a href={to} {...props}>
      {children}
    </a>
  ),
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

const mockEvents = [
  {
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
  },
  {
    id: 'event-2',
    name: 'Feria de Emprendedores',
    description: 'Descubri productos locales.',
    date: '2026-09-01T14:00:00Z',
    location: 'La Rural, Buenos Aires',
    imageUrl: null,
    organizerId: 'user-2',
    ticketTypes: [{ id: 'tt-3', name: 'General', price: 0, quantity: 500, available: 500 }],
  },
]

describe('EventList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
  })

  it('renders event cards from API data', async () => {
    mockGet.mockResolvedValue({ data: mockEvents })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })
    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
    expect(screen.getByText(/la rural/i)).toBeInTheDocument()
    expect(screen.getByAltText(/recital de rock nacional/i)).toHaveAttribute(
      'src',
      'https://example.com/rock.jpg'
    )
    expect(
      screen.getByText(/\$\s*15\.000 — \$\s*25\.000/)
    ).toBeInTheDocument()
  })

  it('shows loading state while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    renderWithQueryClient(<EventList />)

    expect(screen.getByRole('heading', { name: /eventos/i })).toBeInTheDocument()
    expect(document.querySelectorAll('.event-card-skeleton').length).toBeGreaterThan(0)
  })

  it('shows error state with retry button', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    mockGet.mockResolvedValue({ data: mockEvents })
    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })
  })

  it('shows empty state when no events exist', async () => {
    mockGet.mockResolvedValue({ data: [] })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(screen.getByText(/no hay eventos disponibles/i)).toBeInTheDocument()
    })
    expect(screen.getByRole('link', { name: /volver al inicio/i })).toHaveAttribute('href', '/')
  })

  it('links each card to its event detail page', async () => {
    mockGet.mockResolvedValue({ data: mockEvents })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const rockCard = screen.getByRole('link', { name: /ver detalle de recital de rock nacional/i })
    expect(rockCard).toHaveAttribute('href', '/events/event-1')
    const feriaCard = screen.getByRole('link', { name: /ver detalle de feria de emprendedores/i })
    expect(feriaCard).toHaveAttribute('href', '/events/event-2')
  })

  it('event cards are native <a> links', async () => {
    mockGet.mockResolvedValue({ data: mockEvents })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const eventCard = screen.getByRole('link', { name: /ver detalle de recital de rock nacional/i })
    expect(eventCard.tagName).toBe('A')
  })
})
