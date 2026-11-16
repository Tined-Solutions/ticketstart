import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import EventSummaryTicket from '../EventSummaryTicket.jsx'

const event = {
  name: 'Recital de Rock Nacional',
  imageUrl: null,
  date: '2026-12-15T21:00:00Z',
  location: 'Estadio Luna Park',
}

describe('EventSummaryTicket', () => {
  it('shows the total and the service fee it includes', () => {
    render(
      <EventSummaryTicket
        event={event}
        selectionName="Platea"
        quantity={2}
        totalPrice={33000}
        serviceFee={3000}
      />,
    )

    expect(screen.getByText(/\$\s*33\.000/)).toBeInTheDocument()
    expect(screen.getByText(/incluye cargo por servicio de \$\s*3\.000/i)).toBeInTheDocument()
  })

  it('omits the fee line when no service fee is known', () => {
    render(
      <EventSummaryTicket event={event} selectionName="Platea" quantity={2} totalPrice={30000} />,
    )

    expect(screen.getByText(/\$\s*30\.000/)).toBeInTheDocument()
    expect(screen.queryByText(/cargo por servicio/i)).not.toBeInTheDocument()
  })
})
