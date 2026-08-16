import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { screen, waitFor, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Checkout from './Checkout.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'

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

  await user.clear(screen.getByLabelText(/^dni$/i))
  if (dni) {
    await user.type(screen.getByLabelText(/^dni$/i), dni)
  }

  await user.clear(screen.getByLabelText('Confirmar DNI'))
  if (confirmDNI) {
    await user.type(screen.getByLabelText('Confirmar DNI'), confirmDNI)
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
  fireEvent.change(screen.getByLabelText(/^dni$/i), {
    target: { value: dni },
  })
  fireEvent.change(screen.getByLabelText('Confirmar DNI'), {
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
    expect(screen.getByText(/x 2/i)).toBeInTheDocument()
    expect(screen.getByText(/total:/i)).toBeInTheDocument()
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

    expect(screen.getByText(/el dni es obligatorio/i)).toBeInTheDocument()
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
    expect(screen.getByText(/x 2/i)).toBeInTheDocument()
    expect(screen.getByText(/total:/i)).toBeInTheDocument()
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

  it('shows validation error when DNIs do not match', async () => {
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(userEvent.setup(), {
      dni: '12345678',
      confirmDNI: '87654321',
    })
    await userEvent.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los dnis no coinciden/i)).toBeInTheDocument()
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows both DNI fields in the form with correct labels', () => {
    renderWithQueryClient(<Checkout />)

    expect(screen.getByLabelText(/^dni$/i)).toBeInTheDocument()
    expect(screen.getByLabelText('Confirmar DNI')).toBeInTheDocument()
  })

  it('clears error when user types in either DNI field after a mismatch', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<Checkout />)

    await fillPurchaserForm(user, {
      dni: '12345678',
      confirmDNI: '87654321',
    })
    await user.click(screen.getByRole('button', { name: /reservar entradas/i }))

    expect(screen.getByText(/los dnis no coinciden/i)).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/^dni$/i))
    await user.type(screen.getByLabelText(/^dni$/i), '12345678')

    expect(screen.queryByText(/los dnis no coinciden/i)).not.toBeInTheDocument()
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

    // Form data should be preserved
    expect(screen.getByLabelText(/nombre completo/i)).toHaveValue('Carlos Ruiz')
    expect(screen.getByLabelText('Email')).toHaveValue('carlos@test.com')
    expect(screen.getByLabelText(/^dni$/i)).toHaveValue('11222333')

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
    expect(screen.queryByText(/los dnis no coinciden/i)).not.toBeInTheDocument()
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
})
