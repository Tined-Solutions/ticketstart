import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { screen, waitFor, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Checkout from './Checkout.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'
import {
  CHECKOUT_RESERVATION_KEY,
  buildCartSignature,
} from '../lib/checkoutReservationStorage.js'

const mockNavigate = vi.fn()
const mockPost = vi.fn()
const mockPatch = vi.fn()
const mockLocationState = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
  useLocation: () => ({ state: mockLocationState() }),
}))

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
    patch: (...args) => mockPatch(...args),
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

const cartSignature = buildCartSignature({
  eventId: cart.eventId,
  ticketTypeId: cart.selection.ticketTypeId,
  quantity: cart.selection.quantity,
})

function buildStoredEntry(overrides = {}) {
  return {
    signature: cartSignature,
    id: 'reservation-restored',
    token: 'restored-token',
    expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
    quantity: 2,
    purchaserName: 'Ana Restaurada',
    purchaserEmail: 'ana@test.com',
    purchaserDNI: '30111222',
    documentCountry: 'AR',
    ...overrides,
  }
}

function storeEntry(entry) {
  sessionStorage.setItem(CHECKOUT_RESERVATION_KEY, JSON.stringify(entry))
}

function readStoredEntry() {
  const raw = sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)
  return raw ? JSON.parse(raw) : null
}

async function fillPurchaserForm(
  user,
  { name = 'Juan Perez', email = 'juan@example.com', confirmEmail = email, dni = '12345678', confirmDNI = dni } = {}
) {
  await user.clear(screen.getByLabelText(/nombre completo/i))
  if (name) {
    await user.type(screen.getByLabelText(/nombre completo/i), name)
  }

  await user.clear(screen.getByLabelText('Email'))
  if (email) {
    await user.type(screen.getByLabelText('Email'), email)
  }

  await user.clear(screen.getByLabelText('Confirmar email'))
  if (confirmEmail) {
    await user.type(screen.getByLabelText('Confirmar email'), confirmEmail)
  }

  await user.clear(screen.getByLabelText(/^(dni|c[eé]dula)$/i))
  if (dni) {
    await user.type(screen.getByLabelText(/^(dni|c[eé]dula)$/i), dni)
  }

  await user.clear(screen.getByLabelText(/^confirmar (dni|c[eé]dula)$/i))
  if (confirmDNI) {
    await user.type(screen.getByLabelText(/^confirmar (dni|c[eé]dula)$/i), confirmDNI)
  }
}

function fillPurchaserFormFire(
  { name = 'Juan Perez', email = 'juan@example.com', confirmEmail = email, dni = '12345678', confirmDNI = dni } = {}
) {
  fireEvent.change(screen.getByLabelText(/nombre completo/i), {
    target: { value: name },
  })
  fireEvent.change(screen.getByLabelText('Email'), {
    target: { value: email },
  })
  fireEvent.change(screen.getByLabelText('Confirmar email'), {
    target: { value: confirmEmail },
  })
  fireEvent.change(screen.getByLabelText(/^(dni|c[eé]dula)$/i), {
    target: { value: dni },
  })
  fireEvent.change(screen.getByLabelText(/^confirmar (dni|c[eé]dula)$/i), {
    target: { value: confirmDNI },
  })
}

