import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TicketLookup from './TicketLookup.jsx'

const mockGet = vi.fn()
const mockPost = vi.fn()

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
    post: (...args) => mockPost(...args),
  },
}))

vi.mock('@marsidev/react-turnstile', () => ({
  Turnstile: ({ onSuccess, onError, onExpire }) => (
    <div data-testid="turnstile-widget">
      <button
        data-testid="turnstile-success-trigger"
        onClick={() => onSuccess('mock-turnstile-token')}
      >
        Trigger Success
      </button>
      <button
        data-testid="turnstile-error-trigger"
        onClick={() => onError()}
      >
        Trigger Error
      </button>
      <button
        data-testid="turnstile-expire-trigger"
        onClick={() => onExpire()}
      >
        Trigger Expire
      </button>
    </div>
  ),
}))

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const mockTicket = {
  eventName: 'Recital de Rock Nacional',
  eventDate: '2026-08-15T21:00:00Z',
  eventLocation: 'Estadio Luna Park, Buenos Aires',
  ticketType: 'Platea',
  quantity: 2,
  purchaserEmail: 'j***@example.com',
}

const mockVipTicket = {
  ...mockTicket,
  ticketType: 'VIP',
  quantity: 1,
}

const mockOtherEvent = {
  eventName: 'Feria de Emprendedores',
  eventDate: '2026-09-01T14:00:00Z',
  eventLocation: 'La Rural, Buenos Aires',
  ticketType: 'General',
  quantity: 1,
  purchaserEmail: 'j***@example.com',
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getLookupEmailInput() {
  return screen.getAllByLabelText(/email/i)[0]
}

function getLookupDniInput() {
  return screen.getByLabelText(/^dni$/i)
}

function getResendEmailInput() {
  return screen.getAllByLabelText(/email/i)[1]
}

function fillLookupForm(
  user,
  { email = 'juan@example.com', dni = '12345678' } = {}
) {
  return {
    async submit() {
      if (email) {
        await user.clear(getLookupEmailInput())
        await user.type(getLookupEmailInput(), email)
      }
      if (dni) {
        await user.clear(getLookupDniInput())
        await user.type(getLookupDniInput(), dni)
      }
      const lookupButton = screen.getByRole('button', { name: /buscar entradas/i })
      await user.click(lookupButton)
    },
  }
}

async function fillResendForm(user, { email = 'juan@example.com' } = {}) {
  if (email) {
    await user.clear(getResendEmailInput())
    await user.type(getResendEmailInput(), email)
  }
}

async function activateTurnstile() {
  await userEvent.click(screen.getByTestId('turnstile-success-trigger'))
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('TicketLookup', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockPost.mockReset()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  // -- Rendering --------------------------------------------------------

  it('renders the lookup form with email and DNI inputs', () => {
    render(<TicketLookup />)

    expect(
      screen.getByRole('heading', { name: /buscar mis entradas/i })
    ).toBeInTheDocument()
    expect(getLookupEmailInput()).toBeInTheDocument()
    expect(getLookupDniInput()).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /buscar entradas/i })
    ).toBeInTheDocument()
  })

  // -- Validation (lookup) ----------------------------------------------

  it('shows validation error for empty email', async () => {
    render(<TicketLookup />)

    await userEvent.click(
      screen.getByRole('button', { name: /buscar entradas/i })
    )

    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('shows validation error for invalid email format', async () => {
    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup(), { email: 'not-an-email' })
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

    await userEvent.type(getLookupEmailInput(), 'a')
    expect(
      screen.queryByText(/el email es obligatorio/i)
    ).not.toBeInTheDocument()
  })

  it('shows validation error when DNI is empty', async () => {
    render(<TicketLookup />)

    await userEvent.click(
      screen.getByRole('button', { name: /buscar entradas/i })
    )

    expect(
      screen.getByText(/el documento es obligatorio/i)
    ).toBeInTheDocument()
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('shows validation error for invalid DNI format and does not call the API', async () => {
    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup(), { dni: '123' })
    await form.submit()

    expect(screen.getAllByText(/formato de dni inv[aá]lido/i).length).toBeGreaterThan(0)
    expect(mockGet).not.toHaveBeenCalled()
  })

  it('clears the DNI validation error when the user starts typing', async () => {
    render(<TicketLookup />)

    await userEvent.click(
      screen.getByRole('button', { name: /buscar entradas/i })
    )
    expect(
      screen.getByText(/el documento es obligatorio/i)
    ).toBeInTheDocument()

    await userEvent.type(getLookupDniInput(), '12345678')
    expect(
      screen.queryByText(/el documento es obligatorio/i)
    ).not.toBeInTheDocument()
  })

  it('does not call the lookup API when DNI is invalid', async () => {
    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup(), { dni: '12345' })
    await form.submit()

    expect(mockGet).not.toHaveBeenCalled()
  })

  it('validates a UY cédula and sends the clean DNI to the API', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const user = userEvent.setup()

    await user.selectOptions(
      screen.getByLabelText('País del documento'),
      'UY'
    )
    await user.type(getLookupEmailInput(), 'juan@example.com')
    await user.type(getLookupDniInput(), '5.123.456-1')
    await user.click(screen.getByRole('button', { name: /buscar entradas/i }))

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(mockGet).toHaveBeenCalledWith('/tickets/lookup', {
      params: { email: 'juan@example.com', dni: '51234561' },
    })
  })

  // -- Successful lookup ------------------------------------------------

  it('calls the lookup API with email and DNI and displays tickets on success', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket, mockOtherEvent] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/3 entradas encontradas/i)).toBeInTheDocument()
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
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/2 entradas encontradas/i)).toBeInTheDocument()
    })
  })

  it('displays "1 entrada encontrada" for a single ticket', async () => {
    mockGet.mockResolvedValue({ data: [{ ...mockTicket, quantity: 1 }] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/1 entrada encontrada/i)).toBeInTheDocument()
    })
  })

  // -- No QR / no print / no download -----------------------------------

  it('does NOT display QR code images', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(
      screen.queryByAltText(/codigo qr/i)
    ).not.toBeInTheDocument()
  })

  it('does NOT show download or print buttons', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(
      screen.queryByRole('button', { name: /descargar qr/i })
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /imprimir entrada/i })
    ).not.toBeInTheDocument()
  })

  // -- Ticket details ---------------------------------------------------

  it('displays ticket type and quantity, not price', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText('2')).toBeInTheDocument()
      expect(screen.getByText('Platea')).toBeInTheDocument()
    })

    // Info-only contract: no price is shown
    expect(screen.queryByText(/\$ 15\.000/)).not.toBeInTheDocument()
  })

  it('displays ticket quantity next to the ticket type', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // The quantity and the type name live on the same line ("2 Platea")
    const typeLine = screen.getByText('Platea').closest('li')
    expect(typeLine).toBeInTheDocument()
    expect(typeLine.textContent).toMatch(/^2\s*Platea$/)
  })

  it('groups tickets of the same event into a single card with per-type quantities', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket, mockVipTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/3 entradas encontradas/i)).toBeInTheDocument()
    })

    // Exactly ONE card for the event (headings: page h1 + card h3 for the event)
    const eventHeadings = screen.getAllByRole('heading', { name: /recital/i })
    expect(eventHeadings.length).toBe(1)

    // Both ticket-type lines are visible: "2 Platea" and "1 VIP"
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByText('Platea')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument()
    expect(screen.getByText('VIP')).toBeInTheDocument()
  })

  // -- Empty state ------------------------------------------------------

  it('shows empty state when no tickets match', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByText(/no se encontraron entradas con ese email y dni/i)
      ).toBeInTheDocument()
    })

    expect(
      screen.getByText(/verifica que los datos sean correctos/i)
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: /ver eventos/i })).toHaveAttribute(
      'href',
      '/events'
    )
  })

  // -- Error state (lookup) --------------------------------------------

  it('shows error message and retry button on API failure', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
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
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    expect(screen.queryByText(/error de conexion/i)).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /reintentar/i })
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /buscar entradas/i })
    ).toBeInTheDocument()
  })

  it('shows the error message from a plain Error object', async () => {
    mockGet.mockRejectedValue(new Error('Network error'))

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/network error/i)).toBeInTheDocument()
    })
  })

  it('shows error message from backend plain string error', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: 'El email no esta registrado' } },
    })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByText(/el email no esta registrado/i)
      ).toBeInTheDocument()
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
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    expect(
      screen.getByRole('button', { name: /buscando/i })
    ).toBeDisabled()

    resolveGet({ data: [mockTicket] })
  })

  // ── Resend section ──────────────────────────────────────────────────

  it('renders the resend form with email input, Turnstile widget, and submit button', () => {
    render(<TicketLookup />)

    expect(
      screen.getByRole('heading', { name: /reenviar entradas/i })
    ).toBeInTheDocument()

    expect(screen.getByTestId('turnstile-widget')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /reenviar entradas/i })
    ).toBeInTheDocument()
  })

  it('disables the resend submit button when Turnstile token is not set', () => {
    render(<TicketLookup />)

    expect(
      screen.getByRole('button', { name: /reenviar entradas/i })
    ).toBeDisabled()
  })

  it('enables the resend submit button when Turnstile succeeds', async () => {
    render(<TicketLookup />)

    await activateTurnstile()

    expect(
      screen.getByRole('button', { name: /reenviar entradas/i })
    ).not.toBeDisabled()
  })

  it('calls POST /tickets/resend with turnstileToken and shows success message', async () => {
    mockPost.mockResolvedValue({ data: {} })

    render(<TicketLookup />)

    await activateTurnstile()
    await fillResendForm(userEvent.setup())
    await userEvent.click(
      screen.getByRole('button', { name: /reenviar entradas/i })
    )

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/tickets/resend', {
        email: 'juan@example.com',
        turnstileToken: 'mock-turnstile-token',
      })
    })

    await waitFor(() => {
      expect(
        screen.getByText(
          /si el email esta registrado, recibiras las entradas en tu casilla/i
        )
      ).toBeInTheDocument()
    })
  })

  it('shows rate-limit message on 429 response', async () => {
    mockPost.mockRejectedValue({
      response: { status: 429 },
    })

    render(<TicketLookup />)

    await activateTurnstile()
    await fillResendForm(userEvent.setup())
    await userEvent.click(
      screen.getByRole('button', { name: /reenviar entradas/i })
    )

    await waitFor(() => {
      expect(
        screen.getByText(/demasiados intentos. intenta de nuevo en una hora/i)
      ).toBeInTheDocument()
    })
  })

  it('shows validation error for empty resend email', async () => {
    render(<TicketLookup />)

    await activateTurnstile()

    await userEvent.click(
      screen.getByRole('button', { name: /reenviar entradas/i })
    )

    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows Verified text after Turnstile success', async () => {
    render(<TicketLookup />)

    await activateTurnstile()

    expect(screen.getByText(/✓ Verified/)).toBeInTheDocument()
  })
})
