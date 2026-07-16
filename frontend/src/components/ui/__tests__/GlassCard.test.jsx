import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import GlassCard from '../GlassCard.jsx'

describe('GlassCard', () => {
  it('renders children inside a div by default', () => {
    render(<GlassCard>Hello Glass</GlassCard>)
    expect(screen.getByText('Hello Glass')).toBeInTheDocument()
  })

  it('applies .glass-surface class to the wrapper', () => {
    render(<GlassCard>Content</GlassCard>)
    const el = screen.getByText('Content').closest('div')
    expect(el).toHaveClass('glass-surface')
  })

  it('merges className prop', () => {
    render(<GlassCard className="extra-class">Content</GlassCard>)
    const el = screen.getByText('Content').closest('div')
    expect(el).toHaveClass('glass-surface')
    expect(el).toHaveClass('extra-class')
  })

  it('renders as a different HTML element via the `as` prop', () => {
    render(
      <GlassCard as="section" data-testid="card">
        Section content
      </GlassCard>
    )
    const el = screen.getByTestId('card')
    expect(el.tagName).toBe('SECTION')
    expect(el).toHaveClass('glass-surface')
  })

  it('renders multiple children', () => {
    render(
      <GlassCard>
        <span>One</span>
        <span>Two</span>
      </GlassCard>
    )
    expect(screen.getByText('One')).toBeInTheDocument()
    expect(screen.getByText('Two')).toBeInTheDocument()
  })
})
