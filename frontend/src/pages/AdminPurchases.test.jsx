import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import AdminPurchases from './AdminPurchases.jsx'
import App from '../App.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()
const mockPost = vi.fn()
let mockEventId = 'event-1'

vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useParams: () => ({ id: mockEventId }),
  }
})

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
    post: (...args) => mockPost(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: vi.fn(),
}))

vi.mock('../hooks/useTheme.jsx', async (importOriginal) => {
  const original = await importOriginal()
  return {
    ...original,
    useTheme: vi.fn(),
  }
})

import { useAuth } from '../context/auth.js'
import { useTheme } from '../hooks/useTheme.jsx'

const mockPurchases = {
  eventId: 'event-1',
  eventName: 'Recital de Rock Nacional',
  purchases: [
    {
      reservationId: 'res-1',
      purchaserEmail: 'juan.perez@gmail.com',
      purchaserDni: '31234561',
      ticketType: 'General',
      quantity: 2,
      amount: 200,
      purchasedAt: '2026-07-01T10:00:00Z',
      refundedQuantity: 0,
      refundedAmount: 0,
      linkUnverified: false,
    },
    {
      reservationId: 'res-2',
      purchaserEmail: 'maria@test.com',
      purchaserDni: '25123458',
      ticketType: 'VIP',
      quantity: 1,
      amount: 150,
      purchasedAt: '2026-07-02T10:00:00Z',
      refundedQuantity: 1,
      refundedAmount: 150,
      linkUnverified: false,
    },
  ],
  totalRefunded: 150,
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AdminPurchases />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('AdminPurchases', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockPost.mockReset()
    mockNavigate.mockReset()
    mockEventId = 'event-1'
    useAuth.mockReturnValue({ user: null, isAuthenticated: false })
    useTheme.mockReturnValue({ theme: 'dark', setTheme: vi.fn(), toggle: vi.fn() })

    mockGet.mockImplementation((url) => {
      if (url === '/admin/events/event-1/purchases') {
        return Promise.resolve({ data: mockPurchases })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
  })

  it('renders purchases with raw buyer data and refunded badge variants', async () => {
    renderPage()

    // Raw buyer + DNI (Admin-only surface: the admin needs to identify the buyer to refund)
    await waitFor(() => {
      expect(screen.getByText('juan.perez@gmail.com')).toBeInTheDocument()
    })
    expect(screen.getByText('31234561')).toBeInTheDocument()

    // APR-010 badge variants: fully refunded (res-2, 1/1) → error "1 de 1 reembolsadas";
    // not refunded (res-1, 0/2) → success "Confirmada"
    expect(screen.getByText('1 de 1 reembolsadas')).toBeInTheDocument()
    expect(screen.getByText('Confirmada')).toBeInTheDocument()

    // Per-event totalRefunded is displayed
    expect(screen.getByText(/reembolsado: \$ 150/i)).toBeInTheDocument()

    // The fully-refunded row (refundedQuantity >= quantity) must NOT offer a refund action
    const refundedRow = screen.getAllByRole('row').find((r) => r.textContent.includes('maria@test.com'))
    expect(within(refundedRow).queryByRole('button')).toBeDisabled()
  })

  it('shows empty state when the event has no confirmed purchases', async () => {
    mockGet.mockResolvedValue({
      data: { eventId: 'event-1', eventName: 'Vacio', purchases: [], totalRefunded: 0 },
    })

    renderPage()

    await waitFor(() => {
      expect(screen.getByText(/no hay compras confirmadas/i)).toBeInTheDocument()
    })
  })

  it('shows error state when the purchases fetch fails', async () => {
    mockGet.mockRejectedValue({ response: { data: { error: 'Event not found' } } })

    renderPage()

    expect(await screen.findByRole('alert')).toHaveTextContent(/event not found/i)
  })

  it('partial refund via quantity selector posts {quantity, amount} and updates the row', async () => {
    // First GET returns the pre-refund list; after invalidation the refetch returns
    // res-1 with refundedQuantity 2 (fully refunded) and totalRefunded updated.
    let callCount = 0
    mockGet.mockImplementation((url) => {
      if (url !== '/admin/events/event-1/purchases') return Promise.reject(new Error('Unknown endpoint'))
      callCount += 1
      if (callCount === 1) {
        return Promise.resolve({ data: mockPurchases })
      }
      const updated = {
        ...mockPurchases,
        purchases: mockPurchases.purchases.map((p) =>
          p.reservationId === 'res-1' ? { ...p, refundedQuantity: 2, refundedAmount: 200 } : p
        ),
        totalRefunded: 350,
      }
      return Promise.resolve({ data: updated })
    })
    mockPost.mockResolvedValue({ data: { message: 'Purchase refunded successfully' } })

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('juan.perez@gmail.com')).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /reembolsar compra de juan\.perez@gmail\.com/i }))

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/confirmar reembolso/i)).toBeInTheDocument()

    // Quantity selector: number input 1..activeRemaining (activeRemaining = 2 - 0 = 2)
    const quantityInput = within(dialog).getByLabelText(/cantidad a reembolsar/i)
    expect(quantityInput).toHaveValue(1)
    expect(quantityInput).toHaveAttribute('min', '1')
    expect(quantityInput).toHaveAttribute('max', '2')

    // Amount input: prefilled to K × unit price (1 × 100 = 100), step 0.01
    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    expect(amountInput).toHaveAttribute('step', '0.01')
    expect(amountInput).toHaveValue(100)

    // Live preview with cents: unitPrice = amount / quantity = 200 / 2 = 100
    expect(within(dialog).getByText(/reembolsar 1 × \$ 100,00 = \$ 100,00/i)).toBeInTheDocument()

    // Select K=2 → the amount prefill recomputes to 2 × 100 = 200
    await userEvent.clear(quantityInput)
    await userEvent.type(quantityInput, '2')
    expect(amountInput).toHaveValue(200)
    expect(within(dialog).getByText(/reembolsar 2 × \$ 100,00 = \$ 200,00/i)).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))

    // POST sent to the refund endpoint WITH the {quantity, amount} body (APR-010)
    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/purchases/res-1/refund', { quantity: 2, amount: 200 })
    })

    // Query invalidation → refetch → the row now shows the new refundedQuantity
    // ("2 de 2 reembolsadas") and totalRefunded grew
    await waitFor(() => {
      expect(screen.getByText(/reembolsado: \$ 350/i)).toBeInTheDocument()
    })
    const updatedRow = screen.getAllByRole('row').find((r) => r.textContent.includes('juan.perez@gmail.com'))
    expect(within(updatedRow).getByText('2 de 2 reembolsadas')).toBeInTheDocument()
    expect(within(updatedRow).getByRole('button')).toBeDisabled()
    expect(mockGet.mock.calls.length).toBeGreaterThanOrEqual(2)
  })

  it('refund failure shows the error and leaves the list unchanged', async () => {
    mockPost.mockRejectedValue({
      response: { data: { error: 'Cannot refund a purchase with used tickets' } },
    })

    renderPage()

    await waitFor(() => {
      expect(screen.getByText('juan.perez@gmail.com')).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /reembolsar compra de juan\.perez@gmail\.com/i }))

    const dialog = screen.getByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))

    // APR-010: error is shown, no refetch happened, row state unchanged
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(/cannot refund a purchase with used tickets/i)

    expect(mockGet).toHaveBeenCalledTimes(1)
    expect(screen.getByText('Confirmada')).toBeInTheDocument()
    expect(screen.getByText('1 de 1 reembolsadas')).toBeInTheDocument()
  })
})

