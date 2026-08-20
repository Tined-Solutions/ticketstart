import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Button from '../Button.jsx'

describe('Button', () => {
  it('renders children', () => {
    render(<Button>Click me</Button>)
    expect(screen.getByRole('button', { name: 'Click me' })).toBeInTheDocument()
  })

  it('calls onClick when clicked', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Click</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('does not call onClick when disabled', async () => {
    const onClick = vi.fn()
    render(<Button disabled onClick={onClick}>Click</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).not.toHaveBeenCalled()
  })

  it('does not call onClick when loading', async () => {
    const onClick = vi.fn()
    render(<Button loading onClick={onClick}>Click</Button>)
    const btn = screen.getByRole('button')
    expect(btn).toBeDisabled()
    await userEvent.click(btn)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('shows spinner when loading', () => {
    render(<Button loading>Loading</Button>)
    const btn = screen.getByRole('button')
    // The spinner SVG is hidden, but the button text should still be there
    expect(btn).toHaveTextContent('Loading')
    expect(btn).toBeDisabled()
  })

  it('applies primary variant classes by default', () => {
    render(<Button>Primary</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('bg-primary')
  })

  it('applies danger variant classes', () => {
    render(<Button variant="danger">Delete</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('bg-danger')
  })

  it('applies ghost variant classes', () => {
    render(<Button variant="ghost">Cancel</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('bg-transparent')
  })

  it('applies size classes', () => {
    render(<Button size="lg">Big</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('text-base')
    expect(btn.className).toContain('px-7')
    expect(btn.className).toContain('py-3')
  })

  it('applies pill (rounded-full) shape for all sizes', () => {
    render(<Button>Pill</Button>)
    const btn = screen.getByRole('button', { name: 'Pill' })
    expect(btn.className).toContain('rounded-full')
  })

  it('uses type="button" by default', () => {
    render(<Button>Click</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button')
  })

  it('accepts custom type', () => {
    render(<Button type="submit">Submit</Button>)
    expect(screen.getByRole('button')).toHaveAttribute('type', 'submit')
  })

  it('forwards ref', () => {
    const ref = { current: null }
    render(<Button ref={ref}>Ref</Button>)
    expect(ref.current).toBeInstanceOf(HTMLButtonElement)
  })

  it('is keyboard accessible — Enter triggers click', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Press</Button>)
    const btn = screen.getByRole('button')
    btn.focus()
    await userEvent.keyboard('{Enter}')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('is keyboard accessible — Space triggers click', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Press</Button>)
    const btn = screen.getByRole('button')
    btn.focus()
    await userEvent.keyboard(' ')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('has focus-visible ring styles', () => {
    render(<Button>Focus</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('focus-visible:ring-')
  })
})
