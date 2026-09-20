import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import NotFound from './NotFound.jsx'

describe('NotFound', () => {
  it('renders the 404 heading in large typography', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    expect(screen.getByText('404')).toBeInTheDocument()
  })

  it('has a descriptive message', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    expect(
      screen.getByText(/la página que buscás no existe/i)
    ).toBeInTheDocument()
  })

  it('has a link back to the home page via the "Volver al inicio" button', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    const homeButton = screen.getByRole('link', { name: /volver al inicio/i })
    expect(homeButton).toBeInTheDocument()
    expect(homeButton).toHaveAttribute('href', '/')
  })

  it('renders the Button component with the solid accent (brand purple) style', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    const link = screen.getByRole('link', { name: /volver al inicio/i })
    const btn = link.querySelector('button')
    expect(btn).toBeTruthy()
    expect(btn.className).toContain('bg-accent')
  })
})
