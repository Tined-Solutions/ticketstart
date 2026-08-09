import { describe, it, expect, vi } from 'vitest'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useDialog } from '../useDialog.js'

function Harness({ onClose, open = true, autoFocus = true }) {
  const ref = useDialog({ onClose, open, autoFocus })
  if (!open) return null
  return (
    <div ref={ref} role="dialog" aria-modal="true" aria-label="Test dialog">
      <button type="button">First</button>
      <button type="button">Second</button>
      <button type="button">Last</button>
    </div>
  )
}

describe('useDialog', () => {
  it('renders the dialog with a focusable container', () => {
    render(<Harness onClose={vi.fn()} />)
    const dialog = screen.getByRole('dialog')
    expect(dialog).toBeInTheDocument()
    expect(dialog).toHaveAttribute('aria-modal', 'true')
  })

  it('auto-focuses the first focusable element on open', async () => {
    render(<Harness onClose={vi.fn()} />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
    })
  })

  it('calls onClose when Escape is pressed', async () => {
    const onClose = vi.fn()
    render(<Harness onClose={onClose} />)
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('does not call onClose on Escape when closed', async () => {
    const onClose = vi.fn()
    render(<Harness onClose={onClose} open={false} />)
    await userEvent.keyboard('{Escape}')
    expect(onClose).not.toHaveBeenCalled()
  })

  it('traps Tab: Tab from the last element cycles back to the first', () => {
    render(<Harness onClose={vi.fn()} />)
    const first = screen.getByRole('button', { name: 'First' })
    const last = screen.getByRole('button', { name: 'Last' })
    last.focus()
    fireEvent.keyDown(last, { key: 'Tab' })
    expect(first).toHaveFocus()
  })

  it('traps Tab: Shift+Tab from the first element cycles to the last', () => {
    render(<Harness onClose={vi.fn()} />)
    const first = screen.getByRole('button', { name: 'First' })
    const last = screen.getByRole('button', { name: 'Last' })
    first.focus()
    fireEvent.keyDown(first, { key: 'Tab', shiftKey: true })
    expect(last).toHaveFocus()
  })

  it('locks body scroll and adds overscroll-contain while open, restoring on close', async () => {
    const { rerender } = render(<Harness onClose={vi.fn()} />)

    expect(document.body.style.overflow).toBe('hidden')
    expect(document.body.classList.contains('overscroll-contain')).toBe(true)

    rerender(<Harness onClose={vi.fn()} open={false} />)

    expect(document.body.style.overflow).toBe('')
    expect(document.body.classList.contains('overscroll-contain')).toBe(false)
  })

  it('restores focus to the previously focused element on close', async () => {
    const { rerender } = render(
      <div>
        <button type="button" data-testid="outside">Outside</button>
        <Harness onClose={vi.fn()} open={false} />
      </div>
    )

    const outside = screen.getByTestId('outside')
    outside.focus()

    // Open the dialog: previousFocus (outside) is captured and the first
    // focusable element inside the dialog takes focus.
    rerender(
      <div>
        <button type="button" data-testid="outside">Outside</button>
        <Harness onClose={vi.fn()} />
      </div>
    )

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
    })

    // Close the dialog: focus returns to the element focused before it opened.
    rerender(
      <div>
        <button type="button" data-testid="outside">Outside</button>
        <Harness onClose={vi.fn()} open={false} />
      </div>
    )

    await waitFor(() => {
      expect(outside).toHaveFocus()
    })
  })
})
