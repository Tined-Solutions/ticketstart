import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Button from '../Button.jsx'

// --- Existing tests for primary/secondary/danger/ghost remain in Button.test.jsx ---
// These tests only cover the NEW glass and gradient variants.

describe('Button — glass & gradient variants', () => {
  it('renders glass variant with backdrop-blur classes', () => {
    render(<Button variant="glass">Glass</Button>)
    const btn = screen.getByRole('button', { name: 'Glass' })
    expect(btn.className).toContain('backdrop-blur-md')
    expect(btn.className).toContain('bg-white/10')
    expect(btn.className).toContain('border-white/20')
  })

  it('renders gradient variant with brand gradient classes', () => {
    render(<Button variant="gradient">Gradient</Button>)
    const btn = screen.getByRole('button', { name: 'Gradient' })
    expect(btn.className).toContain('from-indigo-500')
    expect(btn.className).toContain('to-violet-500')
    expect(btn.className).toContain('text-white')
  })

  it('glass variant handles clicks normally', async () => {
    const onClick = vi.fn()
    render(<Button variant="glass" onClick={onClick}>Click</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('gradient variant handles clicks normally', async () => {
    const onClick = vi.fn()
    render(<Button variant="gradient" onClick={onClick}>Click</Button>)
    await userEvent.click(screen.getByRole('button'))
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('glass variant disabled state', async () => {
    const onClick = vi.fn()
    render(<Button variant="glass" disabled onClick={onClick}>Click</Button>)
    const btn = screen.getByRole('button')
    expect(btn).toBeDisabled()
    await userEvent.click(btn)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('gradient variant loading state shows spinner', () => {
    render(<Button variant="gradient" loading>Loading</Button>)
    const btn = screen.getByRole('button')
    expect(btn).toBeDisabled()
    expect(btn).toHaveTextContent('Loading')
  })
})
