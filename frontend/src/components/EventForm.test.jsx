import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, act, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventForm from './EventForm.jsx'

const mockPost = vi.fn()
const mockPut = vi.fn()
const mockOnSuccess = vi.fn()
const mockReadImageDimensions = vi.fn()

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
    put: (...args) => mockPut(...args),
  },
}))

// jsdom cannot decode images, so the dropzone's dimension check is mocked to
// treat every fixture as a conforming 16:9, 1920×1080 image.
vi.mock('../lib/readImageDimensions.js', () => ({
  readImageDimensions: (...args) => mockReadImageDimensions(...args),
}))

beforeEach(() => {
  mockReadImageDimensions.mockReset()
  mockReadImageDimensions.mockResolvedValue({ width: 1920, height: 1080 })
})

function buildEvent(overrides = {}) {
  return {
    id: 'event-1',
    name: 'Recital de Rock Nacional',
    // Dynamic future date: a hardcoded date eventually becomes past and trips
    // the future-date validation.
    date: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
    location: 'Estadio Luna Park, Buenos Aires',
    description: 'Un gran recital',
    imageUrl: 'https://example.com/rock.jpg',
    ticketTypes: [
      { id: 'tt-1', name: 'General', price: 5000, quantity: 200 },
      { id: 'tt-2', name: 'VIP', price: 15000, quantity: 50 },
    ],
    ...overrides,
  }
}

// ── DateTimePicker helpers ────────────────────────────────────────────────
// The event date is now a custom picker: open the trigger, navigate with the
// month/year dropdowns, click the day, set the time selects and confirm.

const pad = (n) => String(n).padStart(2, '0')

/** Local "YYYY-MM-DDTHH:mm" some days ahead — always future and inside the picker range. */
function futureDateTimeLocal({ daysAhead = 30, hours = '20', minutes = '00' } = {}) {
  const target = new Date()
  target.setDate(target.getDate() + daysAhead)
  return (
    `${target.getFullYear()}-${pad(target.getMonth() + 1)}-${pad(target.getDate())}` +
    `T${hours}:${minutes}`
  )
}

function fullDayLabel(year, monthIndex, day) {
  return new Intl.DateTimeFormat('es-AR', { dateStyle: 'full' }).format(
    new Date(year, monthIndex, day)
  )
}

function parseDateTimeLocal(value) {
  const [datePart, timePart] = value.split('T')
  const [year, month, day] = datePart.split('-').map(Number)
  const [hours, minutes] = timePart.split(':')
  return { year, month, day, hours, minutes }
}

async function fillDateTimePicker(user, value) {
  const { year, month, day, hours, minutes } = parseDateTimeLocal(value)

  await user.click(screen.getByLabelText(/fecha y hora/i))
  const dialog = screen.getByRole('dialog', { name: /seleccionar fecha y hora/i })
  await user.selectOptions(
    within(dialog).getByLabelText(/elegir el año/i),
    String(year)
  )
  await user.selectOptions(
    within(dialog).getByLabelText(/elegir el mes/i),
    String(month - 1)
  )
  await user.click(
    within(dialog).getByRole('button', { name: fullDayLabel(year, month - 1, day) })
  )
  await user.selectOptions(within(dialog).getByLabelText('Hora'), hours)
  await user.selectOptions(within(dialog).getByLabelText('Minutos'), minutes)
  await user.click(within(dialog).getByRole('button', { name: 'Listo' }))
}

function fillDateTimePickerFire(value) {
  const { year, month, day, hours, minutes } = parseDateTimeLocal(value)

  fireEvent.click(screen.getByLabelText(/fecha y hora/i))
  const dialog = screen.getByRole('dialog', { name: /seleccionar fecha y hora/i })
  fireEvent.change(within(dialog).getByLabelText(/elegir el año/i), {
    target: { value: String(year) },
  })
  fireEvent.change(within(dialog).getByLabelText(/elegir el mes/i), {
    target: { value: String(month - 1) },
  })
  // "Today" carries the localized "Hoy, …" accessible name.
  const now = new Date()
  const isToday =
    year === now.getFullYear() && month - 1 === now.getMonth() && day === now.getDate()
  const dayName = isToday
    ? `Hoy, ${fullDayLabel(year, month - 1, day)}`
    : fullDayLabel(year, month - 1, day)
  fireEvent.click(within(dialog).getByRole('button', { name: dayName }))
  fireEvent.change(within(dialog).getByLabelText('Hora'), {
    target: { value: hours },
  })
  fireEvent.change(within(dialog).getByLabelText('Minutos'), {
    target: { value: minutes },
  })
  fireEvent.click(within(dialog).getByRole('button', { name: 'Listo' }))
}

