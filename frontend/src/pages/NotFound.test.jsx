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
      screen.getByText(/the page you're looking for doesn't exist/i)
    ).toBeInTheDocument()
  })

  it('has a link back to the home page via the Go Home button', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    const homeButton = screen.getByRole('link', { name: /go home/i })
    expect(homeButton).toBeInTheDocument()
    expect(homeButton).toHaveAttribute('href', '/')
  })

  it('renders the Button component with gradient variant', () => {
    render(
      <MemoryRouter>
        <NotFound />
      </MemoryRouter>
    )

    const link = screen.getByRole('link', { name: /go home/i })
    const btn = link.querySelector('button')
    expect(btn).toBeTruthy()
    expect(btn.className).toContain('to-violet')
  })
})
