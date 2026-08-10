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
      refunded: false,
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
      refunded: true,
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

  it('renders purchases with raw buyer data and refunded badge', async () => {
    renderPage()

    // Raw buyer + DNI (Admin-only surface: the admin needs to identify the buyer to refund)
    await waitFor(() => {
      expect(screen.getByText('juan.perez@gmail.com')).toBeInTheDocument()
    })
    expect(screen.getByText('31234561')).toBeInTheDocument()

    // Refunded row shows the Refunded badge, approved row shows Confirmada
    expect(screen.getByText('Reembolsada')).toBeInTheDocument()
    expect(screen.getByText('Confirmada')).toBeInTheDocument()

    // Per-event totalRefunded is displayed
    expect(screen.getByText(/reembolsado: \$ 150/i)).toBeInTheDocument()

    // The refunded row must NOT offer a refund action
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

  it('refund confirm success invalidates the query and shows the updated state', async () => {
    // First GET returns the pre-refund list; after invalidation the refetch returns
    // the purchase as refunded (res-1) with totalRefunded updated.
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
          p.reservationId === 'res-1' ? { ...p, refunded: true } : p
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

    await userEvent.click(within(dialog).getByRole('button', { name: 'Reembolsar' }))

    // POST sent to the refund endpoint
    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-1/purchases/res-1/refund')
    })

    // Query invalidation → refetch → the row now shows Reembolsada and totalRefunded grew
    await waitFor(() => {
      expect(screen.getByText(/reembolsado: \$ 350/i)).toBeInTheDocument()
    })
    expect(screen.getAllByText('Reembolsada')).toHaveLength(2)
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
    expect(screen.queryByText('Reembolsada')).toBeInTheDocument()
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