describe('AdminPurchases — refund dialog amount (APR-010/D4)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockPost.mockReset()
    mockEventId = 'event-1'
    useAuth.mockReturnValue({ user: { role: 'Admin' }, isAuthenticated: true })
    useTheme.mockReturnValue({ theme: 'dark', setTheme: vi.fn(), toggle: vi.fn() })
    mockPost.mockResolvedValue({ data: { message: 'Purchase refunded successfully' } })
  })

  function seedPurchases(purchases) {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events/event-1/purchases') {
        return Promise.resolve({ data: { eventId: 'event-1', eventName: 'Recital', purchases, totalRefunded: 0 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
  }

  async function openDialog(purchases = mockPurchases.purchases) {
    seedPurchases(purchases)
    renderPage()
    await waitFor(() => {
      expect(screen.getByText('juan.perez@gmail.com')).toBeInTheDocument()
    })
    await userEvent.click(screen.getByRole('button', { name: /reembolsar compra de juan\.perez@gmail\.com/i }))
    return screen.getByRole('dialog')
  }

  it('prefills the amount input to K × unit price and recomputes on quantity change (3 × 100 → 300)', async () => {
    const purchase = { ...mockPurchases.purchases[0], quantity: 3, amount: 300 }
    const dialog = await openDialog([purchase])

    // Prefill for the default quantity 1: 1 × unit price 100 = 100
    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    expect(amountInput).toHaveValue(100)

    // Selecting K=3 recomputes the prefill to 3 × 100 = 300
    const quantityInput = within(dialog).getByLabelText(/cantidad a reembolsar/i)
    await userEvent.clear(quantityInput)
    await userEvent.type(quantityInput, '3')
    expect(amountInput).toHaveValue(300)

    // Confirming WITHOUT edits posts the prefilled amount (300) — untouched dialog = full-price behavior
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))
    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/purchases/res-1/refund', { quantity: 3, amount: 300 })
    })
  })

  it('25% helper converts client-side to 50 via integer-cents math and never posts a percent', async () => {
    const dialog = await openDialog()

    const quantityInput = within(dialog).getByLabelText(/cantidad a reembolsar/i)
    await userEvent.clear(quantityInput)
    await userEvent.type(quantityInput, '2')

    // One-shot amount write (D1): 25% of the 200 cap = 50
    await userEvent.click(within(dialog).getByRole('button', { name: /aplicar 25%/i }))
    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    expect(amountInput).toHaveValue(50)

    // Live preview shows cents (D2)
    expect(within(dialog).getByText(/\$ 50,00/)).toBeInTheDocument()

    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))

    // The post body carries {quantity, amount} EXACTLY — never a percent key
    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/purchases/res-1/refund', { quantity: 2, amount: 50 })
    })
    const body = mockPost.mock.calls[0][1]
    expect(Object.keys(body)).toEqual(['quantity', 'amount'])
  })

  it('100% helper fills the full cap amount', async () => {
    const dialog = await openDialog()

    await userEvent.click(within(dialog).getByRole('button', { name: /aplicar 100%/i }))
    expect(within(dialog).getByLabelText(/monto a reembolsar/i)).toHaveValue(100)
  })

  it('amount ≤ 0 blocks submit with an inline error and no mutation', async () => {
    const dialog = await openDialog()

    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)

    // Inline validation error (role="alert") appears, submit blocked
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(/mayor a cero/i)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('amount above the cap blocks submit with an inline error and no mutation', async () => {
    const dialog = await openDialog()

    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)
    await userEvent.type(amountInput, '100.01')

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(/no puede superar/i)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('amounts with more than 2 decimals are flagged inline (mirrors D3 reject, never round)', async () => {
    const dialog = await openDialog()

    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)
    await userEvent.type(amountInput, '33.333')

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(/2 decimales/i)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('re-validates the amount against the new cap when quantity changes while dirty', async () => {
    const dialog = await openDialog()

    const quantityInput = within(dialog).getByLabelText(/cantidad a reembolsar/i)
    await userEvent.clear(quantityInput)
    await userEvent.type(quantityInput, '2')

    // Dirty the amount: 150 is valid for K=2 (cap 200)
    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)
    await userEvent.type(amountInput, '150')
    expect(within(dialog).queryByRole('alert')).not.toBeInTheDocument()

    // Back to K=1 → the new cap is 100 < 150 → inline error appears, submit blocked
    await userEvent.clear(quantityInput)
    await userEvent.type(quantityInput, '1')
    expect(await within(dialog).findByRole('alert')).toHaveTextContent(/no puede superar/i)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))
    expect(mockPost).not.toHaveBeenCalled()
  })

  it('shows a cents preview for cent-exact amounts', async () => {
    const dialog = await openDialog()

    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)
    await userEvent.type(amountInput, '50.5')

    expect(within(dialog).getByText(/\$ 50,50/)).toBeInTheDocument()
  })

  it('resets the amount state on remount (cancel → reopen shows the prefill again)', async () => {
    const dialog = await openDialog()

    const amountInput = within(dialog).getByLabelText(/monto a reembolsar/i)
    await userEvent.clear(amountInput)
    await userEvent.type(amountInput, '33')

    // Cancel unmounts the dialog; reopening must show a FRESH prefill (100)
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancelar' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /reembolsar compra de juan\.perez@gmail\.com/i }))
    const reopened = screen.getByRole('dialog')
    expect(within(reopened).getByLabelText(/monto a reembolsar/i)).toHaveValue(100)
    expect(within(reopened).queryByRole('alert')).not.toBeInTheDocument()
  })
})

describe('AdminPurchases — route guard (APR-010)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useTheme.mockReturnValue({ theme: 'dark', setTheme: vi.fn(), toggle: vi.fn() })
  })

  it('denies access to non-admin users on the purchases route', async () => {
    useAuth.mockReturnValue({ user: { role: 'Organizador' }, isAuthenticated: true })

    render(
      <MemoryRouter initialEntries={['/admin/events/event-1/purchases']}>
        <App />
      </MemoryRouter>
    )

    expect(await screen.findByText(/403/i)).toBeInTheDocument()
    expect(screen.getByText(/no tenes permisos para acceder/i)).toBeInTheDocument()
  })

  it('allows access for admin users on the purchases route', async () => {
    useAuth.mockReturnValue({ user: { role: 'Admin' }, isAuthenticated: true })
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events/event-1/purchases') {
        return Promise.resolve({ data: mockPurchases })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/admin/events/event-1/purchases']}>
          <App />
        </MemoryRouter>
      </QueryClientProvider>
    )

    expect(await screen.findByText(/compras del evento/i)).toBeInTheDocument()
  })
})
