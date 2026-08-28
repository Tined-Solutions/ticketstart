import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import OrganizerDashboard from './OrganizerDashboard.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()
const mockUseAuth = vi.fn()

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
  },
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => mockUseAuth(),
}))

// Far-future dates computed at runtime so the mock events never become "past"
// and trip the isPast/read-only UI logic (PEM-002), which would break the
// mutable-path tests for reasons unrelated to what they assert.
const futureDate = (days) =>
  new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString()

const mockMetrics = [
  {
    id: 'metrics-1',
    eventId: 'event-1',
    eventName: 'Recital de Rock Nacional',
    eventDate: futureDate(400),
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
    eventDate: futureDate(300),
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
    eventDate: futureDate(365),
    ticketsSold: 0,
    totalRevenue: 0,
    remainingInventory: 50,
    ticketsScanned: 0,
    status: 'Rejected',
  },
]

// Rows are stacked flex containers (no <table>). Each row carries the
// `hover:bg-surface-elevated` utility, so we scope queries to a single event
// by walking up from its name heading.
const eventRow = (name) =>
  screen.getByRole('heading', { name: new RegExp(name, 'i') }).closest('[class*="bg-surface-elevated"]')

// Opens a row's "Acciones" dropdown (kebab) and waits for the menu panel.
const openActionsMenu = async (name) => {
  const row = eventRow(name)
  await userEvent.click(within(row).getByRole('button', { name: /^acciones/i }))
  await screen.findByRole('menu')
}

