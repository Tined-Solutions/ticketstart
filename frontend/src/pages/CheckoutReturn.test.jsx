import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import CheckoutReturn from './CheckoutReturn.jsx'

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
  })

  it('renders success confirmation for approved payment', () => {
    setSearchParams({ status: 'approved', payment_id: 'pay-123', external_reference: 'res-456' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    expect(
      screen.getByText(/si la compra fue exitosa, recibiras un email con tus entradas en la casilla indicada/i)
    ).toBeInTheDocument()
    expect(screen.getByText(/revisá tu casilla de correo/i)).toBeInTheDocument()
    expect(screen.getByText('pay-123')).toBeInTheDocument()
    expect(screen.getByText('res-456')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /volver al catalogo/i })).toHaveAttribute(
      'href',
      '/events'
    )
  })

  it('renders success confirmation for success status alias', () => {
    setSearchParams({ status: 'SUCCESS' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    expect(
      screen.getByText(/si la compra fue exitosa, recibiras un email con tus entradas en la casilla indicada/i)
    ).toBeInTheDocument()
  })

  it('renders pending message for in_process status', () => {
    setSearchParams({ status: 'in_process', payment_id: 'pay-pending-1' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
    expect(screen.getByText(/te avisaremos cuando se confirme/i)).toBeInTheDocument()
    expect(screen.getByText('pay-pending-1')).toBeInTheDocument()
  })

  it('renders pending message for pending status alias', () => {
    setSearchParams({ status: 'pending' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
  })

  it('renders failure message for rejected payment', () => {
    setSearchParams({ status: 'rejected', payment_id: 'pay-fail-1' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago rechazado/i })).toBeInTheDocument()
    expect(screen.getByText(/el pago fue rechazado/i)).toBeInTheDocument()
    expect(screen.getByText('pay-fail-1')).toBeInTheDocument()
  })

  it('renders failure message for failure status alias', () => {
    setSearchParams({ status: 'failure' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /pago rechazado/i })).toBeInTheDocument()
    expect(screen.getByText(/el pago fue rechazado/i)).toBeInTheDocument()
  })

  it('renders unknown status message when no status is provided', () => {
    setSearchParams({})

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /resultado del pago/i })).toBeInTheDocument()
    expect(
      screen.getByText(/no pudimos determinar el estado del pago/i)
    ).toBeInTheDocument()
  })

  it('renders unknown status message for an unrecognized status', () => {
    setSearchParams({ status: 'cancelled' })

    render(<CheckoutReturn />)

    expect(screen.getByRole('heading', { name: /resultado del pago/i })).toBeInTheDocument()
    expect(
      screen.getByText(/no pudimos determinar el estado del pago/i)
    ).toBeInTheDocument()
  })

  it('hides payment details when query parameters are absent', () => {
    setSearchParams({ status: 'approved' })

    render(<CheckoutReturn />)

    expect(screen.queryByText(/id de pago:/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/referencia:/i)).not.toBeInTheDocument()
  })
})
