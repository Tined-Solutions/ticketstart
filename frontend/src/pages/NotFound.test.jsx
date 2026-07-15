import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import NotFound from './NotFound.jsx'

describe('NotFound', () => {
  it('renders the 404 heading', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    expect(screen.getByText(/404/i)).toBeInTheDocument()
    expect(screen.getByText(/pagina no encontrada/i)).toBeInTheDocument()
  })

  it('has a link back to the home page', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    const homeLink = screen.getByRole('link', { name: /volver al inicio/i })
    expect(homeLink).toBeInTheDocument()
    expect(homeLink).toHaveAttribute('href', '/')
  })
})