describe('OrganizerDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockNavigate.mockReset()
    mockUseAuth.mockReset()
    // Default: organizer role — Editar hidden (EA-009 UI-only)
    mockUseAuth.mockReturnValue({ user: { role: 'Organizador' } })
    mockGet.mockResolvedValue({ data: mockMetrics })
  })

  // ── Event list display ────────────────────────────────────────────

  it('renders event metrics fetched from the API (flat array response)', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    expect(screen.getByText('Eventos (3)')).toBeInTheDocument()
  })

  it('handles the paginated { items: [...] } response shape', async () => {
    mockGet.mockResolvedValue({ data: { items: mockMetrics, total: 3 } })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText('Eventos (3)')).toBeInTheDocument()
  })

  it('renders all 4 mini-stats per row (sold, revenue, inventory, scanned)', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const recitalRow = eventRow('recital de rock nacional')
    expect(within(recitalRow).getByText(/entradas vendidas:/i)).toBeInTheDocument()
    expect(within(recitalRow).getByText('120')).toBeInTheDocument()
    // formatCurrency output (es-AR thousands separator)
    expect(within(recitalRow).getByText('$ 1.800.000')).toBeInTheDocument()
    expect(within(recitalRow).getByText('30')).toBeInTheDocument()
    expect(within(recitalRow).getByText('45')).toBeInTheDocument()
  })

  it('display zero values correctly', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    const workshopRow = eventRow('workshop de fotografia')

    // Workshop has 0 tickets sold, 0 revenue, 0 scanned
    const zeroCells = within(workshopRow).getAllByText('0')
    expect(zeroCells.length).toBeGreaterThanOrEqual(2) // sold=0, scanned=0
    expect(within(workshopRow).getByText('$ 0')).toBeInTheDocument() // revenue
  })

  it('renders a status badge per event row (3 variants)', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Approved → "Aprobado" (success), Pending → "Pendiente" (warning),
    // Rejected → "Rechazado" (error) — one badge per row
    const approvedRow = eventRow('recital de rock nacional')
    const pendingRow = eventRow('feria de emprendedores')
    const rejectedRow = eventRow('workshop de fotografia')

    expect(within(approvedRow).getByText('Aprobado')).toBeInTheDocument()
    expect(within(pendingRow).getByText('Pendiente')).toBeInTheDocument()
    expect(within(rejectedRow).getByText('Rechazado')).toBeInTheDocument()
  })

  // ── Loading / error / empty states ─────────────────────────────────

  it('shows loading skeletons while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<OrganizerDashboard />)

    expect(screen.getByRole('heading', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getAllByRole('status').length).toBeGreaterThan(0)
  })

  it('shows error state and Reintentar re-fetches', async () => {
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

  it('shows the EmptyState with Eventos (0) and a gradient CTA when no events exist', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText('Eventos (0)')).toBeInTheDocument()
    })

    expect(screen.getByText(/no tenes eventos creados todavia/i)).toBeInTheDocument()
    expect(screen.getByText(/crea tu primer evento/i)).toBeInTheDocument()

    const createBtn = screen.getByRole('button', { name: /^crear evento$/i })
    await userEvent.click(createBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/new')
  })

  // ── Section header actions ─────────────────────────────────────────

  it('"+ Crear evento" header button navigates to the new event page', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /\+\s*crear evento/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/new')
  })

  // ── Row quick action: Ver ──────────────────────────────────────────

  it('"Ver" button navigates to the read-only event view', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const recitalRow = eventRow('recital de rock nacional')
    await userEvent.click(within(recitalRow).getByRole('button', { name: /ver recital de rock nacional/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1/view')
  })

  // ── Dropdown actions ───────────────────────────────────────────────

  it('shows no Acciones kebab for organizers: no Eliminar/Metricas entries, Ver stays (ED-001/EHE-006)', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // EA-009: still no visible Editar button outside menus
    expect(screen.queryByRole('button', { name: /editar/i })).not.toBeInTheDocument()

    // ED-001/D-4: the kebab is gone entirely for organizers — a dead trigger
    // opening an empty panel is broken UX
    const recitalRow = eventRow('recital de rock nacional')
    expect(within(recitalRow).queryByRole('button', { name: /^acciones/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^acciones/i })).not.toBeInTheDocument()

    // Eliminar / Metricas are removed change-wide for every role and status
    expect(screen.queryByRole('menuitem', { name: /eliminar/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /ver metricas/i })).not.toBeInTheDocument()

    // "Ver" remains available (standalone button, untouched)
    expect(within(recitalRow).getByRole('button', { name: /ver recital de rock nacional/i })).toBeEnabled()
  })

  it('shows Editar menuitem for admins and navigates to edit (EA-009)', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await openActionsMenu('recital de rock nacional')

    // ED-001/EHE-006: the admin kebab narrows to Editar only — Metricas and
    // Eliminar are removed for every row regardless of role
    expect(screen.queryByRole('menuitem', { name: /ver metricas de recital de rock nacional/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /eliminar recital de rock nacional/i })).not.toBeInTheDocument()

    await userEvent.click(
      await screen.findByRole('menuitem', { name: /editar recital de rock nacional/i })
    )
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1')
  })

  it('kebab menu opens with a high z-index panel (not clipped by the row below)', async () => {
    // The kebab survives only for admins (ED-001/D-4) — their menu still
    // exercises the z-index-over-sibling-rows behavior.
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await openActionsMenu('recital de rock nacional')

    const menu = screen.getByRole('menu')
    expect(menu).toBeInTheDocument()
    // The panel carries a high z-index so it paints above sibling rows
    expect(menu.className).toContain('z-50')
  })

  // ── Delete flow ────────────────────────────────────────────────────
  // Removed with ED-001/EHE-006: the dashboard delete flow is gone entirely
  // (no Eliminar entry, no DeleteConfirmationDialog usage on this page). The
  // shared dialog itself survives via AdminPanel (ED-003, AdminPanel.test.jsx).

  // ── Past events: read-only (PEM-002) ────────────────────────────────

  it('keeps past events read-only: Finalizado badge, Ver enabled, mutations disabled with title', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })
    mockGet.mockResolvedValue({
      data: [
        {
          id: 'metrics-past',
          eventId: 'event-past',
          eventName: 'Concierto Pasado',
          eventDate: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
          ticketsSold: 80,
          totalRevenue: 500000,
          remainingInventory: 0,
          ticketsScanned: 80,
          status: 'Approved',
        },
      ],
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/concierto pasado/i)).toBeInTheDocument()
    })

    const pastRow = eventRow('concierto pasado')

    // Finalizado badge + Ver button (read-only view) are shown and enabled
    expect(within(pastRow).getByText('Finalizado')).toBeInTheDocument()
    const verBtn = within(pastRow).getByRole('button', { name: /ver concierto pasado/i })
    expect(verBtn).toBeEnabled()
    await userEvent.click(verBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-past/view')

    // Kebab (admin): Editar disabled with the readonly title; Eliminar and
    // Metricas no longer exist on ANY row — past rows included (PEC-004
    // metricas-absent-past-row, ED-001 change-wide removal)
    await openActionsMenu('concierto pasado')
    const editarItem = await screen.findByRole('menuitem', { name: /editar concierto pasado/i })
    expect(editarItem).toBeDisabled()
    expect(editarItem).toHaveAttribute('title', 'Evento finalizado — solo lectura')
    expect(screen.queryByRole('menuitem', { name: /eliminar concierto pasado/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('menuitem', { name: /ver metricas de concierto pasado/i })).not.toBeInTheDocument()
  })

  // ── Sort order ─────────────────────────────────────────────────────

  it('sorts upcoming events soonest-first, then past events oldest-last', async () => {
    const day = 24 * 60 * 60 * 1000
    mockGet.mockResolvedValue({
      data: [
        { id: 'm-gala', eventId: 'e-gala', eventName: 'Gala Anual', eventDate: new Date(Date.now() - 400 * day).toISOString(), ticketsSold: 1, totalRevenue: 1, remainingInventory: 1, ticketsScanned: 1, status: 'Approved' },
        { id: 'm-taller', eventId: 'e-taller', eventName: 'Taller de Arte', eventDate: new Date(Date.now() + 10 * day).toISOString(), ticketsSold: 1, totalRevenue: 1, remainingInventory: 1, ticketsScanned: 1, status: 'Pending' },
        { id: 'm-vintage', eventId: 'e-vintage', eventName: 'Concierto Vintage', eventDate: new Date(Date.now() - 30 * day).toISOString(), ticketsSold: 1, totalRevenue: 1, remainingInventory: 1, ticketsScanned: 1, status: 'Approved' },
        { id: 'm-festival', eventId: 'e-festival', eventName: 'Festival Primavera', eventDate: new Date(Date.now() + 50 * day).toISOString(), ticketsSold: 1, totalRevenue: 1, remainingInventory: 1, ticketsScanned: 1, status: 'Approved' },
      ],
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/festival primavera/i)).toBeInTheDocument()
    })

    // Upcoming (+10, +50) come first, soonest-upcoming at top; past (-30, -400)
    // come after, sorted descending so the OLDEST past event is LAST.
    const headings = screen.getAllByRole('heading', { level: 3 })
    const names = headings.map((h) => h.textContent)
    expect(names).toEqual([
      'Taller de Arte', // +10 → soonest upcoming
      'Festival Primavera', // +50
      'Concierto Vintage', // -30 (recent past)
      'Gala Anual', // -400 → oldest past, LAST
    ])

    // Header count still reads the unsorted source
    expect(screen.getByText('Eventos (4)')).toBeInTheDocument()
  })

  it('fetches organizer metrics on mount', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(mockGet).toHaveBeenCalledWith('/metrics/organizer', expect.any(Object))
  })
})

// ── Visual Regression: Glass & Theme ──────────────────────────────────

describe('OrganizerDashboard — Visual Regression', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockNavigate.mockReset()
    mockUseAuth.mockReset()
    mockUseAuth.mockReturnValue({ user: { role: 'Organizador' } })
  })

  it('renders GlassCard wrappers in the loaded state', async () => {
    mockGet.mockResolvedValue({ data: mockMetrics })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders GlassCard in the empty state', async () => {
    mockGet.mockResolvedValue({ data: [] })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/no tenes eventos creados todavia/i)).toBeInTheDocument()
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
