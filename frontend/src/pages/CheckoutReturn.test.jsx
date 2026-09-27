import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import CheckoutReturn from './CheckoutReturn.jsx'
import { CHECKOUT_RESERVATION_KEY } from '../lib/checkoutReservationStorage.js'

const mockGetSearchParam = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useSearchParams: () => [{ get: (key) => mockGetSearchParam(key) }, vi.fn()],
}))

function setSearchParams(values) {
  mockGetSearchParam.mockImplementation((key) => values[key] ?? null)
}

describe('CheckoutReturn', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGetSearchParam.mockReset()
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
    expect(screen.getByRole('link', { name: /volver al cat[aá]logo/i })).toHaveAttribute(
      'href',
      '/events'
    )
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /reintentar pago/i })).not.toBeInTheDocument()
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

  it('offers retry and catalog actions for a rejected payment with event id', () => {
    setSearchParams({ status: 'rejected', event: 'evt-1' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('link', { name: /reintentar pago/i })).toHaveAttribute(
      'href',
      '/events/evt-1'
    )
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: /volver al cat[aá]logo/i })).toHaveAttribute(
      'href',
      '/events'
    )
  })

  it('omits the retry action for a rejected payment without event id', () => {
    setSearchParams({ status: 'rejected' })

    render(<CheckoutReturn />)

    expect(screen.queryByRole('link', { name: /reintentar pago/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('offers retry and catalog actions for an incomplete checkout with event id', () => {
    setSearchParams({ status: 'failure', event: 'evt-2' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /reintentar pago/i })).toHaveAttribute(
      'href',
      '/events/evt-2'
    )
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('normalizes a literal null event id as absent', () => {
    setSearchParams({ status: 'rejected', event: 'null' })

    render(<CheckoutReturn />)

    expect(screen.queryByRole('link', { name: /reintentar pago/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /buscar mis entradas/i })).not.toBeInTheDocument()
  })

  it('keeps the incomplete state and offers retry for a failure return without status', () => {
    // Abandonment/cancel path: MP appends no status to the failure back URL.
    setSearchParams({ event: 'evt-3' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /no completaste el pago/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /reintentar pago/i })).toHaveAttribute(
      'href',
      '/events/evt-3'
    )
  })

  it('shows rejection when MP reports the real rejected status', () => {
    setSearchParams({ status: 'rejected' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago rechazado/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /reintentar pago/i })).not.toBeInTheDocument()
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

  it('clears the stored checkout reservation on mount after a failed return', () => {
    setSearchParams({ status: 'rejected' })
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({ signature: 'event-1|tt-1|2', id: 'reservation-1' })
    )

    render(<CheckoutReturn />)

    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })
})
