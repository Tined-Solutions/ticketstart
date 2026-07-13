import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, waitFor, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Checkout from './Checkout.jsx'

const mockNavigate = vi.fn()
const mockPost = vi.fn()
const mockLocationState = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
  useLocation: () => ({ state: mockLocationState() }),
}))

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => ({ user: null }),
}))

const cart = {
  eventId: 'event-1',
  eventName: 'Recital de Rock Nacional',
  eventDate: '2026-08-15T21:00:00Z',
  eventLocation: 'Estadio Luna Park, Buenos Aires',
  eventImageUrl: 'https://example.com/rock.jpg',
  selection: {
    ticketTypeId: 'tt-1',
    name: 'Platea',
    price: 15000,
    quantity: 2,
  },
  totalTickets: 2,
  totalPrice: 30000,
}

function buildReservation(overrides = {}) {
  return {
    id: 'reservation-1',
    token: 'reservation-token-1',
    quantity: 2,
    expiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
    ...overrides,
  }
}

async function fillPurchaserForm(
  user,
  { name = 'Juan Perez', email = 'juan@example.com', dni = '12345678' } = {}
) {
  await user.clear(screen.getByLabelText(/nombre completo/i))
  if (name) {
    await user.type(screen.getByLabelText(/nombre completo/i), name)
  }

  await user.clear(screen.getByLabelText(/email/i))
  if (email) {
    await user.type(screen.getByLabelText(/email/i), email)
  }

  await user.clear(screen.getByLabelText(/^dni$/i))
  if (dni) {
    await user.type(screen.getByLabelText(/^dni$/i), dni)
  }
}

function fillPurchaserFormFire(
  { name = 'Juan Perez', email = 'juan@example.com', dni = '12345678' } = {}
) {
  fireEvent.change(screen.getByLabelText(/nombre completo/i), {
    target: { value: name },
  })
  fireEvent.change(screen.getByLabelText(/email/i), {
    target: { value: email },
  })
  fireEvent.change(screen.getByLabelText(/^dni$/i), {
    target: { value: dni },
  })
}

describe('Checkout', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockNavigate.mockReset()
    mockLocationState.mockReset()
    mockLocationState.mockReturnValue(cart)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('redirects to the event catalog when there is no cart state', async () => {
    mockLocationState.mockReturnValue(undefined)

    render(<Checkout />)

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/events', { replace: true })
    })
  })

  it('renders the reservation form with event and selection summary', () => {
    render(<Checkout />)

    expect(screen.getByRole('heading', { name: /reserva tus entradas/i })).toBeInTheDocument()
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
    expect(screen.getByText(/platea/i)).toBeInTheDocument()
    expect(screen.getByText(/x 2/i)).toBeInTheDocument()
    expect(screen.getByText(/total:/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/nombre completo/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /reservar entradas/i })
    ).toBeInTheDocument()
  })

  it('shows a validation error when DNI is missing', async () => {
    render(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), { dni: ' ' })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/el dni es obligatorio/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('creates a reservation and displays the confirmation with countdown timer', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    render(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledWith('/reservations', {
      eventId: cart.eventId,
      ticketTypeId: cart.selection.ticketTypeId,
      quantity: cart.selection.quantity,
      purchaserDNI: '12345678',
    })
    expect(
      screen.getByRole('heading', { name: /confirma tu reserva/i })
    ).toBeInTheDocument()
    expect(screen.getByRole('timer')).toHaveTextContent('10:00')
    expect(screen.getByText(/platea/i)).toBeInTheDocument()
    expect(screen.getByText(/x 2/i)).toBeInTheDocument()
    expect(screen.getByText(/total:/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /pagar con mercado pago/i })
    ).toBeInTheDocument()
  })

  it('updates the countdown timer as time advances', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    render(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('timer')).toHaveTextContent('10:00')

    act(() => {
      vi.advanceTimersByTime(65000)
    })

    expect(screen.getByRole('timer')).toHaveTextContent('08:55')
  })

  it('shows the expiration view when the reservation expires', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    render(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('timer')).toBeInTheDocument()

    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    expect(screen.getByRole('heading', { name: /reserva expirada/i })).toBeInTheDocument()
    expect(screen.getByText(/tu reserva ya no es valida/i)).toBeInTheDocument()
    expect(screen.getByText(/las entradas fueron liberadas/i)).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
  })

  it('navigates back to the catalog from the expired view', async () => {
    const reservation = buildReservation({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    mockPost.mockResolvedValueOnce({ data: reservation })

    render(<Checkout />)

    await fillPurchaserForm(userEvent.setup())
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /volver al catalogo/i })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /volver al catalogo/i }))

    expect(mockNavigate).toHaveBeenCalledWith('/events', { replace: true })
  })

  it('displays the API error message when reservation creation fails', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'No hay stock disponible' } } },
    })

    render(<Checkout />)

    await fillPurchaserForm(userEvent.setup())
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(await screen.findByText(/no hay stock disponible/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /reservar entradas/i })
    ).not.toBeDisabled()
  })

  it('redirects to the Mercado Pago checkout URL when paying', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    const checkoutUrl = 'https://www.mercadopago.com.ar/checkout/v1/redirect?pref_id=abc123'

    mockPost
      .mockResolvedValueOnce({ data: reservation })
      .mockResolvedValueOnce({ data: { checkoutUrl, preferenceId: 'pref-abc123' } })

    const mockLocation = { href: window.location.href }
    const mockedWindow = new Proxy(window, {
      get(target, prop) {
        return prop === 'location' ? mockLocation : target[prop]
      },
    })
    vi.stubGlobal('window', mockedWindow)

    render(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('button', { name: /pagar con mercado pago/i })).toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /pagar con mercado pago/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenLastCalledWith('/payments/create-preference', {
      reservationId: reservation.id,
      token: reservation.token,
    })
    expect(mockLocation.href).toBe(checkoutUrl)

    vi.unstubAllGlobals()
  })

  it('displays the API error message when payment preference creation fails', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()

    mockPost
      .mockResolvedValueOnce({ data: reservation })
      .mockRejectedValueOnce({
        response: { data: { error: { message: 'La reserva expiro' } } },
      })

    render(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('button', { name: /pagar con mercado pago/i })).toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /pagar con mercado pago/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/la reserva expiro/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /pagar con mercado pago/i })
    ).not.toBeDisabled()
  })

  it('prevents payment when the reservation has expired', async () => {
    const reservation = buildReservation({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    mockPost.mockResolvedValueOnce({ data: reservation })

    render(<Checkout />)

    await fillPurchaserForm(userEvent.setup())
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /reserva expirada/i })).toBeInTheDocument()
    })

    expect(screen.queryByRole('button', { name: /pagar con mercado pago/i })).not.toBeInTheDocument()
  })
})
