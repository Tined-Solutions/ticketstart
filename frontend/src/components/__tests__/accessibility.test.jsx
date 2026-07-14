/**
 * Accessibility tests for Task 28.3
 *
 * Tests keyboard navigation, screen reader compatibility, and color contrast
 * on key reusable components.
 */
import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Button from '../Button.jsx'
import Modal from '../Modal.jsx'
import FormField from '../FormField.jsx'
import Spinner from '../Spinner.jsx'
import Card from '../Card.jsx'

// ── Keyboard navigation ─────────────────────────────────────────────────

describe('Keyboard navigation', () => {
  it('Button is focusable and activatable with Enter', async () => {
    render(<Button>Test</Button>)
    const btn = screen.getByRole('button')
    btn.focus()
    expect(btn).toHaveFocus()
    await userEvent.keyboard('{Enter}')
    // Just verifying it doesn't throw — the button is focusable and interactive
  })

  it('Button is focusable and activatable with Space', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Test</Button>)
    const btn = screen.getByRole('button')
    btn.focus()
    await userEvent.keyboard(' ')
    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('disabled Button is not focusable with Tab', async () => {
    render(<Button disabled>Test</Button>)
    const btn = screen.getByRole('button')
    expect(btn).toBeDisabled()
    // Disabled buttons cannot receive focus with keyboard interaction
    btn.focus()
    expect(btn).not.toHaveFocus()
  })

  it('Modal close button is keyboard accessible', async () => {
    render(
      <Modal open onClose={vi.fn()} title="Accessible">
        Content
      </Modal>
    )

    const closeBtn = screen.getByLabelText('Cerrar')
    closeBtn.focus()
    expect(closeBtn).toHaveFocus()
  })

  it('FormField label is linked to input via htmlFor', () => {
    render(<FormField id="email" label="Email" type="email" />)
    const input = screen.getByLabelText('Email')
    expect(input).toHaveAttribute('id', 'email')
  })

  it('Card is rendered as a generic container (no implicit role)', () => {
    render(<Card>Content</Card>)
    // Card has no ARIA role by default — it's a presentational container
    const card = screen.getByText('Content').closest('div')
    expect(card).not.toHaveAttribute('role')
  })
})

// ── Screen reader compatibility ──────────────────────────────────────────

describe('Screen reader compatibility', () => {
  it('Button with loading state announces disabled state', () => {
    render(<Button loading>Save</Button>)
    const btn = screen.getByRole('button', { name: 'Save' })
    expect(btn).toBeDisabled()
  })

  it('Modal uses aria-modal and aria-labelledby', () => {
    render(
      <Modal open onClose={vi.fn()} title="Confirmation">
        Are you sure?
      </Modal>
    )

    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAttribute('aria-labelledby', 'modal-title')

    const title = screen.getByText('Confirmation')
    expect(title).toHaveAttribute('id', 'modal-title')
  })

  it('FormField renders error with role="alert"', () => {
    render(
      <FormField
        id="name"
        label="Name"
        error="Name is required"
      />
    )

    const error = screen.getByRole('alert')
    expect(error).toHaveTextContent('Name is required')
  })

  it('FormField error is linked to input via aria-describedby', () => {
    render(
      <FormField
        id="name"
        label="Name"
        error="Name is required"
      />
    )

    const input = screen.getByLabelText('Name')
    expect(input).toHaveAttribute('aria-describedby', 'name-error')
    expect(input).toHaveAttribute('aria-invalid', 'true')
  })

  it('Spinner announces its label via aria-label', () => {
    render(<Spinner label="Loading events" />)
    const status = screen.getByRole('status', { name: 'Loading events' })
    expect(status).toBeInTheDocument()
  })
})

// ── Color contrast (structural verification) ─────────────────────────────

describe('Color contrast structure', () => {
  it('Button primary variant uses light text on dark background', () => {
    render(<Button variant="primary">Primary</Button>)
    const btn = screen.getByRole('button')
    // Verifying the CSS classes that ensure contrast
    expect(btn.className).toContain('text-primary-content')
    expect(btn.className).toContain('bg-primary')
  })

  it('Button danger variant uses white text on red background', () => {
    render(<Button variant="danger">Delete</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('text-white')
    expect(btn.className).toContain('bg-danger')
  })

  it('Button ghost variant uses dark text on transparent background', () => {
    render(<Button variant="ghost">Cancel</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('text-neutral-700')
  })

  it('FormField error text uses danger color', () => {
    render(<FormField id="test" label="Test" error="Required" />)
    const error = screen.getByRole('alert')
    expect(error.className).toContain('text-danger')
  })

  it('FormField labels have medium weight for readability', () => {
    render(<FormField id="test" label="Test" />)
    const label = screen.getByText('Test')
    expect(label.className).toContain('font-medium')
  })
})

// ── Focus visibility ─────────────────────────────────────────────────────

describe('Focus visibility', () => {
  it('Button has focus-visible ring', () => {
    render(<Button>Focus me</Button>)
    const btn = screen.getByRole('button')
    expect(btn.className).toContain('focus-visible:ring-')
    expect(btn.className).toContain('focus-visible:outline-none')
  })

  it('FormField input has focus ring', () => {
    render(<FormField id="email" label="Email" />)
    const input = screen.getByLabelText('Email')
    expect(input.className).toContain('focus:ring-')
  })

  it('Modal close button has focus ring', () => {
    render(
      <Modal open onClose={vi.fn()} title="Test">
        Content
      </Modal>
    )
    const closeBtn = screen.getByLabelText('Cerrar')
    expect(closeBtn.className).toContain('focus-visible:ring-')
  })
})
