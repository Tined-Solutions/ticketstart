import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
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
  it('links to the event detail page', () => {
    renderCard()
    const link = screen.getByRole('link', { name: /ver detalle de recital de rock nacional/i })
    expect(link).toHaveAttribute('href', '/events/event-1')
    expect(link.tagName).toBe('A')
  })

  it('renders the event image with the event name as alt', () => {
    renderCard()
    expect(screen.getByAltText(/recital de rock nacional/i)).toHaveAttribute(
      'src',
      'https://example.com/rock.jpg'
    )
  })

  it('renders the price range from ticket types', () => {
    renderCard()
    expect(screen.getByText(/\$\s*15\.000 — \$\s*25\.000/)).toBeInTheDocument()
  })

  it('renders a date badge', () => {
    renderCard()
    // Badge renders the formatted event date
    expect(screen.getByText(/agosto de 2026/i)).toBeInTheDocument()
  })

  it('renders event name and location', () => {
    renderCard()
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
  })

  it('shows "Sin imagen" placeholder when no image', () => {
    renderCard({ ...event, imageUrl: null })
    expect(screen.getByText(/sin imagen/i)).toBeInTheDocument()
  })
})