async function fillBasicFields(user, overrides = {}) {
  const data = {
    name: 'Nuevo Evento',
    date: futureDateTimeLocal(),
    location: 'Teatro Colon',
    description: 'Descripcion del evento',
    ...overrides,
  }

  await user.clear(screen.getByLabelText(/nombre del evento/i))
  await user.type(screen.getByLabelText(/nombre del evento/i), data.name)

  await fillDateTimePicker(user, data.date)

  await user.clear(screen.getByLabelText(/^ubicacion/i))
  await user.type(screen.getByLabelText(/^ubicacion/i), data.location)

  if (data.description) {
    await user.clear(screen.getByLabelText(/descripcion/i))
    await user.type(screen.getByLabelText(/descripcion/i), data.description)
  }
}

async function fillTicketType(user, { index = 0, name = 'General', price = '5000', quantity = '100' } = {}) {
  const rows = screen.getAllByText(/nombre/i)
    .filter((el) => el.closest('.ticket-type-row'))
    .map((el) => el.closest('.ticket-type-row'))

  const row = rows[index]
  if (!row) return

  const nameInput = row.querySelector('input[id^="tt-name-"]')
  const priceInput = row.querySelector('input[id^="tt-price-"]')
  const quantityInput = row.querySelector('input[id^="tt-quantity-"]')

  if (nameInput) {
    await user.clear(nameInput)
    await user.type(nameInput, name)
  }
  if (priceInput) {
    await user.clear(priceInput)
    await user.type(priceInput, price)
  }
  if (quantityInput) {
    await user.clear(quantityInput)
    await user.type(quantityInput, quantity)
  }
}

function fillBasicFieldsFire(overrides = {}) {
  const data = {
    name: 'Nuevo Evento',
    date: futureDateTimeLocal(),
    location: 'Teatro Colon',
    description: 'Descripcion del evento',
    ...overrides,
  }

  fireEvent.change(screen.getByLabelText(/nombre del evento/i), {
    target: { value: data.name },
  })
  fillDateTimePickerFire(data.date)
  fireEvent.change(screen.getByLabelText(/^ubicacion/i), {
    target: { value: data.location },
  })
  fireEvent.change(screen.getByLabelText(/descripcion/i), {
    target: { value: data.description },
  })
}

function fillTicketTypeFire({ index = 0, name = 'General', price = '5000', quantity = '100' } = {}) {
  const rows = screen.getAllByText(/nombre/i)
    .filter((el) => el.closest('.ticket-type-row'))
    .map((el) => el.closest('.ticket-type-row'))

  const row = rows[index]
  if (!row) return

  const nameInput = row.querySelector('input[id^="tt-name-"]')
  const priceInput = row.querySelector('input[id^="tt-price-"]')
  const quantityInput = row.querySelector('input[id^="tt-quantity-"]')

  if (nameInput) fireEvent.change(nameInput, { target: { value: name } })
  if (priceInput) fireEvent.change(priceInput, { target: { value: price } })
  if (quantityInput) fireEvent.change(quantityInput, { target: { value: quantity } })
}

