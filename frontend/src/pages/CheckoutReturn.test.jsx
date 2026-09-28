import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CheckoutReturn from './CheckoutReturn.jsx'
import { CHECKOUT_RESERVATION_KEY } from '../lib/checkoutReservationStorage.js'

const mockGetSearchParam = vi.fn()
const mockNavigate = vi.fn()
const mockPost = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
  useSearchParams: () => [{ get: (key) => mockGetSearchParam(key) }, vi.fn()],
}))

vi.mock('../api/client.js', () => ({
  default: { post: (...args) => mockPost(...args) },
}))

function setSearchParams(values) {
  mockGetSearchParam.mockImplementation((key) => values[key] ?? null)
}

describe('CheckoutReturn', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetSearchParam.mockReset()
    mockPost.mockReset()
    mockPost.mockResolvedValue({ data: {} })
    sessionStorage.clear()
  })

  it('renders success confirmation for approved payment', () => {
    setSearchParams({ status: 'approved', payment_id: 'pay-123', external_reference: 'res-456' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    expect(
      screen.getByText(/si la compra fue exitosa, recibir[aá]s un email con tus entradas en la casilla indicada/i)
    ).toBeInTheDocument()
    expect(screen.getByText(/revisá tu casilla de correo/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /volver al cat[aá]logo/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /reintentar pago/i })).not.toBeInTheDocument()
  })

  it('renders success confirmation for success status alias', () => {
    setSearchParams({ status: 'SUCCESS' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    expect(
      screen.getByText(/si la compra fue exitosa, recibir[aá]s un email con tus entradas en la casilla indicada/i)
    ).toBeInTheDocument()
  })

  it('renders pending message for in_process status', () => {
    setSearchParams({ status: 'in_process', payment_id: 'pay-pending-1' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
    expect(screen.getByText(/te avisaremos cuando se confirme/i)).toBeInTheDocument()
    expect(screen.queryByText(/revisá tu casilla de correo/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('renders pending message for pending status alias', () => {
    setSearchParams({ status: 'pending' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
  })

  it('renders rejection message only for rejected status', () => {
    setSearchParams({ status: 'rejected', payment_id: 'pay-fail-1' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago rechazado/i })).toBeInTheDocument()
    expect(screen.getByText(/el pago fue rechazado/i)).toBeInTheDocument()
    expect(screen.queryByText(/revisá tu casilla de correo/i)).not.toBeInTheDocument()
  })

  it('renders incomplete state for the legacy failure status', () => {
    setSearchParams({ status: 'failure' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(
      screen.getByText(/no se realizó ningún cobro\. podés intentar de nuevo cuando quieras\./i)
    ).toBeInTheDocument()
    expect(screen.getByText('No completado')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /pago rechazado/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /resultado del pago/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/revisá tu casilla de correo/i)).not.toBeInTheDocument()
  })

  it('renders incomplete state when no status is provided', () => {
    setSearchParams({})

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(
      screen.getByText(/no se realizó ningún cobro\. podés intentar de nuevo cuando quieras\./i)
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /pago rechazado/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /resultado del pago/i })).not.toBeInTheDocument()
  })

  it('renders incomplete state for an unrecognized status', () => {
    setSearchParams({ status: 'cancelled' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /pago rechazado/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /resultado del pago/i })).not.toBeInTheDocument()
  })

  it('renders the neutral incomplete state for an abandoned checkout (literal null params)', () => {
    setSearchParams({
      status: 'null',
      payment_id: 'null',
      external_reference: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /pago rechazado/i })).not.toBeInTheDocument()
    expect(screen.queryByText(/revisá tu casilla de correo/i)).not.toBeInTheDocument()
    expect(screen.queryByText('null')).not.toBeInTheDocument()
    expect(screen.queryByText('3fa85f64-5717-4562-b3fc-2c963f66afa6')).not.toBeInTheDocument()
    expect(screen.queryByText(/id de pago:/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/referencia:/i)).not.toBeInTheDocument()
  })

  it('offers retry and catalog actions for a rejected payment with event id', async () => {
    const user = userEvent.setup()
    setSearchParams({ status: 'rejected', event: 'evt-1' })

    render(<CheckoutReturn />)

    const retry = screen.getByRole('button', { name: /reintentar pago/i })
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /volver al cat[aá]logo/i })).toBeInTheDocument()

    // No stored hold to restore → the retry falls back to the event page.
    await user.click(retry)
    expect(mockNavigate).toHaveBeenCalledWith('/events/evt-1')
  })

  it('omits the retry action for a rejected payment without event id and stored hold', () => {
    setSearchParams({ status: 'rejected' })

    render(<CheckoutReturn />)

    expect(screen.queryByRole('button', { name: /reintentar pago/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('offers retry and catalog actions for an incomplete checkout with event id', () => {
    setSearchParams({ status: 'failure', event: 'evt-2' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /reintentar pago/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('normalizes a literal null event id as absent', () => {
    setSearchParams({ status: 'rejected', event: 'null' })

    render(<CheckoutReturn />)

    expect(screen.queryByRole('button', { name: /reintentar pago/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('keeps the incomplete state and offers retry for a failure return without status', () => {
    // Abandonment/cancel path: MP appends no status to the failure back URL.
    setSearchParams({ event: 'evt-3' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /reintentar pago/i })).toBeInTheDocument()
  })

  it('shows rejection when MP reports the real rejected status', () => {
    setSearchParams({ status: 'rejected' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago rechazado/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /reintentar pago/i })).not.toBeInTheDocument()
  })

  it('treats a literal null status as incomplete, not rejected', () => {
    setSearchParams({ status: 'null' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /pago rechazado/i })).not.toBeInTheDocument()
  })

  it('falls back to pending when only the origin marker is present', () => {
    setSearchParams({ origin: 'pending' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
  })

  it('prefers MP approved status over the origin marker', () => {
    setSearchParams({ status: 'approved', origin: 'pending' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
  })

  it('treats literal null status with the origin marker as pending', () => {
    setSearchParams({ status: 'null', origin: 'pending' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: /no completaste el pago/i })
    ).not.toBeInTheDocument()
  })

  it('clears the stored checkout reservation on mount after a successful return', () => {
    setSearchParams({ status: 'approved' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({ signature: 'event-1|tt-1|2', id: 'reservation-1' })
    )

    render(<CheckoutReturn />)

    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('keeps the stored checkout reservation after a failed return so retry can restore the purchase', () => {
    setSearchParams({ status: 'rejected' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({
        signature: 'event-1|tt-1|2',
        id: 'reservation-1',
        expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
      })
    )

    render(<CheckoutReturn />)

    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).not.toBeNull()
  })

  it('restores the interrupted purchase when the buyer retries', async () => {
    const user = userEvent.setup()
    setSearchParams({ status: 'rejected', event: 'evt-1' })
    const cart = {
      eventId: 'evt-1',
      eventName: 'Recital',
      selection: { ticketTypeId: 'tt-1', name: 'Platea', price: 15000, quantity: 2 },
      totalPrice: 30000,
    }
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({
        signature: 'evt-1|tt-1|2',
        id: 'reservation-1',
        token: 'tok-1',
        expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
        cart,
      })
    )

    render(<CheckoutReturn />)

    await user.click(screen.getByRole('button', { name: /reintentar pago/i }))

    expect(mockNavigate).toHaveBeenCalledWith('/checkout', { state: cart })
  })

  it('falls back to the event page when the stored hold already lapsed', async () => {
    const user = userEvent.setup()
    setSearchParams({ status: 'rejected', event: 'evt-1' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({
        signature: 'evt-1|tt-1|2',
        id: 'reservation-1',
        expiresAt: new Date(Date.now() - 60 * 1000).toISOString(),
        cart: { eventId: 'evt-1', selection: { ticketTypeId: 'tt-1', quantity: 2 } },
      })
    )

    render(<CheckoutReturn />)

    await user.click(screen.getByRole('button', { name: /reintentar pago/i }))

    expect(mockNavigate).toHaveBeenCalledWith('/events/evt-1')
    // A lapsed hold is discarded, never restored.
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('releases the held tickets immediately when leaving to the catalog', async () => {
    const user = userEvent.setup()
    setSearchParams({ status: 'rejected', event: 'evt-1' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({
        signature: 'evt-1|tt-1|2',
        id: 'reservation-1',
        token: 'tok-1',
        expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
        cart: { eventId: 'evt-1', selection: { ticketTypeId: 'tt-1', quantity: 2 } },
      })
    )

    render(<CheckoutReturn />)

    await user.click(screen.getByRole('button', { name: /volver al cat[aá]logo/i }))

    expect(mockPost).toHaveBeenCalledWith('/reservations/reservation-1/cancel', {
      token: 'tok-1',
    })
    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
    expect(mockNavigate).toHaveBeenCalledWith('/events')
  })

  it('does not release anything when leaving from a successful return', async () => {
    const user = userEvent.setup()
    setSearchParams({ status: 'approved' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({
        signature: 'evt-1|tt-1|2',
        id: 'reservation-1',
        token: 'tok-1',
        expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(),
      })
    )

    render(<CheckoutReturn />)

    await user.click(screen.getByRole('button', { name: /volver al cat[aá]logo/i }))

    expect(mockPost).not.toHaveBeenCalled()
    expect(mockNavigate).toHaveBeenCalledWith('/events')
  })
})
