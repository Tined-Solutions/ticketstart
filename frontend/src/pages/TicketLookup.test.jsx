import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TicketLookup from './TicketLookup.jsx'

const mockGet = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children, className }) => (
    <a href={to} className={className}>
      {children}
    </a>
  ),
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const mockTicket = {
  id: 'ticket-001',
  eventId: 'event-1',
  eventName: 'Recital de Rock Nacional',
  eventDate: '2026-08-15T21:00:00Z',
  eventLocation: 'Estadio Luna Park, Buenos Aires',
  ticketTypeName: 'Platea',
  price: 15000,
  qrCodeData: 'ticket-001:1750000000:abc123signature',
  qrCodeImage: 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==',
  isUsed: false,
  usedAt: null,
  createdAt: '2026-07-10T12:00:00Z',
}

const mockUsedTicket = {
  ...mockTicket,
  id: 'ticket-002',
  eventName: 'Feria de Emprendedores',
  eventDate: '2026-09-01T14:00:00Z',
  eventLocation: 'La Rural, Buenos Aires',
  ticketTypeName: 'General',
  price: 0,
  isUsed: true,
  usedAt: '2026-09-01T15:30:00Z',
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function fillForm(user, { email = 'juan@example.com', dni = '12345678' } = {}) {
  return {
    async submit() {
      if (email) {
        await user.clear(screen.getByLabelText(/email/i))
        await user.type(screen.getByLabelText(/email/i), email)
      }
      if (dni) {
        await user.clear(screen.getByLabelText(/^dni$/i))
        await user.type(screen.getByLabelText(/^dni$/i), dni)
      }
      await user.click(screen.getByRole('button', { name: /buscar entradas/i }))
    },
  }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('TicketLookup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
  })

  afterEach(() => {
    // Restore any mocked browser APIs
    vi.restoreAllMocks()
  })

  // -- Rendering --------------------------------------------------------

  it('renders the lookup form with email and DNI inputs', () => {
    render(<TicketLookup />)

    expect(
      screen.getByRole('heading', { name: /buscar mis entradas/i })
    ).toBeInTheDocument()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /buscar entradas/i })
    ).toBeInTheDocument()
  })

  // -- Validation -------------------------------------------------------

  it('shows validation errors for empty fields', async () => {
    render(<TicketLookup />)

    await userEvent.click(
      screen.getByRole('button', { name: /buscar entradas/i })
    )

    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/el dni es obligatorio/i)).toBeInTheDocument()
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('shows validation error for invalid email format', async () => {
    render(<TicketLookup />)
    const form = fillForm(userEvent.setup(), { email: 'not-an-email' })
    await form.submit()

    expect(
      screen.getByText(/el formato del email no es valido/i)
    ).toBeInTheDocument()
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('clears field validation error when the user starts typing', async () => {
    render(<TicketLookup />)

    await userEvent.click(
      screen.getByRole('button', { name: /buscar entradas/i })
    )
    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText(/email/i), 'a')
    expect(
      screen.queryByText(/el email es obligatorio/i)
    ).not.toBeInTheDocument()
  })

  // -- Successful lookup ------------------------------------------------

  it('calls the lookup API and displays tickets on success', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket, mockUsedTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/2 entradas encontradas/i)).toBeInTheDocument()
    })

    expect(mockGet).toHaveBeenCalledWith('/tickets/lookup', {
      params: { email: 'juan@example.com', dni: '12345678' },
    })

    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
    expect(screen.getByText(/la rural/i)).toBeInTheDocument()
  })

  it('displays a single ticket result with correct heading', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/1 entrada encontrada/i)).toBeInTheDocument()
    })
  })

  // -- QR code display --------------------------------------------------

  it('displays QR code images for each ticket', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByAltText(/codigo qr de recital de rock nacional/i)
      ).toBeInTheDocument()
    })

    const qrImage = screen.getByAltText(/codigo qr de recital de rock nacional/i)
    expect(qrImage).toHaveAttribute(
      'src',
      `data:image/png;base64,${mockTicket.qrCodeImage}`
    )
  })

  it('shows download and print buttons for each ticket', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByRole('button', { name: /descargar qr/i })
      ).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: /imprimir entrada/i })
      ).toBeInTheDocument()
    })
  })

  // -- Ticket used status -----------------------------------------------

  it('shows "Usada" badge and usage date for used tickets', async () => {
    mockGet.mockResolvedValue({ data: [mockUsedTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      // The badge div has class "ticket-usage-badge used" with text "Usada"
      const badges = screen.getAllByText(/^Usada$/)
      expect(badges.length).toBe(1)
      expect(badges[0]).toBeInTheDocument()
    })

    // The usage timestamp appears in the "ticket-used-at" paragraph
    const usedAtParagraph = document.querySelector('.ticket-used-at')
    expect(usedAtParagraph).toBeInTheDocument()
    expect(usedAtParagraph.textContent).toMatch(/septiembre/i)
  })

  it('shows "Valida" badge for unused tickets', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/valida/i)).toBeInTheDocument()
    })
  })

  // -- Ticket details ---------------------------------------------------

  it('displays ticket type and price', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    // Price formatted in es-AR locale
    expect(screen.getByText(/\$ 15\.000,00/)).toBeInTheDocument()
  })

  // -- Download functionality -------------------------------------------

  it('renders download and print buttons with accessible labels', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket, mockUsedTicket] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/2 entradas encontradas/i)).toBeInTheDocument()
    })

    // Each ticket has its own download + print button
    const downloadButtons = screen.getAllByRole('button', {
      name: /descargar qr/i,
    })
    const printButtons = screen.getAllByRole('button', {
      name: /imprimir entrada/i,
    })

    expect(downloadButtons).toHaveLength(2)
    expect(printButtons).toHaveLength(2)

    // Buttons are not disabled
    downloadButtons.forEach((btn) => expect(btn).not.toBeDisabled())
    printButtons.forEach((btn) => expect(btn).not.toBeDisabled())
  })

  // -- Empty state ------------------------------------------------------

  it('shows empty state when no tickets match', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByText(/no se encontraron entradas con esos datos/i)
      ).toBeInTheDocument()
    })

    expect(
      screen.getByText(/verifica que el email y dni sean correctos/i)
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: /ver eventos/i })).toHaveAttribute(
      'href',
      '/events'
    )
  })

  // -- Error state ------------------------------------------------------

  it('shows error message and retry button on API failure', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    expect(screen.getByRole('button', { name: /reintentar/i })).toBeInTheDocument()
  })

  it('resets to form state when clicking retry after error', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    // After "reintentar" the error message and retry button should disappear
    expect(screen.queryByText(/error de conexion/i)).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /reintentar/i })
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /buscar entradas/i })
    ).toBeInTheDocument()
  })

  it('shows fallback error message for unexpected error shapes', async () => {
    mockGet.mockRejectedValue(new Error('Network error'))

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByText(/ocurrio un error al buscar entradas/i)
      ).toBeInTheDocument()
    })
  })

  it('shows error message from backend plain string error', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: 'El DNI es obligatorio' } },
    })

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/el dni es obligatorio/i)).toBeInTheDocument()
    })
  })

  // -- Loading state ----------------------------------------------------

  it('displays loading state during API call', async () => {
    let resolveGet
    mockGet.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveGet = resolve
        })
    )

    render(<TicketLookup />)
    const form = fillForm(userEvent.setup())
    await form.submit()

    expect(
      screen.getByRole('button', { name: /buscando/i })
    ).toBeDisabled()

    resolveGet({ data: [mockTicket] })
  })
})
