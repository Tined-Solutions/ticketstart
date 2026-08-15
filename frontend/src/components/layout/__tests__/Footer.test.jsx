import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Footer from '../Footer.jsx'

function renderFooter() {
  return render(
    <MemoryRouter>
      <Footer />
    </MemoryRouter>
  )
}

describe('Footer', () => {
  it('renders copyright line with current year', () => {
    renderFooter()
    const year = new Date().getFullYear()
    expect(
      screen.getByText(new RegExp(`${year} TicketStart`, 'i'))
    ).toBeInTheDocument()
  })

  it('renders the "Powered by TicketStart" link', () => {
    renderFooter()
    const link = screen.getByRole('link', { name: /powered by TicketStart/i })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/')
  })

  it('renders a link to the FAQ page', () => {
    renderFooter()
    const link = screen.getByRole('link', { name: /preguntas frecuentes/i })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/faq')
  })

  it('renders as a <footer> element', () => {
    const { container } = renderFooter()
    const footer = container.querySelector('footer')
    expect(footer).toBeTruthy()
  })
})