describe('Checkout', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockPatch.mockReset()
    mockNavigate.mockReset()
    mockLocationState.mockReset()
    mockLocationState.mockReturnValue(cart)
    sessionStorage.clear()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('redirects to the event catalog when there is no cart state', async () => {
    mockLocationState.mockReturnValue(undefined)

    renderWithQueryClient(<Checkout />)

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/events', { replace: true })
    })
  })

  it('renders the reservation form with event and selection summary', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByRole('heading', { name: /reserva tus entradas/i })).toBeInTheDocument()
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    expect(screen.getByText(/estadio luna park/i)).toBeInTheDocument()
    expect(screen.getByText(/platea/i)).toBeInTheDocument()
    expect(screen.getByText(/cantidad/i)).toBeInTheDocument()
    expect(screen.getByText(/^2$/i)).toBeInTheDocument()
    expect(screen.getByText(/total/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/nombre completo/i)).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar email')).toBeInTheDocument()
    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar DNI')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /reservar entradas/i })
    ).toBeInTheDocument()
  })

  it('shows a validation error when DNI is missing', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), { dni: ' ' })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/el documento es obligatorio/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('creates a reservation and displays the confirmation with countdown timer', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledWith('/reservations', {
      eventId: cart.eventId,
      ticketTypeId: cart.selection.ticketTypeId,
      quantity: cart.selection.quantity,
      purchaserName: 'Juan Perez',
      purchaserEmail: 'juan@example.com',
      confirmEmail: 'juan@example.com',
      purchaserDNI: '12345678',
    })
    expect(
      screen.getByRole('heading', { name: /confirma tu reserva/i })
    ).toBeInTheDocument()
    expect(screen.getByRole('timer')).toHaveTextContent('10:00')
    expect(screen.getByText(/platea/i)).toBeInTheDocument()
    expect(screen.getByText(/cantidad/i)).toBeInTheDocument()
    expect(screen.getByText(/^2$/i)).toBeInTheDocument()
    expect(screen.getByText(/total/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /confirmar y proceder al pago/i })
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /editar datos/i })
    ).toBeInTheDocument()
    expect(screen.getByText(/datos del comprador/i)).toBeInTheDocument()
    expect(screen.getByText('Juan Perez')).toBeInTheDocument()
    expect(screen.getByText('juan@example.com')).toBeInTheDocument()
    expect(screen.getByText('12345678')).toBeInTheDocument()
  })

  it('updates the countdown timer as time advances', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

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

  it('starts the countdown at reservation creation, not at page mount', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    renderWithQueryClient(<Checkout />)

    // Time passes while the user fills the form (1 minute): the page was
    // mounted BEFORE the reservation existed.
    vi.advanceTimersByTime(60_000)

    // Reservation is created NOW (server-side 10-minute expiry from this moment).
    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    // Must read ~10:00 (600s), NOT 11:00 (660s — the bug: countdown from page mount).
    expect(screen.getByRole('timer')).toHaveTextContent('10:00')
  })

  it('shows the expiration view when the reservation expires', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

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
    expect(screen.getByText(/tu reserva ya no es válida/i)).toBeInTheDocument()
    expect(screen.getByText(/las entradas fueron liberadas/i)).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
  })

  it('navigates back to the catalog from the expired view', async () => {
    const reservation = buildReservation({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup())
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /volver al catálogo/i })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /volver al catálogo/i }))

    expect(mockNavigate).toHaveBeenCalledWith('/events', { replace: true })
  })

  it('displays the API error message when reservation creation fails', async () => {
    mockPost.mockRejectedValueOnce({
      response: { data: { error: { message: 'No hay stock disponible' } } },
    })

    renderWithQueryClient(<Checkout />)

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

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('button', { name: /confirmar y proceder al pago/i })).toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /confirmar y proceder al pago/i }))
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

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('button', { name: /confirmar y proceder al pago/i })).toBeInTheDocument()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /confirmar y proceder al pago/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/la reserva expiro/i)).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /confirmar y proceder al pago/i })
    ).not.toBeDisabled()
  })

  it('prevents payment when the reservation has expired', async () => {
    const reservation = buildReservation({
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    })
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup())
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /reserva expirada/i })).toBeInTheDocument()
    })

    expect(screen.queryByRole('button', { name: /confirmar y proceder al pago/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /editar datos/i })).not.toBeInTheDocument()
  })

  it('renders a second email input labeled "Confirmar email"', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText('Confirmar email')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar email')).toHaveAttribute('type', 'email')
    expect(screen.getByLabelText('Confirmar email')).toBeRequired()
  })

  it('blocks paste on the confirm email field', () => {
    renderWithQueryClient(<Checkout />)

    const confirmInput = screen.getByLabelText('Confirmar email')
    const pasteEvent = new Event('paste', { bubbles: true, cancelable: true })
    const prevented = !confirmInput.dispatchEvent(pasteEvent)

    expect(prevented).toBe(true)
  })

  it('shows validation error when emails do not match', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), {
      email: 'juan@example.com',
      confirmEmail: 'diferente@example.com',
    })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los emails no coinciden/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('accepts a confirm email that differs only by case', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire({ email: 'Foo@Bar.com', confirmEmail: 'foo@bar.com' })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(mockPost).toHaveBeenCalledWith('/reservations', {
      eventId: cart.eventId,
      ticketTypeId: cart.selection.ticketTypeId,
      quantity: cart.selection.quantity,
      purchaserName: 'Juan Perez',
      purchaserEmail: 'Foo@Bar.com',
      confirmEmail: 'foo@bar.com',
      purchaserDNI: '12345678',
    })
    expect(screen.queryByText(/los emails no coinciden/i)).not.toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: /confirma tu reserva/i })
    ).toBeInTheDocument()
  })

  it('still shows the mismatch error when the confirm email is genuinely different', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), {
      email: 'foo@bar.com',
      confirmEmail: 'foo@baz.com',
    })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los emails no coinciden/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows both email fields in the form with correct labels', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar email')).toBeInTheDocument()
  })

  it('clears error when user types in either email field after a mismatch', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(user, {
      email: 'juan@example.com',
      confirmEmail: 'diferente@example.com',
    })
    await user.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los emails no coinciden/i)).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'juan@example.com')

    expect(screen.queryByText(/los emails no coinciden/i)).not.toBeInTheDocument()
  })

  it('renders a second DNI input labeled "Confirmar DNI"', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText('Confirmar DNI')).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar DNI')).toHaveAttribute('type', 'text')
    expect(screen.getByLabelText('Confirmar DNI')).toBeRequired()
  })

  it('blocks paste on the confirm DNI field', () => {
    renderWithQueryClient(<Checkout />)

    const confirmInput = screen.getByLabelText('Confirmar DNI')
    const pasteEvent = new Event('paste', { bubbles: true, cancelable: true })
    const prevented = !confirmInput.dispatchEvent(pasteEvent)

    expect(prevented).toBe(true)
  })

  it('strips non-digit characters while typing in the confirm DNI field', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    const confirmInput = screen.getByLabelText('Confirmar DNI')
    await user.type(confirmInput, '12abc34def')

    // Behaves like the primary DNI field: only digits are kept as you type.
    expect(confirmInput).toHaveValue('1234')
  })

  it('shows validation error when DNIs do not match', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), {
      dni: '12345678',
      confirmDNI: '87654321',
    })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los documentos no coinciden/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows both DNI fields in the form with correct labels', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar DNI')).toBeInTheDocument()
  })

  it('switches the document field label to "Cédula" when Uruguay is selected', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    // Default country is Argentina → label reads DNI
    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()

    await user.selectOptions(
      screen.getByLabelText('País del documento'),
      'UY'
    )

    // Uruguay calls it "cédula", not DNI — the label is display-only, the
    // submitted value (clean digits) does not change.
    expect(screen.getByLabelText('Cédula')).toBeInTheDocument()
    expect(screen.queryByLabelText(/^dni$/i)).not.toBeInTheDocument()
  })

  it('switches the confirm field label to "Confirmar Cédula" when Uruguay is selected', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText('Confirmar DNI')).toBeInTheDocument()

    await user.selectOptions(
      screen.getByLabelText('País del documento'),
      'UY'
    )

    expect(screen.getByLabelText('Confirmar Cédula')).toBeInTheDocument()
    expect(screen.queryByLabelText('Confirmar DNI')).not.toBeInTheDocument()
  })

  it('shows "Cédula" as the document label in the confirmation review when Uruguay is selected', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fireEvent.change(screen.getByLabelText('País del documento'), {
      target: { value: 'UY' },
    })

    fillPurchaserFormFire({
      name: 'Maria Gomez',
      email: 'maria@test.com',
      dni: '51234561',
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/datos del comprador/i)).toBeInTheDocument()
    expect(screen.getByText('Cédula')).toBeInTheDocument()
    expect(screen.queryByText('DNI')).not.toBeInTheDocument()
  })

  it('clears error when user types in either DNI field after a mismatch', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(user, {
      dni: '12345678',
      confirmDNI: '87654321',
    })
    await user.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los documentos no coinciden/i)).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/^dni$/i))
    await user.type(screen.getByLabelText(/^dni$/i), '12345678')

    expect(screen.queryByText(/los documentos no coinciden/i)).not.toBeInTheDocument()
  })

  it('displays purchaser data in the confirmation review section', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire({
      name: 'Maria Gomez',
      email: 'maria@test.com',
      dni: '99887766',
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByText(/datos del comprador/i)).toBeInTheDocument()
    expect(screen.getByText('Maria Gomez')).toBeInTheDocument()
    expect(screen.getByText('maria@test.com')).toBeInTheDocument()
    expect(screen.getByText('99887766')).toBeInTheDocument()
  })

  it('returns to the reservation form when clicking Editar datos, preserving input data', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire({
      name: 'Carlos Ruiz',
      email: 'carlos@test.com',
      dni: '11222333',
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    // Click "Editar datos" to go back
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /editar datos/i }))
      await Promise.resolve()
    })

    // Should be back on the edit form with the reservation still active
    expect(screen.getByRole('heading', { name: /editar tus datos/i })).toBeInTheDocument()

    // Form data should be preserved (DNI shows the formatted value — the
    // component formats valid documents when the input is not focused)
    expect(screen.getByLabelText(/nombre completo/i)).toHaveValue('Carlos Ruiz')
    expect(screen.getByLabelText('Email')).toHaveValue('carlos@test.com')
    expect(screen.getByLabelText(/^dni$/i)).toHaveValue('11.222.333')

    // The "Guardar cambios" button should be available
    expect(
      screen.getByRole('button', { name: /guardar cambios/i })
    ).toBeInTheDocument()
  })

  it('sends a PATCH request when saving edits on an existing reservation', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire({
      name: 'Original Name',
      email: 'original@test.com',
      dni: '12345678',
    })

    // Create initial reservation
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    // Click "Editar datos"
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /editar datos/i }))
      await Promise.resolve()
    })

    // Change the data
    fireEvent.change(screen.getByLabelText(/nombre completo/i), {
      target: { value: 'Nombre Editado' },
    })
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'editado@test.com' },
    })
    fireEvent.change(screen.getByLabelText('Confirmar email'), {
      target: { value: 'editado@test.com' },
    })
    fireEvent.change(screen.getByLabelText(/^dni$/i), {
      target: { value: '87654321' },
    })
    fireEvent.change(screen.getByLabelText('Confirmar DNI'), {
      target: { value: '87654321' },
    })

    // Setup PATCH mock for the edit
    mockPatch.mockResolvedValueOnce({ data: reservation })

    // Save changes
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    // Should have called PATCH, NOT POST
    expect(mockPatch).toHaveBeenCalledWith(
      `/reservations/${reservation.id}`,
      expect.objectContaining({
        purchaserName: 'Nombre Editado',
        purchaserEmail: 'editado@test.com',
        purchaserDNI: '87654321',
        token: reservation.token,
      })
    )
    // Should NOT have created a second reservation
    expect(mockPost).toHaveBeenCalledTimes(1)
  })

  it('shows validation error when name is empty', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), { name: '' })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/el nombre es obligatorio/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows validation error when email is empty', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), { email: '', confirmEmail: '' })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows validation error when email format is invalid', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), {
      email: 'no-es-un-email',
      confirmEmail: 'no-es-un-email',
    })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/formato de email inválido/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('matches DNIs by their numeric value ignoring formatting', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    // Type clean numeric in DNI field, formatted with dots in confirm DNI
    fillPurchaserFormFire({
      name: 'Test User',
      email: 'test@test.com',
      dni: '43350328',
      confirmDNI: '43.350.328',
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    // Should NOT show "DNIs no coinciden" — they're the same after cleaning
    expect(screen.queryByText(/los documentos no coinciden/i)).not.toBeInTheDocument()
    expect(mockPost).toHaveBeenCalled()
  })

  it('formats confirm DNI on blur matching the primary DNI format', async () => {
    renderWithQueryClient(<Checkout />)

    const confirmInput = screen.getByLabelText('Confirmar DNI')

    // Type raw digits
    await userEvent.type(confirmInput, '43350328')

    // While focused, shows raw value
    expect(confirmInput).toHaveValue('43350328')

    // Blur the field
    fireEvent.blur(confirmInput)

    // After blur, should show formatted value
    await waitFor(() => {
      expect(confirmInput).toHaveValue('43.350.328')
    })
  })

  it('sets autocomplete and spellcheck attributes for a11y', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText(/nombre completo/i)).toHaveAttribute('autocomplete', 'name')
    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'email')
    expect(screen.getByLabelText('Email')).toHaveAttribute('spellcheck', 'false')
    expect(screen.getByLabelText('Confirmar email')).toHaveAttribute('autocomplete', 'off')
    expect(screen.getByLabelText('Confirmar DNI')).toHaveAttribute('autocomplete', 'off')
  })

  it('renders field errors with role=alert and links them via aria-describedby', async () => {
    renderWithQueryClient(<Checkout />)

    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    const alerts = screen.getAllByRole('alert')
    expect(alerts.length).toBeGreaterThanOrEqual(1)
    expect(alerts[0]).toHaveTextContent(/el nombre es obligatorio/i)
    expect(screen.getByLabelText(/nombre completo/i)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText(/nombre completo/i)).toHaveAttribute('aria-describedby', 'purchaserName-error')
  })

  it('shows a step progress indicator on the reservation form', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByText(/paso 1 de 2/i)).toBeInTheDocument()
  })

  it('shows a non-color warning cue when the countdown is low', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation({
      expiresAt: new Date('2026-07-13T12:00:25Z').toISOString(),
    })
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(screen.getByRole('timer')).toHaveTextContent('00:25')
    expect(screen.getByText(/quedan pocos segundos/i)).toBeInTheDocument()
  })

  // -- reservation persistence across remounts (back/forward) ---------------

  it('restores an active reservation from sessionStorage without creating a new one', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    storeEntry(
      buildStoredEntry({
        expiresAt: new Date('2026-07-13T12:05:00Z').toISOString(),
      })
    )

    renderWithQueryClient(<Checkout />)

    expect(
      screen.getByRole('heading', { name: /confirma tu reserva/i })
    ).toBeInTheDocument()
    expect(screen.getByRole('timer')).toHaveTextContent('05:00')
    expect(screen.getByText('Ana Restaurada')).toBeInTheDocument()
    expect(screen.getByText('ana@test.com')).toBeInTheDocument()
    expect(screen.getByText('30111222')).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: /reserva tus entradas/i })
    ).not.toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('ignores a stored reservation whose signature belongs to a different cart', () => {
    storeEntry(
      buildStoredEntry({ signature: `${cartSignature}|otra-compra` })
    )

    renderWithQueryClient(<Checkout />)

    expect(
      screen.getByRole('heading', { name: /reserva tus entradas/i })
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: /confirma tu reserva/i })
    ).not.toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('ignores an expired stored reservation and removes it', () => {
    storeEntry(
      buildStoredEntry({
        expiresAt: new Date(Date.now() - 1000).toISOString(),
      })
    )

    renderWithQueryClient(<Checkout />)

    expect(
      screen.getByRole('heading', { name: /reserva tus entradas/i })
    ).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('persists the created reservation to sessionStorage', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(readStoredEntry()).toEqual({
      signature: cartSignature,
      id: reservation.id,
      token: reservation.token,
      expiresAt: reservation.expiresAt,
      quantity: reservation.quantity,
      purchaserName: 'Juan Perez',
      purchaserEmail: 'juan@example.com',
      purchaserDNI: '12345678',
      documentCountry: 'AR',
    })
  })

  it('updates the stored reservation purchaser fields after editing data', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire({
      name: 'Original Name',
      email: 'original@test.com',
      dni: '12345678',
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /editar datos/i }))
      await Promise.resolve()
    })

    fireEvent.change(screen.getByLabelText(/nombre completo/i), {
      target: { value: 'Nombre Editado' },
    })
    fireEvent.change(screen.getByLabelText('Email'), {
      target: { value: 'editado@test.com' },
    })
    fireEvent.change(screen.getByLabelText('Confirmar email'), {
      target: { value: 'editado@test.com' },
    })
    fireEvent.change(screen.getByLabelText(/^dni$/i), {
      target: { value: '87654321' },
    })
    fireEvent.change(screen.getByLabelText('Confirmar DNI'), {
      target: { value: '87654321' },
    })

    mockPatch.mockResolvedValueOnce({ data: reservation })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /guardar cambios/i }))
      await Promise.resolve()
    })

    expect(readStoredEntry()).toEqual({
      signature: cartSignature,
      id: reservation.id,
      token: reservation.token,
      expiresAt: reservation.expiresAt,
      quantity: reservation.quantity,
      purchaserName: 'Nombre Editado',
      purchaserEmail: 'editado@test.com',
      purchaserDNI: '87654321',
      documentCountry: 'AR',
    })
  })

  it('clears the stored reservation when the hold expires', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-13T12:00:00Z'))

    const reservation = buildReservation()
    mockPost.mockResolvedValueOnce({ data: reservation })

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    expect(readStoredEntry()).not.toBeNull()

    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000)
    })

    expect(
      screen.getByRole('heading', { name: /reserva expirada/i })
    ).toBeInTheDocument()
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  // -- return from Mercado Pago (WI7) ----------------------------------------

  it('persists the preference id in the stored reservation when paying', async () => {
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

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /confirmar y proceder al pago/i }))
      await Promise.resolve()
    })

    expect(readStoredEntry()?.preferenceId).toBe('pref-abc123')
    expect(mockLocation.href).toBe(checkoutUrl)

    vi.unstubAllGlobals()
  })

  it('verifies a stored payment on mount and shows the confirmed panel', async () => {
    storeEntry(buildStoredEntry({ preferenceId: 'pref-stored' }))
    mockPost.mockResolvedValueOnce({ data: { status: 'confirmed' } })

    renderWithQueryClient(<Checkout />)

    expect(screen.getByText(/verificando el estado de tu pago/i)).toBeInTheDocument()

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    })

    expect(mockPost).toHaveBeenCalledWith('/payments/confirm', {
      preferenceId: 'pref-stored',
    })
    expect(screen.getByText(/tus entradas fueron enviadas a tu email/i)).toBeInTheDocument()
    expect(screen.getByText(/revisá tu casilla de correo/i)).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: /buscar mis entradas/i })
    ).toHaveAttribute('href', '/tickets/lookup')
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('shows the pending panel without a pay button and re-verifies on demand', async () => {
    storeEntry(buildStoredEntry({ preferenceId: 'pref-pending' }))
    mockPost
      .mockResolvedValueOnce({ data: { status: 'pending', reason: 'payment_pending' } })
      .mockResolvedValueOnce({ data: { status: 'pending', reason: 'payment_pending' } })

    renderWithQueryClient(<Checkout />)

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
    })

    expect(screen.getByText(/no hace falta que pagues de nuevo/i)).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /confirmar y proceder al pago/i })
    ).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /verificar de nuevo/i }))

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledTimes(2)
    })
    expect(mockPost).toHaveBeenLastCalledWith('/payments/confirm', {
      preferenceId: 'pref-pending',
    })
  })

  it('returns to phase 2 with a notice when no payment was registered', async () => {
    storeEntry(buildStoredEntry({ preferenceId: 'pref-nopay' }))
    mockPost.mockResolvedValueOnce({ data: { status: 'pending', reason: 'no_payment' } })

    renderWithQueryClient(<Checkout />)

    await waitFor(() => {
      expect(screen.getByText(/no registramos un pago todavía/i)).toBeInTheDocument()
    })

    expect(
      screen.getByRole('button', { name: /confirmar y proceder al pago/i })
    ).toBeEnabled()
  })

  it('shows the unverified panel when the confirm request fails', async () => {
    storeEntry(buildStoredEntry({ preferenceId: 'pref-error' }))
    mockPost.mockRejectedValueOnce(new Error('Network error'))

    renderWithQueryClient(<Checkout />)

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /no pudimos verificar tu pago/i })
      ).toBeInTheDocument()
    })

    expect(screen.getByText(/reintentá en unos segundos/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /verificar de nuevo/i })).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /confirmar y proceder al pago/i })
    ).not.toBeInTheDocument()
  })

  it('resets the pay button and re-verifies on a bfcache pageshow', async () => {
    const reservation = buildReservation()
    mockPost
      .mockResolvedValueOnce({ data: reservation })
      .mockResolvedValueOnce({
        data: { checkoutUrl: 'https://mp.test/checkout', preferenceId: 'pref-bfc' },
      })
      .mockResolvedValueOnce({ data: { status: 'pending', reason: 'no_payment' } })

    const mockLocation = { href: window.location.href }
    const mockedWindow = new Proxy(window, {
      get(target, prop) {
        return prop === 'location' ? mockLocation : target[prop]
      },
    })
    vi.stubGlobal('window', mockedWindow)

    renderWithQueryClient(<Checkout />)

    fillPurchaserFormFire()

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))
      await Promise.resolve()
    })

    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /confirmar y proceder al pago/i }))
      await Promise.resolve()
    })

    // The redirect never happens in jsdom, so payLoading stays true: the button
    // is stuck on "Preparando pago…", exactly like a bfcache-frozen checkout.
    expect(screen.getByRole('button', { name: /preparando pago…/i })).toBeDisabled()

    await act(async () => {
      const pageShow = new Event('pageshow')
      Object.defineProperty(pageShow, 'persisted', { value: true })
      window.dispatchEvent(pageShow)
      await Promise.resolve()
    })

    await waitFor(() => {
      expect(screen.getByText(/no registramos un pago todavía/i)).toBeInTheDocument()
    })

    expect(
      screen.getByRole('button', { name: /confirmar y proceder al pago/i })
    ).toBeEnabled()
    expect(mockPost).toHaveBeenLastCalledWith('/payments/confirm', {
      preferenceId: 'pref-bfc',
    })

    vi.unstubAllGlobals()
  })
})
