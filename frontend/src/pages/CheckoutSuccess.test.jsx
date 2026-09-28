import { describe, it, expect, vi, beforeEach } from 'vitest'
import { screen, waitFor, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CheckoutSuccess, {
  AUTO_RECHECK_DELAY_MS,
  MAX_AUTO_RECHECKS,
} from './CheckoutSuccess.jsx'
import { renderWithQueryClient } from '../test/queryClientUtils.jsx'
import { CHECKOUT_RESERVATION_KEY } from '../lib/checkoutReservationStorage.js'

const mockPost = vi.fn()
const mockGetSearchParam = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useSearchParams: () => [{ get: (key) => mockGetSearchParam(key) }, vi.fn()],
}))

vi.mock('../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
  },
}))

function setSearchParams(values) {
  mockGetSearchParam.mockImplementation((key) => values[key] ?? null)
}

describe('CheckoutSuccess', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    mockGetSearchParam.mockReset()
    sessionStorage.clear()
  })

  it('shows the confirming state while payment is being verified', () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost.mockImplementation(() => new Promise(() => {}))

    renderWithQueryClient(<CheckoutSuccess />)

    expect(screen.getByText(/confirmando tu pago…/i)).toBeInTheDocument()
  })

  it('shows the confirmed state with success message when payment is confirmed', async () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost.mockResolvedValue({ data: { status: 'confirmed' } })

    renderWithQueryClient(<CheckoutSuccess />)

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    })
    expect(
      screen.getByText(/tus entradas fueron enviadas a tu email/i)
    ).toBeInTheDocument()
    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/payments/confirm', {
      preferenceId: 'pref-123',
    })
  })

  it('shows the error state with retry button when the API call fails', async () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost.mockRejectedValue(new Error('Network error'))

    renderWithQueryClient(<CheckoutSuccess />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /reintentar/i })).toBeInTheDocument()
    })
    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent(
      /no se pudo conectar con el servidor. reintentá o volvé a intentar en unos minutos/i
    )
  })

  it('shows the pending feedback and never leaks the backend error message', async () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost.mockResolvedValue({
      data: {
        status: 'pending',
        reason: 'payment_pending',
        error: 'A payment is still pending for this preference',
      },
    })

    renderWithQueryClient(<CheckoutSuccess />)

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /confirmando tu pago/i })
      ).toBeInTheDocument()
    })
    expect(screen.getByText(/no cierres esta página ni pagues de nuevo/i)).toBeInTheDocument()
    expect(
      screen.queryByText(/a payment is still pending for this preference/i)
    ).not.toBeInTheDocument()
  })

  it('retries the confirmation when the user clicks the retry button', async () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce({ data: { status: 'confirmed' } })

    renderWithQueryClient(<CheckoutSuccess />)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /reintentar/i })).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    })
    expect(mockPost).toHaveBeenCalledTimes(2)
  })

  it('re-verifies the payment from the pending state without re-paying', async () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost
      .mockResolvedValueOnce({ data: { status: 'in_process' } })
      .mockResolvedValueOnce({ data: { status: 'confirmed' } })

    renderWithQueryClient(<CheckoutSuccess />)

    await waitFor(() => {
      expect(
        screen.getByRole('heading', { name: /confirmando tu pago/i })
      ).toBeInTheDocument()
    })

    const verifyButton = screen.getByRole('button', { name: /verificar de nuevo/i })
    // "Reintentar" would wrongly suggest re-paying; pending only re-checks.
    expect(screen.queryByRole('button', { name: /reintentar/i })).not.toBeInTheDocument()

    await userEvent.click(verifyButton)

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
    })
    expect(mockPost).toHaveBeenCalledTimes(2)
    expect(mockPost).toHaveBeenLastCalledWith('/payments/confirm', {
      preferenceId: 'pref-123',
    })
  })

  it('clears the stored checkout reservation on mount', () => {
    setSearchParams({ preference_id: 'pref-123' })
    mockPost.mockImplementation(() => new Promise(() => {}))
    sessionStorage.setItem(
      CHECKOUT_RESERVATION_KEY,
      JSON.stringify({ signature: 'event-1|tt-1|2', id: 'reservation-1' })
    )

    renderWithQueryClient(<CheckoutSuccess />)

    expect(sessionStorage.getItem(CHECKOUT_RESERVATION_KEY)).toBeNull()
  })

  it('re-checks automatically and confirms without user action', async () => {
    vi.useFakeTimers()
    try {
      setSearchParams({ preference_id: 'pref-123' })
      mockPost
        .mockResolvedValueOnce({ data: { status: 'pending', reason: 'payment_pending' } })
        .mockResolvedValueOnce({ data: { status: 'confirmed' } })

      renderWithQueryClient(<CheckoutSuccess />)

      // Wait for the pending feedback (unique text: the heading is shared with
      // the initial confirming state, which would resolve this wait too early).
      await vi.waitFor(() => {
        expect(
          screen.getByText(/no cierres esta página ni pagues de nuevo/i)
        ).toBeInTheDocument()
      })

      await act(async () => {
        await vi.advanceTimersByTimeAsync(AUTO_RECHECK_DELAY_MS)
      })

      await vi.waitFor(() => {
        expect(screen.getByRole('heading', { name: /pago confirmado/i })).toBeInTheDocument()
      })
      expect(mockPost).toHaveBeenCalledTimes(2)
    } finally {
      vi.useRealTimers()
    }
  })

  it('stops auto re-checking after the attempt budget and offers manual verification', async () => {
    vi.useFakeTimers()
    try {
      setSearchParams({ preference_id: 'pref-123' })
      mockPost.mockResolvedValue({ data: { status: 'pending', reason: 'payment_pending' } })

      renderWithQueryClient(<CheckoutSuccess />)

      // Wait for the pending feedback (unique text: the heading is shared with
      // the initial confirming state, which would resolve this wait too early).
      await vi.waitFor(() => {
        expect(
          screen.getByText(/no cierres esta página ni pagues de nuevo/i)
        ).toBeInTheDocument()
      })

      for (let i = 0; i < MAX_AUTO_RECHECKS + 2; i += 1) {
        await act(async () => {
          await vi.advanceTimersByTimeAsync(AUTO_RECHECK_DELAY_MS)
        })
      }

      await vi.waitFor(() => {
        expect(screen.getByRole('heading', { name: /pago pendiente/i })).toBeInTheDocument()
      })

      // The auto re-check budget is capped: no more calls than the initial
      // check plus MAX_AUTO_RECHECKS, even though extra time keeps passing.
      const callsWhenExhausted = mockPost.mock.calls.length
      expect(callsWhenExhausted).toBeLessThanOrEqual(MAX_AUTO_RECHECKS + 1)

      await vi.advanceTimersByTimeAsync(AUTO_RECHECK_DELAY_MS * 5)
      expect(mockPost).toHaveBeenCalledTimes(callsWhenExhausted)

      expect(screen.getByRole('button', { name: /verificar de nuevo/i })).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  })
})
