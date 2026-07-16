import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import ErrorBoundary from '../ErrorBoundary.jsx'

// A component that throws on render
function BrokenComponent({ shouldThrow = true }) {
  if (shouldThrow) {
    throw new Error('Test explosion!')
  }
  return <p>All good</p>
}

describe('ErrorBoundary', () => {
  // Suppress expected error output in console during tests
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    console.error.mockRestore()
  })

  it('renders children when no error occurs', () => {
    render(
      <ErrorBoundary>
        <p>Everything works</p>
      </ErrorBoundary>
    )

    expect(screen.getByText('Everything works')).toBeInTheDocument()
  })

  it('renders fallback UI when a child throws', () => {
    render(
      <ErrorBoundary>
        <BrokenComponent />
      </ErrorBoundary>
    )

    // Should show the error fallback, not the broken component
    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument()
    expect(screen.queryByText('All good')).not.toBeInTheDocument()
  })

  it('calls componentDidCatch and displays error boundary content', () => {
    render(
      <ErrorBoundary fallback={<p>Custom fallback UI</p>}>
        <BrokenComponent />
      </ErrorBoundary>
    )

    expect(screen.getByText('Custom fallback UI')).toBeInTheDocument()
  })
})
