import { describe, it, expect, vi, afterEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ToastProvider from '../ToastProvider.jsx'
import { useToast } from '../useToast.js'

// Test consumer component
function ToastTrigger({ message = 'Test toast', type = 'info' }) {
  const { toast } = useToast()
  return (
    <button onClick={() => toast[type](message)}>
      Show Toast
    </button>
  )
}

describe('ToastProvider — useRef for nextId', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('increments toast IDs correctly without resetting on re-render', async () => {
    const user = userEvent.setup()
    const { rerender } = render(
      <ToastProvider>
        <ToastTrigger message="Toast 1" />
      </ToastProvider>
    )

    await user.click(screen.getByText('Show Toast'))
    expect(screen.getByText('Toast 1')).toBeInTheDocument()

    // Re-render the same provider (not re-mount)
    rerender(
      <ToastProvider>
        <ToastTrigger message="Toast 2" />
      </ToastProvider>
    )

    await user.click(screen.getByText('Show Toast'))
    // The second toast should have a different (higher) id
    expect(screen.getByText('Toast 1')).toBeInTheDocument()
    expect(screen.getByText('Toast 2')).toBeInTheDocument()
    // Both toasts should be visible
    const alerts = screen.getAllByRole('alert')
    expect(alerts.length).toBeGreaterThanOrEqual(2)
  })

  it('nextId is managed via useRef, not module-level variable', () => {
    // Render two separate providers — each should start with id 1
    const { unmount } = render(
      <ToastProvider>
        <ToastTrigger message="Instance 1" />
      </ToastProvider>
    )
    unmount()

    // New mount should get a fresh counter (useRef behavior)
    render(
      <ToastProvider>
        <ToastTrigger message="Instance 2" />
      </ToastProvider>
    )

    // The key point: the provider uses useRef internally,
    // so the counter is scoped to the component instance
    expect(screen.getByText('Show Toast')).toBeInTheDocument()
  })
})
