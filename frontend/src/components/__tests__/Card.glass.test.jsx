import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Card from '../Card.jsx'

describe('Card — glass prop', () => {
  it('renders default (non-glass) card with bg-surface and card radius', () => {
    const { container } = render(<Card>Content</Card>)
    const cardEl = container.firstChild
    expect(cardEl.className).toContain('bg-surface')
    expect(cardEl.className).toContain('border')
    expect(cardEl.className).toContain('rounded-[var(--radius-card)]')
    expect(cardEl.className).not.toContain('glass-surface')
  })

  it('renders glass card with glass-surface class', () => {
    const { container } = render(<Card glass>Content</Card>)
    const cardEl = container.firstChild
    expect(cardEl.className).toContain('glass-surface')
    expect(cardEl.className).not.toContain('bg-surface')
  })

  it('renders header slot', () => {
    render(<Card header={<span>Header text</span>}>Body</Card>)
    expect(screen.getByText('Header text')).toBeInTheDocument()
    expect(screen.getByText('Body')).toBeInTheDocument()
  })

  it('renders footer slot', () => {
    render(<Card footer={<span>Footer text</span>}>Body</Card>)
    expect(screen.getByText('Footer text')).toBeInTheDocument()
  })

  it('renders header, body, and footer together', () => {
    render(
      <Card header="H" footer="F">
        Body content
      </Card>
    )
    expect(screen.getByText('H')).toBeInTheDocument()
    expect(screen.getByText('Body content')).toBeInTheDocument()
    expect(screen.getByText('F')).toBeInTheDocument()
  })

  it('does NOT spread unknown HTML props to the DOM', () => {
    render(
      <Card data-custom="nope" onClick={() => {}}>
        Content
      </Card>
    )
    const el = screen.getByText('Content').closest('div')
    expect(el).not.toHaveAttribute('data-custom')
  })
})