describe('EventForm — create mode', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPut.mockReset()
    mockOnSuccess.mockReset()
  })

  it('renders the create form with empty fields', () => {
    render(<EventForm mode="create" />)

    expect(screen.getByLabelText(/nombre del evento/i)).toHaveValue('')
    expect(screen.getByLabelText(/fecha y hora/i)).toHaveTextContent(
      'Seleccioná fecha y hora'
    )
    expect(screen.getByLabelText(/^ubicacion/i)).toHaveValue('')
    expect(screen.getByLabelText(/descripcion/i)).toHaveValue('')
    expect(screen.getByLabelText(/imagen del evento/i)).toBeInTheDocument()
    expect(screen.getByText(/tipos de entrada/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /crear evento/i })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /\+ agregar tipo de entrada/i })
    ).toBeInTheDocument()
  })

  it('shows a single empty ticket type row by default', () => {
    render(<EventForm mode="create" />)

    const rows = screen.getAllByText(/nombre/i)
      .filter((el) => el.closest('.ticket-type-row'))
    expect(rows).toHaveLength(1)
  })

  it('adds a new ticket type row when clicking the add button', async () => {
    render(<EventForm mode="create" />)

    await userEvent.click(
      screen.getByRole('button', { name: /\+ agregar tipo de entrada/i })
    )

    const rows = screen.getAllByText(/nombre/i)
      .filter((el) => el.closest('.ticket-type-row'))
    expect(rows).toHaveLength(2)
  })

  it('removes a ticket type row when clicking remove', async () => {
    render(<EventForm mode="create" />)

    // Add second row first so there are 2 (remove only shows when >1)
    await userEvent.click(
      screen.getByRole('button', { name: /\+ agregar tipo de entrada/i })
    )

    let rows = screen.getAllByText(/nombre/i)
      .filter((el) => el.closest('.ticket-type-row'))
    expect(rows).toHaveLength(2)

    const removeButtons = screen.getAllByRole('button', { name: /eliminar tipo de entrada/i })
    expect(removeButtons).toHaveLength(2)

    await userEvent.click(removeButtons[0])

    rows = screen.getAllByText(/nombre/i)
      .filter((el) => el.closest('.ticket-type-row'))
    expect(rows).toHaveLength(1)
  })

  it('validates required fields on submit', async () => {
    render(<EventForm mode="create" />)

    await userEvent.click(
      screen.getByRole('button', { name: /crear evento/i })
    )

    expect(
      screen.getByText(/el nombre del evento es obligatorio/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/la fecha es obligatoria/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/la ubicacion es obligatoria/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/el nombre es obligatorio/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/el precio es obligatorio/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/la cantidad es obligatoria/i)
    ).toBeInTheDocument()
  })

  it('validates ticket type price > 0', async () => {
    render(<EventForm mode="create" />)

    await fillBasicFields(userEvent.setup())
    await fillTicketType(userEvent.setup(), { price: '-5', quantity: '10' })

    await userEvent.click(
      screen.getByRole('button', { name: /crear evento/i })
    )

    expect(
      screen.getByText(/el precio debe ser mayor a 0/i)
    ).toBeInTheDocument()
  })

  it('validates ticket type quantity is a positive integer', async () => {
    render(<EventForm mode="create" />)

    await fillBasicFields(userEvent.setup())
    await fillTicketType(userEvent.setup(), { quantity: '3.5' })

    await userEvent.click(
      screen.getByRole('button', { name: /crear evento/i })
    )

    expect(
      screen.getByText(/la cantidad debe ser un numero entero mayor a 0/i)
    ).toBeInTheDocument()
  })

  it('creates an event and calls onSuccess', async () => {
    const createdEvent = { id: 'new-event-id' }
    mockPost.mockResolvedValueOnce({ data: createdEvent })

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    const eventDate = futureDateTimeLocal()
    fillBasicFieldsFire({ date: eventDate })
    fillTicketTypeFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledWith('/events', {
      name: 'Nuevo Evento',
      date: expect.stringContaining(eventDate.slice(0, 10)),
      location: 'Teatro Colon',
      description: 'Descripcion del evento',
      ticketTypes: [
        { name: 'General', price: 5000, quantity: 100 },
      ],
      // No photo → imageUrl is the non-nullable empty string (EIM-004)
      imageUrl: '',
    })
    expect(mockOnSuccess).toHaveBeenCalledWith('new-event-id')
  })

  it('shows success feedback after creating an event', async () => {
    mockPost.mockResolvedValueOnce({ data: { id: 'new-event-id' } })

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/evento creado correctamente/i)).toBeInTheDocument()
  })

  it('shows pending-approval copy after successful creation (EA-009)', async () => {
    mockPost.mockResolvedValueOnce({ data: { id: 'new-event-id' } })

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/pendiente de aprobacion/i)).toBeInTheDocument()
  })

  it('shows error feedback when creation fails', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'Datos invalidos' } } },
    })

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/datos invalidos/i)).toBeInTheDocument()
    expect(mockOnSuccess).not.toHaveBeenCalled()
  })

  it('uploads the image first, then creates the event carrying the imageUrl', async () => {
    const createdEvent = { id: 'new-event-id' }
    mockPost
      .mockResolvedValueOnce({ data: { imageUrl: 'https://r2.example.com/img.jpg' } }) // upload
      .mockResolvedValueOnce({ data: createdEvent }) // create

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    // Simulate file selection (async: react-dropzone processes files in a
    // microtask, so flush it before submitting)
    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [file] } })
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    // EIM-004: upload-FIRST — the upload precedes the save and its URL flows into it
    expect(mockPost).toHaveBeenCalledTimes(2)
    const uploadCall = mockPost.mock.calls[0]
    expect(uploadCall[0]).toBe('/uploads/event-image')
    expect(uploadCall[1]).toBeInstanceOf(FormData)
    // The explicit multipart header is REQUIRED — without it axios's
    // transformRequest JSON-serializes the FormData (client default is
    // application/json) and the backend rejects the upload with 415.
    expect(uploadCall[2]?.headers?.['Content-Type']).toBe('multipart/form-data')
    const createCall = mockPost.mock.calls[1]
    expect(createCall[0]).toBe('/events')
    expect(createCall[1].imageUrl).toBe('https://r2.example.com/img.jpg')
    expect(mockOnSuccess).toHaveBeenCalledWith('new-event-id')
  })

  it('blocks the event save with a red error when the image upload fails', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'La imagen no pudo cargarse' } } },
    })

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [file] } })
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    // EIM-003/004: honest red alert, NO green false-success, save never called
    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent(/la imagen no pudo cargarse/i)
    expect(
      screen.queryByText(/evento creado correctamente/i)
    ).not.toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledTimes(1) // only the upload call
    expect(mockPost.mock.calls[0][0]).toBe('/uploads/event-image')
    expect(mockOnSuccess).not.toHaveBeenCalled()
    // phase reset — the submit button is re-enabled
    expect(
      screen.getByRole('button', { name: /crear evento/i })
    ).not.toBeDisabled()
  })

  it('shows "Subiendo imagen…" and disables the submit while uploading', async () => {
    // Upload never resolves so we can observe the uploading phase
    mockPost.mockImplementation(() => new Promise(() => {}))

    render(<EventForm mode="create" />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    await act(async () => {
      fireEvent.change(screen.getByLabelText(/imagen del evento/i), {
        target: { files: [file] },
      })
    })

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    await waitFor(() => {
      expect(
        screen.getByRole('button', { name: /subiendo imagen/i })
      ).toBeDisabled()
    })
  })

  it('shows "Guardando…" after the upload completes', async () => {
    mockPost
      .mockResolvedValueOnce({ data: { imageUrl: 'https://r2.example.com/img.jpg' } }) // upload resolves
      .mockImplementation(() => new Promise(() => {})) // save hangs

    render(<EventForm mode="create" />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    await act(async () => {
      fireEvent.change(screen.getByLabelText(/imagen del evento/i), {
        target: { files: [file] },
      })
    })

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    await waitFor(() => {
      expect(
        screen.getByRole('button', { name: /guardando/i })
      ).toBeDisabled()
    })
  })

  it('validates image file type inline under the dropzone, not in the banner', async () => {
    render(<EventForm mode="create" />)

    const file = new File(['dummy'], 'event.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [file] } })

    // The rejection is INLINE (role=alert inside the image form-group), never
    // the global banner on top of the form.
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/formato de imagen no valido/i)
    expect(alert.closest('.form-group')).not.toBeNull()
    expect(document.querySelector('.feedback-message')).not.toBeInTheDocument()
  })

  it('validates image file size inline under the dropzone, not in the banner', async () => {
    render(<EventForm mode="create" />)

    // Create a file larger than 5MB
    const largeFile = new File(['x'.repeat(6 * 1024 * 1024)], 'large.jpg', {
      type: 'image/jpeg',
    })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [largeFile] } })

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/la imagen no debe superar los 5 mb/i)
    expect(alert.closest('.form-group')).not.toBeNull()
    expect(document.querySelector('.feedback-message')).not.toBeInTheDocument()
  })

  it('clears the inline image error when a new valid image is selected', async () => {
    render(<EventForm mode="create" />)

    const fileInput = screen.getByLabelText(/imagen del evento/i)

    // Reject a bad file first → inline error appears
    fireEvent.change(fileInput, {
      target: { files: [new File(['dummy'], 'event.pdf', { type: 'application/pdf' })] },
    })
    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/formato de imagen no valido/i)

    // A fresh valid selection clears the previous rejection
    await act(async () => {
      fireEvent.change(fileInput, {
        target: { files: [new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })] },
      })
    })

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('scrolls to and focuses the first invalid field when validation fails', () => {
    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    // prefersReducedMotion() reads window.matchMedia, absent in jsdom
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation((query) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }))
    )
    try {
      render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

      // Empty submit: the first error is the event name
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

      expect(scrollIntoView).toHaveBeenCalled()
      expect(document.activeElement?.id).toBe('eventName')
    } finally {
      delete Element.prototype.scrollIntoView
      vi.unstubAllGlobals()
    }
  })

  it('rejects past datetimes in Spanish without calling the API', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    // Past days are no longer selectable in the picker, so exercise the
    // boundary that is still reachable: today at 00:00 is always past.
    const now = new Date()
    const todayAtMidnight =
      `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T00:00`
    fillBasicFieldsFire({ date: todayAtMidnight })
    fillTicketTypeFire()

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    expect(
      await screen.findByText(/la fecha del evento debe ser futura/i)
    ).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
    expect(mockOnSuccess).not.toHaveBeenCalled()
  })

  it('clears the date error as soon as a date is picked after a failed submit', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    // Fill everything except the date, then submit → "La fecha es obligatoria".
    fireEvent.change(screen.getByLabelText(/nombre del evento/i), {
      target: { value: 'Nuevo Evento' },
    })
    fireEvent.change(screen.getByLabelText(/^ubicacion/i), {
      target: { value: 'Teatro Colon' },
    })
    fireEvent.change(screen.getByLabelText(/descripcion/i), {
      target: { value: 'Descripcion del evento' },
    })
    fillTicketTypeFire()

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    expect(await screen.findByText(/la fecha es obligatoria/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/fecha y hora/i)).toHaveAttribute(
      'aria-invalid',
      'true'
    )

    // Picking a date resolves the field: message and red state disappear.
    fillDateTimePickerFire(futureDateTimeLocal())

    expect(screen.queryByText(/la fecha es obligatoria/i)).not.toBeInTheDocument()
    expect(screen.getByLabelText(/fecha y hora/i)).not.toHaveAttribute('aria-invalid')
  })

  it('clears name and location errors as soon as their fields are filled', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    expect(
      await screen.findByText(/el nombre del evento es obligatorio/i)
    ).toBeInTheDocument()
    expect(screen.getByText(/la ubicacion es obligatoria/i)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/nombre del evento/i), {
      target: { value: 'Nuevo Evento' },
    })

    expect(
      screen.queryByText(/el nombre del evento es obligatorio/i)
    ).not.toBeInTheDocument()
    expect(screen.getByLabelText(/nombre del evento/i)).not.toHaveAttribute(
      'aria-invalid'
    )
    expect(screen.getByText(/la ubicacion es obligatoria/i)).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/^ubicacion/i), {
      target: { value: 'Teatro Colon' },
    })

    expect(screen.queryByText(/la ubicacion es obligatoria/i)).not.toBeInTheDocument()
    expect(screen.getByLabelText(/^ubicacion/i)).not.toHaveAttribute('aria-invalid')
  })

  it('clears ticket-type row errors as soon as their fields are filled', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    expect(await screen.findByText(/el nombre es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/el precio es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/la cantidad es obligatoria/i)).toBeInTheDocument()

    fireEvent.change(document.querySelector('input[id^="tt-name-"]'), {
      target: { value: 'General' },
    })

    expect(screen.queryByText(/el nombre es obligatorio/i)).not.toBeInTheDocument()
    expect(screen.getByText(/el precio es obligatorio/i)).toBeInTheDocument()

    fireEvent.change(document.querySelector('input[id^="tt-price-"]'), {
      target: { value: '5000' },
    })
    fireEvent.change(document.querySelector('input[id^="tt-quantity-"]'), {
      target: { value: '100' },
    })

    expect(screen.queryByText(/el precio es obligatorio/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/la cantidad es obligatoria/i)).not.toBeInTheDocument()
  })

  it('keeps the name error until the new value is actually valid', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
    expect(
      await screen.findByText(/el nombre del evento es obligatorio/i)
    ).toBeInTheDocument()

    const nameInput = screen.getByLabelText(/nombre del evento/i)

    // Empty / whitespace-only values are still invalid: the message survives
    // the change and the field stays marked as invalid.
    fireEvent.change(nameInput, { target: { value: '' } })
    expect(screen.getByText(/el nombre del evento es obligatorio/i)).toBeInTheDocument()

    fireEvent.change(nameInput, { target: { value: '   ' } })
    expect(screen.getByText(/el nombre del evento es obligatorio/i)).toBeInTheDocument()
    expect(nameInput).toHaveAttribute('aria-invalid', 'true')

    // A valid name resolves the field immediately.
    fireEvent.change(nameInput, { target: { value: 'Nuevo Evento' } })

    expect(
      screen.queryByText(/el nombre del evento es obligatorio/i)
    ).not.toBeInTheDocument()
    expect(nameInput).not.toHaveAttribute('aria-invalid')
  })

  it('keeps ticket-type row errors while the new value is still invalid', async () => {
    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
    expect(await screen.findByText(/el precio es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/la cantidad es obligatoria/i)).toBeInTheDocument()

    const priceInput = document.querySelector('input[id^="tt-price-"]')
    const quantityInput = document.querySelector('input[id^="tt-quantity-"]')

    // 0 and empty are still invalid: the row errors survive the change (the
    // message is not re-evaluated until the next submit).
    fireEvent.change(priceInput, { target: { value: '' } })
    expect(screen.getByText(/el precio es obligatorio/i)).toBeInTheDocument()

    fireEvent.change(priceInput, { target: { value: '0' } })
    expect(screen.getByText(/el precio es obligatorio/i)).toBeInTheDocument()
    expect(priceInput).toHaveAttribute('aria-invalid', 'true')

    fireEvent.change(quantityInput, { target: { value: '0' } })
    expect(screen.getByText(/la cantidad es obligatoria/i)).toBeInTheDocument()
    expect(quantityInput).toHaveAttribute('aria-invalid', 'true')

    // Valid values resolve each row error.
    fireEvent.change(priceInput, { target: { value: '5000' } })
    expect(screen.queryByText(/el precio es obligatorio/i)).not.toBeInTheDocument()
    expect(priceInput).not.toHaveAttribute('aria-invalid')

    fireEvent.change(quantityInput, { target: { value: '100' } })
    expect(screen.queryByText(/la cantidad es obligatoria/i)).not.toBeInTheDocument()
    expect(quantityInput).not.toHaveAttribute('aria-invalid')
  })

  it('translates the backend past-date error to Spanish and scrolls to the date field', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: 'Event date must be in the future' } },
    })

    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation((query) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }))
    )
    try {
      render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

      fillBasicFieldsFire()
      fillTicketTypeFire()

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      })

      expect(
        await screen.findByText(/la fecha del evento debe ser futura/i)
      ).toBeInTheDocument()
      // The field is disabled while submitting, so only the scroll applies;
      // focus is covered by the validation-path test above.
      expect(scrollIntoView).toHaveBeenCalled()
    } finally {
      delete Element.prototype.scrollIntoView
      vi.unstubAllGlobals()
    }
  })

  it('scrolls the global banner into view when a backend error appears', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'Datos invalidos' } } },
    })

    const scrollIntoView = vi.fn()
    Element.prototype.scrollIntoView = scrollIntoView
    // prefersReducedMotion() reads window.matchMedia, absent in jsdom
    vi.stubGlobal(
      'matchMedia',
      vi.fn().mockImplementation((query) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }))
    )
    try {
      render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

      fillBasicFieldsFire()
      fillTicketTypeFire()

      await act(async () => {
        fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
        await Promise.resolve()
      })

      // Backend errors still land in the global banner (image rejects are
      // inline now) and the form scrolls the banner into view.
      const alert = screen.getByRole('alert')
      expect(alert).toHaveTextContent(/datos invalidos/i)
      expect(alert.className).toContain('feedback-message')
      expect(scrollIntoView).toHaveBeenCalled()
      expect(mockOnSuccess).not.toHaveBeenCalled()
    } finally {
      delete Element.prototype.scrollIntoView
      vi.unstubAllGlobals()
    }
  })

  it('disables form inputs while submitting', async () => {
    // Make the POST never resolve so we can observe the disabled state
    mockPost.mockImplementation(() => new Promise(() => {}))

    render(<EventForm mode="create" />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))

    await waitFor(() => {
      expect(screen.getByLabelText(/nombre del evento/i)).toBeDisabled()
      expect(
        screen.getByRole('button', { name: /guardando/i })
      ).toBeInTheDocument()
    })
  })
})

