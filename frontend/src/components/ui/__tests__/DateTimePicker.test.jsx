import { describe, it, expect, vi } from 'vitest'
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import DateTimePicker from '../DateTimePicker.jsx'

const fullDayLabel = (date) =>
  new Intl.DateTimeFormat('es-AR', { dateStyle: 'full' }).format(date)

const pad = (part) => String(part).padStart(2, '0')

const toValue = (date, hours, minutes) =>
  `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
  `T${pad(hours)}:${pad(minutes)}`

/** Future target inside the dropdown range (currentYear-6 … currentYear+6). */
function futureTarget(day = 15) {
  const now = new Date()
  return new Date(now.getFullYear() + 1, 5, day)
}

function setup(props = {}) {
  const onChange = vi.fn()
  render(<DateTimePicker id="eventDate" onChange={onChange} {...props} />)
  return { onChange, trigger: screen.getByRole('button') }
}

async function openPicker(user, trigger) {
  await user.click(trigger)
  return screen.getByRole('dialog', { name: /seleccionar fecha y hora/i })
}

async function pickDay(user, dialog, date) {
  await user.selectOptions(
    within(dialog).getByLabelText(/elegir el año/i),
    String(date.getFullYear())
  )
  await user.selectOptions(
    within(dialog).getByLabelText(/elegir el mes/i),
    String(date.getMonth())
  )
  await user.click(within(dialog).getByRole('button', { name: fullDayLabel(date) }))
}

describe('DateTimePicker', () => {
  it('shows the placeholder when the value is empty', () => {
    const { trigger } = setup()

    expect(trigger).toHaveTextContent('Seleccioná fecha y hora')
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
  })

  it('shows the value formatted in es-AR', () => {
    const { trigger } = setup({ value: '2026-12-25T20:00' })

    expect(trigger).toHaveTextContent(/25 de diciembre de 2026/)
    expect(trigger).toHaveTextContent(/20:00/)
  })

  it('exposes dialog aria wiring on the trigger', async () => {
    const user = userEvent.setup()
    const { trigger } = setup({
      'aria-invalid': 'true',
      'aria-describedby': 'eventDate-error',
    })

    expect(trigger).toHaveAttribute('aria-haspopup', 'dialog')
    expect(trigger).toHaveAttribute('aria-invalid', 'true')
    expect(trigger).toHaveAttribute('aria-describedby', 'eventDate-error')

    await openPicker(user, trigger)

    expect(trigger).toHaveAttribute('aria-expanded', 'true')
  })

  it('picks a day and a time and emits the local "YYYY-MM-DDTHH:mm" value', async () => {
    const user = userEvent.setup()
    const { onChange, trigger } = setup()
    const target = futureTarget()

    const dialog = await openPicker(user, trigger)
    await pickDay(user, dialog, target)

    await user.selectOptions(within(dialog).getByLabelText('Hora'), '14')
    await user.selectOptions(within(dialog).getByLabelText('Minutos'), '30')
    await user.click(within(dialog).getByRole('button', { name: 'Listo' }))

    expect(onChange).toHaveBeenCalledTimes(1)
    expect(onChange).toHaveBeenCalledWith(toValue(target, 14, 30))
    // Confirm closes the popover
    expect(
      screen.queryByRole('dialog', { name: /seleccionar fecha y hora/i })
    ).not.toBeInTheDocument()
  })

  it('defaults hours and minutes to 00/00', async () => {
    const user = userEvent.setup()
    const { trigger } = setup()

    const dialog = await openPicker(user, trigger)

    expect(within(dialog).getByLabelText('Hora')).toHaveValue('00')
    expect(within(dialog).getByLabelText('Minutos')).toHaveValue('00')
  })

  it('does not emit when closing without changes', async () => {
    const user = userEvent.setup()
    const { onChange, trigger } = setup({ value: '2026-12-25T20:00' })

    await openPicker(user, trigger)
    await user.keyboard('{Escape}')

    expect(onChange).not.toHaveBeenCalled()
  })

  it('closes on an outside pointerdown', async () => {
    const user = userEvent.setup()
    const { onChange, trigger } = setup()

    await openPicker(user, trigger)
    await user.click(document.body)

    expect(
      screen.queryByRole('dialog', { name: /seleccionar fecha y hora/i })
    ).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('closes on Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup()
    const { trigger } = setup()

    await openPicker(user, trigger)
    await user.keyboard('{Escape}')

    expect(
      screen.queryByRole('dialog', { name: /seleccionar fecha y hora/i })
    ).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })

  it('is disabled and does not open when disabled', async () => {
    const user = userEvent.setup()
    const { trigger } = setup({ disabled: true })

    expect(trigger).toBeDisabled()
    await user.click(trigger)

    expect(
      screen.queryByRole('dialog', { name: /seleccionar fecha y hora/i })
    ).not.toBeInTheDocument()
  })

  it('is disabled and does not open when readOnly', async () => {
    const user = userEvent.setup()
    const { onChange, trigger } = setup({ readOnly: true, value: '2026-12-25T20:00' })

    expect(trigger).toBeDisabled()
    await user.click(trigger)

    expect(
      screen.queryByRole('dialog', { name: /seleccionar fecha y hora/i })
    ).not.toBeInTheDocument()
    expect(onChange).not.toHaveBeenCalled()
  })

  it('keeps the existing time when only the day changes', async () => {
    const user = userEvent.setup()
    const { onChange, trigger } = setup({ value: '2026-12-25T20:00' })
    const target = futureTarget()

    const dialog = await openPicker(user, trigger)
    await pickDay(user, dialog, target)
    await user.click(within(dialog).getByRole('button', { name: 'Listo' }))

    expect(onChange).toHaveBeenCalledWith(toValue(target, 20, 0))
  })

  it('disables days before today and keeps today selectable', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 5, 15, 12, 0, 0))
    try {
      const { trigger } = setup()

      fireEvent.click(trigger)
      const dialog = screen.getByRole('dialog', {
        name: /seleccionar fecha y hora/i,
      })

      expect(
        within(dialog).getByRole('button', {
          name: fullDayLabel(new Date(2026, 5, 14)),
        })
      ).toBeDisabled()
      expect(
        within(dialog).getByRole('button', {
          name: `Hoy, ${fullDayLabel(new Date(2026, 5, 15))}`,
        })
      ).not.toBeDisabled()
    } finally {
      vi.useRealTimers()
    }
  })

  it('offers minutes in 5-minute steps', async () => {
    const user = userEvent.setup()
    const { trigger } = setup()

    const dialog = await openPicker(user, trigger)
    const minutes = within(dialog).getByLabelText('Minutos')

    expect(within(minutes).getAllByRole('option')).toHaveLength(12)
    expect(within(minutes).getByRole('option', { name: '05' })).toBeInTheDocument()
    expect(within(minutes).queryByRole('option', { name: '01' })).not.toBeInTheDocument()
  })

  it('keeps an off-grid minute from an existing value selectable', async () => {
    const user = userEvent.setup()
    const { trigger } = setup({ value: '2026-12-25T20:07' })

    const dialog = await openPicker(user, trigger)
    const minutes = within(dialog).getByLabelText('Minutos')

    expect(minutes).toHaveValue('07')
    expect(within(minutes).getByRole('option', { name: '07' })).toBeInTheDocument()
  })
})
