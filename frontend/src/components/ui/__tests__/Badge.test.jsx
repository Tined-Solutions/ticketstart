import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import Badge from '../Badge.jsx'

describe('Badge', () => {
  it('renders children text', () => {
    render(<Badge>Active</Badge>)
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('defaults to info variant with brand cian colors', () => {
    render(<Badge>Info</Badge>)
    const el = screen.getByText('Info')
    expect(el.className).toContain('bg-cian/15')
    expect(el.className).toContain('text-cian-dark')
  })

  it('renders success variant with brand verde colors', () => {
    render(<Badge variant="success">Done</Badge>)
    const el = screen.getByText('Done')
    expect(el.className).toContain('bg-verde/15')
    expect(el.className).toContain('text-verde-dark')
  })

  it('renders warning variant with brand amarillo colors', () => {
    render(<Badge variant="warning">Notice</Badge>)
    const el = screen.getByText('Notice')
    expect(el.className).toContain('bg-amarillo/15')
    expect(el.className).toContain('text-amarillo-dark')
  })

  it('renders error variant with rose colors', () => {
    render(<Badge variant="error">Failed</Badge>)
    const el = screen.getByText('Failed')
    expect(el.className).toContain('bg-rose-100')
    expect(el.className).toContain('text-rose-700')
  })

  it('merges custom className', () => {
    render(<Badge className="ml-2">Tag</Badge>)
    const el = screen.getByText('Tag')
    expect(el.className).toContain('ml-2')
  })

  it('is a span element', () => {
    render(<Badge>Tag</Badge>)
    const el = screen.getByText('Tag')
    expect(el.tagName).toBe('SPAN')
  })
})
