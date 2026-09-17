import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventSelect from '../EventSelect.jsx'

const events = [
  { id: 'evt-1', name: 'Rock en el Parque', date: '2026-08-15T21:00:00Z', location: 'Estadio Monumental' },
  { id: 'evt-2', name: 'Jazz Night', date: '2026-09-20T20:00:00Z', location: 'Teatro Colon' },
]

function expectedTime(iso) {
  return new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false })
}

describe('EventSelect', () => {
  it('shows the placeholder when nothing is selected', () => {
    render(<EventSelect events={events} value="" onChange={vi.fn()} />)
    expect(screen.getByRole('button')).toHaveTextContent('Seleccionar evento...')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('shows the selected event label on the trigger', () => {
    render(<EventSelect events={events} value="evt-1" onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')
    expect(trigger).toHaveTextContent('Rock en el Parque')
    expect(trigger).toHaveTextContent('Estadio Monumental')
    expect(trigger).toHaveTextContent(expectedTime('2026-08-15T21:00:00Z'))
  })

  it('opens the listbox on click and closes it on a second click', async () => {
    const user = userEvent.setup()
    render(<EventSelect events={events} value="" onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')

    await user.click(trigger)
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    expect(screen.getAllByRole('option')).toHaveLength(2)

    await user.click(trigger)
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('selecting an option calls onChange with the option id', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<EventSelect events={events} value="" onChange={onChange} />)

    await user.click(screen.getByRole('button'))
    await user.click(screen.getByRole('option', { name: /jazz night/i }))

    expect(onChange).toHaveBeenCalledTimes(1)
    expect(onChange).toHaveBeenCalledWith('evt-2')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('closes with Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup()
    render(<EventSelect events={events} value="" onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')

    await user.click(trigger)
    expect(screen.getByRole('listbox')).toBeInTheDocument()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  it('closes on outside click', async () => {
    const user = userEvent.setup()
    render(
      <div>
        <EventSelect events={events} value="" onChange={vi.fn()} />
        <button type="button">Elsewhere</button>
      </div>
    )

    await user.click(screen.getByRole('button', { name: /seleccionar evento/i }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /elsewhere/i }))
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('navigates with arrow keys and selects with Enter', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<EventSelect events={events} value="" onChange={onChange} />)
    const trigger = screen.getByRole('button')
    trigger.focus()

    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('listbox')).toHaveAttribute(
      'aria-activedescendant',
      expect.stringContaining('option-0')
    )

    await user.keyboard('{ArrowDown}')
    await user.keyboard('{ArrowUp}')
    await user.keyboard('{Enter}')

    expect(onChange).toHaveBeenCalledTimes(1)
    expect(onChange).toHaveBeenCalledWith('evt-1')
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('jumps to the last option with End and back to the first with Home', async () => {
    const user = userEvent.setup()
    render(<EventSelect events={events} value="" onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')
    trigger.focus()

    await user.keyboard('{ArrowDown}')
    await user.keyboard('{End}')
    expect(screen.getByRole('listbox').getAttribute('aria-activedescendant')).toContain('option-1')

    await user.keyboard('{Home}')
    expect(screen.getByRole('listbox').getAttribute('aria-activedescendant')).toContain('option-0')
  })

  it('renders a disabled trigger that does not open', async () => {
    const user = userEvent.setup()
    render(<EventSelect events={events} value="" disabled onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')
    expect(trigger).toBeDisabled()

    await user.click(trigger)
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('wires listbox/option ARIA roles and bounds the panel height', async () => {
    const user = userEvent.setup()
    render(<EventSelect events={events} value="evt-2" onChange={vi.fn()} />)
    const trigger = screen.getByRole('button')
    expect(trigger).toHaveAttribute('aria-haspopup', 'listbox')
    expect(trigger).toHaveAttribute('aria-expanded', 'false')

    await user.click(trigger)
    expect(trigger).toHaveAttribute('aria-expanded', 'true')

    const listbox = screen.getByRole('listbox')
    expect(listbox).toHaveClass('max-h-72')
    expect(listbox).toHaveClass('overflow-y-auto')

    const selected = screen.getAllByRole('option').find((o) => o.getAttribute('aria-selected') === 'true')
    expect(selected).toHaveTextContent(/jazz night/i)
  })

  it('shows the loading state while loading', () => {
    render(<EventSelect events={[]} value="" loading onChange={vi.fn()} />)
    expect(screen.getAllByText('Cargando eventos...').length).toBeGreaterThan(0)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('shows the error message as an alert', () => {
    render(
      <EventSelect events={[]} value="" error="No pudimos cargar los eventos." onChange={vi.fn()} />
    )
    expect(screen.getByRole('alert')).toHaveTextContent(/no pudimos cargar los eventos/i)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})