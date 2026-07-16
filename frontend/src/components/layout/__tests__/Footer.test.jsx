import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Footer from '../Footer.jsx'

describe('Footer', () => {
  it('renders copyright line with current year', () => {
    render(<Footer />)
    const year = new Date().getFullYear()
    expect(
      screen.getByText(new RegExp(`${year} Ticketera`, 'i'))
    ).toBeInTheDocument()
  })

  it('renders the "Powered by Ticketera" link', () => {
    render(<Footer />)
    const link = screen.getByRole('link', { name: /powered by ticketera/i })
    expect(link).toBeInTheDocument()
    expect(link).toHaveAttribute('href', '/')
  })

  it('renders as a <footer> element', () => {
    const { container } = render(<Footer />)
    const footer = container.querySelector('footer')
    expect(footer).toBeTruthy()
  })
})
