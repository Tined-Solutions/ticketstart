import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Card from '../Card.jsx'

describe('Card — prop filtering', () => {
  it('renders children correctly', () => {
    render(<Card>Hello World</Card>)
    expect(screen.getByText('Hello World')).toBeInTheDocument()
  })

  it('does NOT spread unknown HTML props to the DOM element', () => {
    render(
      <Card data-custom="should-not-appear" onClick={() => {}}>
        Content
      </Card>
    )

    const cardEl = screen.getByText('Content').closest('div')
    // Unknown attribute should NOT be on the DOM element
    expect(cardEl).not.toHaveAttribute('data-custom')
    // onClick should also not be on a plain div as it's not interactive
    expect(cardEl).not.toHaveAttribute('onclick')
  })

  it('preserves known props like className', () => {
    render(<Card className="my-custom-class">Content</Card>)
    const cardEl = document.querySelector('.my-custom-class')
    expect(cardEl).toBeTruthy()
    expect(cardEl).toHaveClass('my-custom-class')
    expect(cardEl).toHaveClass('border')
  })

  it('does not pass aria or data attributes through rest spread', () => {
    render(
      <Card aria-label="should not appear" data-testid="also not">
        Content
      </Card>
    )

    const cardEl = screen.getByText('Content').closest('div')
    expect(cardEl).not.toHaveAttribute('aria-label')
    expect(cardEl).not.toHaveAttribute('data-testid')
  })
})
