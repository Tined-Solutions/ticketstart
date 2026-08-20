import { describe, it, expect } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import EventCard from '../EventCard.jsx'

const event = {
  id: 'event-1',
  name: 'Recital de Rock Nacional',
  date: '2026-08-15T21:00:00Z',
  location: 'Estadio Luna Park, Buenos Aires',
  imageUrl: 'https://example.com/rock.jpg',
  ticketTypes: [
    { id: 'tt-1', name: 'Platea', price: 15000, quantity: 100, available: 80 },
    { id: 'tt-2', name: 'Campo', price: 25000, quantity: 200, available: 150 },
  ],
}

function renderCard(cardEvent = event) {
  return render(
    <MemoryRouter>
      <EventCard event={cardEvent} />
    </MemoryRouter>
  )
}

describe('EventCard', () => {
  it('starts with an accessible flip control and hides the back link', () => {
    renderCard()
    const flipButton = screen.getByRole('button', {
      name: /ver precio y opciones de recital de rock nacional/i,
    })

    expect(flipButton).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('link', { name: /comprar entradas/i })).not.toBeInTheDocument()
  })

  it('renders the event image with the event name as alt', () => {
    renderCard()
    expect(screen.getByAltText(/recital de rock nacional/i)).toHaveAttribute(
      'src',
      'https://example.com/rock.jpg'
    )
  })

  it('renders the starting price in the ticket stub', () => {
    renderCard()
    const flipButton = screen.getByRole('button', {
      name: /ver precio y opciones de recital de rock nacional/i,
    })

    fireEvent.click(flipButton)

    // The whole ticket (the rotating container) flips in 3D, not just the faces.
    expect(screen.getByTestId('event-card-ticket')).toHaveStyle({
      transform: 'rotateY(180deg)',
    })
    expect(screen.getByText(/desde \$\s*15\.000/i)).toBeInTheDocument()
    expect(screen.queryByText(/\$\s*15\.000 — \$\s*25\.000/)).not.toBeInTheDocument()
  })

  it('reveals a detail link after flipping and can return to the front', () => {
    renderCard()
    const flipButton = screen.getByRole('button', {
      name: /ver precio y opciones de recital de rock nacional/i,
    })

    fireEvent.click(flipButton)

    const detailLink = screen.getByRole('link', { name: /comprar entradas/i })
    expect(detailLink).toHaveAttribute('href', '/events/event-1')
    expect(detailLink).toHaveAttribute('tabindex', '0')

    fireEvent.click(
      screen.getByRole('button', {
        name: /volver a la información de recital de rock nacional/i,
      })
    )

    expect(flipButton).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('link', { name: /comprar entradas/i })).not.toBeInTheDocument()
  })

  it('reveals the event date without a time', () => {
    renderCard()
    fireEvent.click(
      screen.getByRole('button', {
        name: /ver precio y opciones de recital de rock nacional/i,
      })
    )
    expect(screen.getByText('15')).toBeInTheDocument()
    expect(screen.getByText(/ago/i)).toBeInTheDocument()
    expect(screen.queryByText(/2026/)).not.toBeInTheDocument()
    expect(screen.queryByText(/:\d{2}/)).not.toBeInTheDocument()
  })

  it('renders event name and location', () => {
    renderCard()
    fireEvent.click(
      screen.getByRole('button', {
        name: /ver precio y opciones de recital de rock nacional/i,
      })
    )
    expect(
      screen.getByRole('heading', { name: /recital de rock nacional/i })
    ).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
  })

  it('shows "Sin imagen" placeholder when no image', () => {
    renderCard({ ...event, imageUrl: null })
    expect(screen.getByText(/sin imagen/i)).toBeInTheDocument()
  })
})
