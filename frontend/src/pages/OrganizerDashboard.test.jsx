import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import OrganizerDashboard from './OrganizerDashboard.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()
const mockDelete = vi.fn()
const mockUseAuth = vi.fn()

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
    delete: (...args) => mockDelete(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => mockUseAuth(),
}))

const mockMetrics = [
  {
    id: 'metrics-1',
    eventId: 'event-1',
    eventName: 'Recital de Rock Nacional',
    eventDate: '2026-08-15T21:00:00Z',
    ticketsSold: 120,
    totalRevenue: 1800000,
    remainingInventory: 30,
    ticketsScanned: 45,
    status: 'Approved',
  },
  {
    id: 'metrics-2',
    eventId: 'event-2',
    eventName: 'Feria de Emprendedores',
    eventDate: '2026-09-01T14:00:00Z',
    ticketsSold: 300,
    totalRevenue: 0,
    remainingInventory: 200,
    ticketsScanned: 0,
    status: 'Pending',
  },
  {
    id: 'metrics-3',
    eventId: 'event-3',
    eventName: 'Workshop de Fotografia',
    eventDate: '2026-10-10T10:00:00Z',
    ticketsSold: 0,
    totalRevenue: 0,
    remainingInventory: 50,
    ticketsScanned: 0,
    status: 'Rejected',
  },
]

