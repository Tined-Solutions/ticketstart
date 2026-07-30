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
  id: 'ticket-001',
  eventId: 'event-1',
  eventName: 'Recital de Rock Nacional',
  eventDate: '2026-08-15T21:00:00Z',
  eventLocation: 'Estadio Luna Park, Buenos Aires',
  ticketTypeName: 'Platea',
  price: 15000,
  quantity: 2,
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
  quantity: 1,
  isUsed: true,
  usedAt: '2026-09-01T15:30:00Z',
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getLookupEmailInput() {
  return screen.getAllByLabelText(/email/i)[0]
}

function getResendEmailInput() {
  return screen.getAllByLabelText(/email/i)[1]
}

function fillLookupForm(user, { email = 'juan@example.com' } = {}) {
  return {
    async submit() {
      if (email) {
        await user.clear(getLookupEmailInput())
        await user.type(getLookupEmailInput(), email)
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

  it('renders the lookup form with email input', () => {
    render(<TicketLookup />)

    expect(
      screen.getByRole('heading', { name: /buscar mis entradas/i })
    ).toBeInTheDocument()
    expect(getLookupEmailInput()).toBeInTheDocument()
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

  // -- Successful lookup ------------------------------------------------

  it('calls the lookup API with email only and displays tickets on success', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket, mockUsedTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/2 entradas encontradas/i)).toBeInTheDocument()
    })

    expect(mockGet).toHaveBeenCalledWith('/tickets/lookup', {
      params: { email: 'juan@example.com' },
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

  // -- Ticket used status -----------------------------------------------

  it('shows "Usada" badge and usage date for used tickets', async () => {
    mockGet.mockResolvedValue({ data: [mockUsedTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      const badges = screen.getAllByText(/^Usada$/)
      expect(badges.length).toBe(1)
      expect(badges[0]).toBeInTheDocument()
    })

    const usedAtParagraph = document.querySelector('.ticket-used-at')
    expect(usedAtParagraph).toBeInTheDocument()
    expect(usedAtParagraph.textContent).toMatch(/septiembre/i)
  })

  it('shows "Valida" badge for unused tickets', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/valida/i)).toBeInTheDocument()
    })
  })

  // -- Ticket details ---------------------------------------------------

  it('displays ticket type and price', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/platea/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/\$ 15\.000,00/)).toBeInTheDocument()
  })

  it('displays ticket quantity when present', async () => {
    mockGet.mockResolvedValue({ data: [mockTicket] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/cantidad: 2/i)).toBeInTheDocument()
  })

  // -- Empty state ------------------------------------------------------

  it('shows empty state when no tickets match', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<TicketLookup />)
    const form = fillLookupForm(userEvent.setup())
    await form.submit()

    await waitFor(() => {
      expect(
        screen.getByText(/no se encontraron entradas con ese email/i)
      ).toBeInTheDocument()
    })

    expect(
      screen.getByText(/verifica que el email sea correcto/i)
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
