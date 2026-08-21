import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EventForm from './EventForm.jsx'

const mockPost = vi.fn()
const mockPut = vi.fn()
const mockOnSuccess = vi.fn()

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
    put: (...args) => mockPut(...args),
  },
}))

function buildEvent(overrides = {}) {
  return {
    id: 'event-1',
    name: 'Recital de Rock Nacional',
    date: '2026-08-15T21:00:00Z',
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

async function fillBasicFields(user, overrides = {}) {
  const data = {
    name: 'Nuevo Evento',
    date: '2026-12-25T20:00',
    location: 'Teatro Colon',
    description: 'Descripcion del evento',
    ...overrides,
  }

  await user.clear(screen.getByLabelText(/nombre del evento/i))
  await user.type(screen.getByLabelText(/nombre del evento/i), data.name)

  await user.clear(screen.getByLabelText(/fecha y hora/i))
  await user.type(screen.getByLabelText(/fecha y hora/i), data.date)

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
    date: '2026-12-25T20:00',
    location: 'Teatro Colon',
    description: 'Descripcion del evento',
    ...overrides,
  }

  fireEvent.change(screen.getByLabelText(/nombre del evento/i), {
    target: { value: data.name },
  })
  fireEvent.change(screen.getByLabelText(/fecha y hora/i), {
    target: { value: data.date },
  })
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
    expect(screen.getByLabelText(/fecha y hora/i)).toHaveValue('')
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

    fillBasicFieldsFire()
    fillTicketTypeFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledWith('/events', {
      name: 'Nuevo Evento',
      date: expect.stringContaining('2026-12-25'),
      location: 'Teatro Colon',
      description: 'Descripcion del evento',
      ticketTypes: [
        { name: 'General', price: 5000, quantity: 100 },
      ],
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

  it('uploads image after creating event when a file is selected', async () => {
    const createdEvent = { id: 'new-event-id' }
    mockPost
      .mockResolvedValueOnce({ data: createdEvent }) // create event
      .mockResolvedValueOnce({ data: { imageUrl: 'https://r2.example.com/img.jpg' } }) // upload image

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    // Simulate file selection
    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [file] } })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledTimes(2)
    // Second call should be the image upload
    const imageCall = mockPost.mock.calls[1]
    expect(imageCall[0]).toBe('/events/new-event-id/image')
    expect(imageCall[1]).toBeInstanceOf(FormData)
    expect(mockOnSuccess).toHaveBeenCalledWith('new-event-id')
  })

  it('shows warning when image upload fails after successful event creation', async () => {
    const createdEvent = { id: 'new-event-id' }
    mockPost
      .mockResolvedValueOnce({ data: createdEvent }) // create event
      .mockRejectedValueOnce(new Error('Upload failed')) // image upload fails

    render(<EventForm mode="create" onSuccess={mockOnSuccess} />)

    fillBasicFieldsFire()
    fillTicketTypeFire()

    const file = new File(['dummy'], 'event.jpg', { type: 'image/jpeg' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [file] } })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /crear evento/i }))
      await Promise.resolve()
    })

    expect(
      screen.getByText(/evento creado correctamente, pero la imagen no pudo cargarse/i)
    ).toBeInTheDocument()
    expect(mockOnSuccess).toHaveBeenCalledWith('new-event-id')
  })

  it('validates image file type', async () => {
    render(<EventForm mode="create" />)

    const file = new File(['dummy'], 'event.pdf', { type: 'application/pdf' })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [file] } })

    expect(
      screen.getByText(/formato de imagen no valido/i)
    ).toBeInTheDocument()
  })

  it('validates image file size', async () => {
    render(<EventForm mode="create" />)

    // Create a file larger than 5MB
    const largeFile = new File(['x'.repeat(6 * 1024 * 1024)], 'large.jpg', {
      type: 'image/jpeg',
    })
    const fileInput = screen.getByLabelText(/imagen del evento/i)
    fireEvent.change(fileInput, { target: { files: [largeFile] } })

    expect(
      screen.getByText(/la imagen no debe superar los 5 mb/i)
    ).toBeInTheDocument()
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
})
