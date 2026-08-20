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
      expect(
        screen.getByRole('button', {
          name: /ver precio y opciones de recital de rock nacional/i,
        })
      ).toBeInTheDocument()
    })
    expect(
      screen.getByRole('button', {
        name: /ver precio y opciones de feria de emprendedores/i,
      })
    ).toBeInTheDocument()
    expect(screen.getAllByText(/estadio luna park/i)).not.toHaveLength(0)
    expect(screen.getAllByText(/la rural/i)).not.toHaveLength(0)
    expect(screen.getByAltText(/recital de rock nacional/i)).toHaveAttribute(
      'src',
      'https://example.com/rock.jpg'
    )
    expect(screen.getByText(/desde \$\s*15\.000/i)).toBeInTheDocument()
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
      expect(
        screen.getByRole('button', {
          name: /ver precio y opciones de recital de rock nacional/i,
        })
      ).toBeInTheDocument()
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

  it('reveals each event detail link after flipping its card', async () => {
    mockGet.mockResolvedValue({ data: mockEvents })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(
        screen.getByRole('button', {
          name: /ver precio y opciones de recital de rock nacional/i,
        })
      ).toBeInTheDocument()
    })

    const rockFlip = screen.getByRole('button', {
      name: /ver precio y opciones de recital de rock nacional/i,
    })
    await userEvent.click(rockFlip)

    expect(screen.getByRole('link', { name: /comprar entradas/i })).toHaveAttribute(
      'href',
      '/events/event-1'
    )

    await userEvent.click(
      screen.getByRole('button', {
        name: /volver a la información de recital de rock nacional/i,
      })
    )

    const feriaFlip = screen.getByRole('button', {
      name: /ver precio y opciones de feria de emprendedores/i,
    })
    await userEvent.click(feriaFlip)

    const detailLinks = screen.getAllByRole('link', { name: /comprar entradas/i })
    expect(detailLinks).toHaveLength(2)
    expect(detailLinks.map((link) => link.getAttribute('href'))).toEqual(
      expect.arrayContaining(['/events/event-1', '/events/event-2'])
    )
  })

  it('event cards expose native button flip controls', async () => {
    mockGet.mockResolvedValue({ data: mockEvents })

    renderWithQueryClient(<EventList />)

    await waitFor(() => {
      expect(
        screen.getByRole('button', {
          name: /ver precio y opciones de recital de rock nacional/i,
        })
      ).toBeInTheDocument()
    })

    const flipControls = screen.getAllByRole('button', {
      name: /ver precio y opciones/i,
    })
    expect(flipControls).toHaveLength(2)
    expect(flipControls.every((control) => control.tagName === 'BUTTON')).toBe(true)
  })
})