describe('EventForm — edit mode', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPut.mockReset()
    mockOnSuccess.mockReset()
  })

  it('renders the edit form pre-filled with event data', () => {
    const event = buildEvent()

    render(<EventForm mode="edit" initialData={event} />)

    expect(screen.getByLabelText(/nombre del evento/i)).toHaveValue(
      'Recital de Rock Nacional'
    )
    expect(screen.getByLabelText(/^ubicacion/i)).toHaveValue(
      'Estadio Luna Park, Buenos Aires'
    )
    expect(screen.getByLabelText(/descripcion/i)).toHaveValue('Un gran recital')

    // Should show existing image preview
    const preview = screen.getByAltText(/vista previa/i)
    expect(preview).toBeInTheDocument()
    expect(preview.src).toBe('https://example.com/rock.jpg')

    // Crop previews appear once there is an image, so the organizer sees how
    // the photo will be cut in each production context before saving.
    expect(
      screen.getByText(/así se va a ver la imagen en cada lugar/i)
    ).toBeInTheDocument()

    // ATS-008 / D-2: edit mode hides the ticket-type fieldset (no silent no-op).
    // The admin is pointed to the supported stock path instead.
    expect(
      screen.queryByRole('button', { name: /agregar tipo de entrada/i })
    ).not.toBeInTheDocument()
    expect(
      screen.getByText(
        /el stock de entradas se gestiona desde el panel de administracion/i
      )
    ).toBeInTheDocument()

    expect(
      screen.getByRole('button', { name: /guardar cambios/i })
    ).toBeInTheDocument()
  })

  it('updates an event via PUT and calls onSuccess', async () => {
    const event = buildEvent()
    mockPut.mockResolvedValueOnce({ data: {} })

    render(
      <EventForm mode="edit" initialData={event} onSuccess={mockOnSuccess} />
    )

    // Change the name
    const nameInput = screen.getByLabelText(/nombre del evento/i)
    await userEvent.clear(nameInput)
    await userEvent.type(nameInput, 'Evento Modificado')

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    expect(mockPut).toHaveBeenCalledWith('/events/event-1', {
      name: 'Evento Modificado',
      date: expect.any(String),
      location: 'Estadio Luna Park, Buenos Aires',
      description: 'Un gran recital',
      // Regression: a plain text edit must send the current image so the
      // backend never wipes it (contract: null preserves, "" clears).
      imageUrl: 'https://example.com/rock.jpg',
    })
    // Verify the date is a valid ISO string
    const dateArg = mockPut.mock.calls[0][1].date
    expect(new Date(dateArg).toISOString()).toBe(dateArg)
    expect(mockOnSuccess).toHaveBeenCalledWith('event-1')
  })

  it('shows success feedback after updating', async () => {
    const event = buildEvent()
    mockPut.mockResolvedValueOnce({ data: {} })

    render(<EventForm mode="edit" initialData={event} />)

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    expect(
      screen.getByText(/evento actualizado correctamente/i)
    ).toBeInTheDocument()

    // EA-009: the pending-approval copy is create-mode only — edit stays unchanged
    expect(screen.queryByText(/pendiente de aprobacion/i)).not.toBeInTheDocument()
  })

  it('shows error feedback when update fails', async () => {
    const event = buildEvent()
    mockPut.mockRejectedValueOnce({
      response: { data: { error: { message: 'No tiene permisos' } } },
    })

    render(
      <EventForm mode="edit" initialData={event} onSuccess={mockOnSuccess} />
    )

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/no tiene permisos/i)).toBeInTheDocument()
    expect(mockOnSuccess).not.toHaveBeenCalled()
  })
})