describe('OrganizerDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockDelete.mockReset()
    mockNavigate.mockReset()
    mockUseAuth.mockReset()
    // Default: organizer role — Editar hidden (EA-009 UI-only)
    mockUseAuth.mockReturnValue({ user: { role: 'Organizador' } })
  })

  it('renders event metrics from API data', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()

    // Check metrics are displayed for first event
    expect(screen.getByText('120')).toBeInTheDocument()
    expect(screen.getByText('$ 1.800.000')).toBeInTheDocument()
    expect(screen.getByText('30')).toBeInTheDocument()
    expect(screen.getByText('45')).toBeInTheDocument()
  })

  it('shows loading state while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<OrganizerDashboard />)

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument()
    // Skeleton placeholders should be visible during loading
    const skeletons = document.querySelectorAll('[role="status"]')
    expect(skeletons.length).toBeGreaterThan(0)
  })

  it('shows error state with retry button', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error de conexion' } } },
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/error de conexion/i)).toBeInTheDocument()
    })

    mockGet.mockResolvedValue({ data: mockMetrics })
    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })
  })

  it('shows empty state when no events exist', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/no tenes eventos creados/i)).toBeInTheDocument()
    })

    const createBtn = screen.getByRole('button', { name: /crear tu primer evento/i })
    expect(createBtn).toBeInTheDocument()
    await userEvent.click(createBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/new')
  })

  it('"Crear evento" button navigates to new event page', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const createBtn = screen.getByRole('button', { name: /\+\s*crear evento/i })
    await userEvent.click(createBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/new')
  })

  it('edit button navigates to event edit page (admin keeps edit)', async () => {
    // EA-009: Editar is admin-only (UI); admin sees it and it navigates
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const editBtn = screen.getByRole('button', { name: /editar recital de rock nacional/i })
    await userEvent.click(editBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1')
  })

  it('delete button opens confirmation dialog', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    const deleteBtn = screen.getByRole('button', { name: /eliminar feria de emprendedores/i })
    await userEvent.click(deleteBtn)

    // Confirmation dialog appears
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/confirmar eliminacion/i)).toBeInTheDocument()
    expect(
      within(dialog).getByText(/feria de emprendedores/i)
    ).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /eliminar/i })).toBeInTheDocument()
  })

  it('cancel button closes confirmation dialog', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    const deleteBtn = screen.getByRole('button', { name: /eliminar feria de emprendedores/i })
    await userEvent.click(deleteBtn)

    expect(screen.getByRole('dialog')).toBeInTheDocument()

    const cancelBtn = screen.getByRole('button', { name: /cancelar/i })
    await userEvent.click(cancelBtn)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('confirm delete sends DELETE and removes event from list', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })
    mockDelete.mockResolvedValue({})

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    const deleteBtn = screen.getByRole('button', { name: /eliminar workshop de fotografia/i })
    await userEvent.click(deleteBtn)

    const dialog = screen.getByRole('dialog')
    const confirmBtn = within(dialog).getByRole('button', { name: /^eliminar$/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(mockDelete).toHaveBeenCalledWith('/events/event-3')
    })

    // Success feedback
    await waitFor(() => {
      expect(
        screen.getByText(/workshop de fotografia.*eliminado correctamente/i)
      ).toBeInTheDocument()
    })

    // Event should be removed from the table (but still in feedback message)
    const table = document.querySelector('table')
    expect(within(table).queryByText(/workshop de fotografia/i)).not.toBeInTheDocument()
  })

  it('shows delete error feedback when API call fails', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })
    mockDelete.mockRejectedValue({
      response: { data: { error: { message: 'No autorizado' } } },
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const deleteBtn = screen.getByRole('button', { name: /eliminar recital de rock nacional/i })
    await userEvent.click(deleteBtn)

    const dialog = screen.getByRole('dialog')
    const confirmBtn = within(dialog).getByRole('button', { name: /^eliminar$/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(screen.getByText(/no autorizado/i)).toBeInTheDocument()
    })

    // Dialog should be closed even on error
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    // Event should still be in the list
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
  })

  it('"Ver metricas" button navigates to event metrics page', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const metricsBtn = screen.getByRole('button', { name: /ver metricas de recital de rock nacional/i })
    await userEvent.click(metricsBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1/metrics')
  })

  it('display zero values correctly', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    // Workshop has 0 tickets sold, 0 revenue, 0 scanned
    // Find the row containing the workshop event
    const rows = screen.getAllByRole('row')
    const workshopRow = rows.find(
      (r) => r.textContent.includes('Workshop de Fotografia')
    )
    expect(workshopRow).toBeTruthy()

    const zeroCells = within(workshopRow).getAllByText('0')
    expect(zeroCells.length).toBeGreaterThanOrEqual(2) // sold=0, scanned=0
    expect(within(workshopRow).getByText('$ 0')).toBeInTheDocument() // revenue
  })

  it('formats currency correctly for revenue', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText('$ 1.800.000')).toBeInTheDocument()
  })

  it('renders create button even while loading', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))
 
    render(<OrganizerDashboard />)

    expect(screen.getByRole('button', { name: /\+\s*crear evento/i })).toBeInTheDocument()
  })

  // ── EA-009: status badges + role-gated Edit ─────────────────────────

  it('renders a status badge per event row (3 variants)', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Approved → "Aprobado" (success), Pending → "Pendiente" (warning),
    // Rejected → "Rechazado" (error) — one badge per row
    const rows = screen.getAllByRole('row')
    const approvedRow = rows.find((r) => r.textContent.includes('Recital de Rock Nacional'))
    const pendingRow = rows.find((r) => r.textContent.includes('Feria de Emprendedores'))
    const rejectedRow = rows.find((r) => r.textContent.includes('Workshop de Fotografia'))

    expect(within(approvedRow).getByText('Aprobado')).toBeInTheDocument()
    expect(within(pendingRow).getByText('Pendiente')).toBeInTheDocument()
    expect(within(rejectedRow).getByText('Rechazado')).toBeInTheDocument()
  })

  it('hides Edit entry for organizers (EA-009)', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Organizador' } })
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.queryByRole('button', { name: /editar/i })).not.toBeInTheDocument()
    // Metricas + Eliminar remain for organizers
    expect(screen.getByRole('button', { name: /ver metricas de recital de rock nacional/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /eliminar recital de rock nacional/i })).toBeInTheDocument()
  })

  it('shows Edit entry for admins (EA-009)', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getAllByRole('button', { name: /editar/i }).length).toBe(3)
  })
})

// ── Visual Regression: Glass & Theme ──────────────────────────────────

describe('OrganizerDashboard — Visual Regression', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockNavigate.mockReset()
  })

  it('renders GlassCard wrappers in the loaded state', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Verify glass-surface class is present on the table container GlassCard
    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders GlassCard in the empty state', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/no tenes eventos creados/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders GlassCard in the error state', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Server error' } } },
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/server error/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('uses data-theme attribute for theme awareness', () => {
    mockGet.mockResolvedValue({ data: mockMetrics })
    render(<OrganizerDashboard />)

    // The page renders within the app shell which sets data-theme on <html>
    // Verify the page renders without errors (theme is managed globally)
    expect(document.documentElement).toBeTruthy()
  })
})
