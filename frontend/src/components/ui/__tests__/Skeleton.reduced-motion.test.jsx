import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render } from '@testing-library/react'
import Skeleton from '../Skeleton.jsx'

describe('Reduced Motion — prefers-reduced-motion', () => {
  let matchMediaMock

  beforeEach(() => {
    matchMediaMock = vi.fn().mockImplementation((query) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }))
    vi.stubGlobal('matchMedia', matchMediaMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('Skeleton renders with pulse animation when no reduced-motion preference', () => {
    const { container } = render(<Skeleton />)
    const skeleton = container.querySelector('[role="status"]')

    // Without prefers-reduced-motion, the animate-pulse class should be present
    // (motion-safe:animate-pulse is applied when prefers-reduced-motion: no-preference)
    expect(skeleton.className).toContain('motion-safe:animate-pulse')
  })

  it('Skeleton has sr-only label for accessibility', () => {
    const { container } = render(<Skeleton />)
    const label = container.querySelector('.sr-only')

    expect(label).toBeTruthy()
    expect(label.textContent).toBe('Loading\u2026')
  })

  it('Skeleton accepts custom width and height', () => {
    const { container } = render(<Skeleton width="50%" height="40px" />)
    const skeleton = container.querySelector('[role="status"]')

    expect(skeleton.style.width).toBe('50%')
    expect(skeleton.style.height).toBe('40px')
  })
})