describe('EventForm — readOnly mode', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPut.mockReset()
    mockOnSuccess.mockReset()
  })

  it('disables every editable input and hides submit + image upload controls', () => {
    const event = buildEvent()

    render(<EventForm mode="edit" readOnly initialData={event} />)

    // All editable inputs are disabled (D-6 / PEM-002).
    expect(screen.getByLabelText(/nombre del evento/i)).toBeDisabled()
    expect(screen.getByLabelText(/fecha y hora/i)).toBeDisabled()
    expect(screen.getByLabelText(/^ubicacion/i)).toBeDisabled()
    expect(screen.getByLabelText(/descripcion/i)).toBeDisabled()

    // Submit button and image upload input are not rendered.
    expect(
      screen.queryByRole('button', { name: /guardar cambios/i })
    ).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/imagen del evento/i)).not.toBeInTheDocument()

    // Existing data is pre-filled for consultation, and the image preview stays.
    expect(screen.getByLabelText(/nombre del evento/i)).toHaveValue(
      'Recital de Rock Nacional'
    )
    expect(screen.getByAltText(/vista previa/i)).toBeInTheDocument()

    // Crop previews are visible for the reviewer too — same information the
    // organizer sees when authoring, so moderation can judge the real crops.
    expect(
      screen.getByText('Así se va a ver la imagen en cada lugar:')
    ).toBeInTheDocument()
    expect(screen.getByText('Banner — página del evento')).toBeInTheDocument()
    expect(screen.getByText('Card — listado de eventos')).toBeInTheDocument()
    expect(screen.getByText('Miniatura — resumen de compra')).toBeInTheDocument()
  })

  it('does not call the API when readOnly (no submit path exists)', () => {
    const event = buildEvent()

    render(<EventForm mode="edit" readOnly initialData={event} />)

    expect(mockPut).not.toHaveBeenCalled()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('disables the ticket-type fieldset in readOnly create mode', () => {
    render(<EventForm mode="create" readOnly />)

    const fieldset = screen.getByRole('group', { name: /tipos de entrada/i })
    expect(fieldset).toBeDisabled()
    expect(
      screen.queryByRole('button', { name: /crear evento/i })
    ).not.toBeInTheDocument()
  })

  it('renders ticket types read-only with name, price and quantity (pre-approval review)', () => {
    const event = buildEvent()

    render(<EventForm mode="edit" readOnly initialData={event} />)

    // The admin reviews EVERYTHING the organizer entered before approving —
    // prices are public catalog content, so the preview must show them.
    expect(
      screen.getByText(/general — \$ 5\.000 — 200 entradas/i)
    ).toBeInTheDocument()
    expect(
      screen.getByText(/vip — \$ 15\.000 — 50 entradas/i)
    ).toBeInTheDocument()

    // The stock-management note is edit-mode guidance; the read-only preview
    // replaces it with the actual data.
    expect(
      screen.queryByText(/el stock de entradas se gestiona desde el panel/i)
    ).not.toBeInTheDocument()
  })
})
