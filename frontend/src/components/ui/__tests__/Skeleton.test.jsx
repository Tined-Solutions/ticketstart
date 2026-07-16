import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Skeleton from '../Skeleton.jsx'

describe('Skeleton', () => {
  it('renders with role="status" and accessible label', () => {
    render(<Skeleton />)
    const el = screen.getByRole('status', { name: /loading/i })
    expect(el).toBeInTheDocument()
  })

  it('renders a hidden sr-only "Loading…" span', () => {
    render(<Skeleton />)
    const el = screen.getByText('Loading…')
    expect(el).toHaveClass('sr-only')
  })

  it('applies default text variant (h-4 rounded)', () => {
    render(<Skeleton />)
    const el = screen.getByRole('status')
    expect(el.className).toContain('h-4')
    expect(el.className).toContain('rounded')
  })

  it('applies circular variant (rounded-full)', () => {
    render(<Skeleton variant="circular" />)
    const el = screen.getByRole('status')
    expect(el.className).toContain('rounded-full')
  })

  it('applies rectangular variant (rounded-md)', () => {
    render(<Skeleton variant="rectangular" height="200px" />)
    const el = screen.getByRole('status')
    expect(el.className).toContain('rounded-md')
  })

  it('applies custom width and height via style', () => {
    render(<Skeleton width="300px" height="24px" />)
    const el = screen.getByRole('status')
    expect(el.style.width).toBe('300px')
    expect(el.style.height).toBe('24px')
  })

  it('applies animate-pulse only when motion is safe', () => {
    render(<Skeleton data-testid="skel" />)
    const el = screen.getByRole('status')
    // motion-safe:animate-pulse is the Tailwind v4 way to gate animation
    // Vitest/jsdom doesn't emulate prefers-reduced-motion, so the class
    // should be present in className (even if not "active" without media match)
    expect(el.className).toContain('motion-safe:animate-pulse')
  })

  it('merges custom className', () => {
    render(<Skeleton className="my-4" />)
    const el = screen.getByRole('status')
    expect(el.className).toContain('my-4')
  })
})
