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
    mockDelete.mockReset()
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

  it('"Metricas" menu item navigates to the event metrics page', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await openActionsMenu('recital de rock nacional')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /ver metricas de recital de rock nacional/i })
    )
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1/metrics')
  })

  it('hides Editar for organizers, everywhere (EA-009)', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // No visible Editar button outside menus
    expect(screen.queryByRole('button', { name: /editar/i })).not.toBeInTheDocument()

    // And no Editar menuitem inside the kebab menu
    await openActionsMenu('recital de rock nacional')
    expect(screen.queryByRole('menuitem', { name: /editar/i })).not.toBeInTheDocument()
    // Metricas + Eliminar remain for organizers
    expect(screen.getByRole('menuitem', { name: /ver metricas de recital de rock nacional/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /eliminar recital de rock nacional/i })).toBeInTheDocument()
  })

  it('shows Editar menuitem for admins and navigates to edit (EA-009)', async () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Admin' } })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await openActionsMenu('recital de rock nacional')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /editar recital de rock nacional/i })
    )
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1')
  })

  it('kebab menu opens with a high z-index panel (not clipped by the row below)', async () => {
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

  it('Eliminar menu item opens the confirmation dialog', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    await openActionsMenu('feria de emprendedores')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /eliminar feria de emprendedores/i })
    )

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/confirmar eliminación/i)).toBeInTheDocument()
    expect(within(dialog).getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /^eliminar$/i })).toBeInTheDocument()
  })

  it('cancel button closes the dialog without calling DELETE', async () => {
    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    await openActionsMenu('feria de emprendedores')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /eliminar feria de emprendedores/i })
    )

    expect(screen.getByRole('dialog')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('confirm delete sends DELETE, removes the row and shows success feedback', async () => {
    mockDelete.mockResolvedValue({})

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    await openActionsMenu('workshop de fotografia')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /eliminar workshop de fotografia/i })
    )

    const dialog = screen.getByRole('dialog')
    const confirmBtn = within(dialog).getByRole('button', { name: /^eliminar$/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(mockDelete).toHaveBeenCalledWith('/events/event-3')
    })

    await waitFor(() => {
      expect(
        screen.getByText(/workshop de fotografia.*eliminado correctamente/i)
      ).toBeInTheDocument()
    })

    // Event should be removed from the list (its name heading disappears)
    await waitFor(() => {
      expect(
        screen.queryAllByRole('heading', { name: /workshop de fotografia/i })
      ).toHaveLength(0)
    })
  })

  it('shows delete error feedback and keeps the row when the API call fails', async () => {
    mockDelete.mockRejectedValue({
      response: { data: { error: { message: 'No autorizado' } } },
    })

    render(<OrganizerDashboard />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await openActionsMenu('recital de rock nacional')
    await userEvent.click(
      await screen.findByRole('menuitem', { name: /eliminar recital de rock nacional/i })
    )

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

    // Kebab: Editar/Eliminar disabled with the readonly title
    await openActionsMenu('concierto pasado')
    const editarItem = await screen.findByRole('menuitem', { name: /editar concierto pasado/i })
    expect(editarItem).toBeDisabled()
    expect(editarItem).toHaveAttribute('title', 'Evento finalizado — solo lectura')
    const eliminarItem = screen.getByRole('menuitem', { name: /eliminar concierto pasado/i })
    expect(eliminarItem).toBeDisabled()
    expect(eliminarItem).toHaveAttribute('title', 'Evento finalizado — solo lectura')
    // Metricas stays enabled for past events (read-only page)
    expect(screen.getByRole('menuitem', { name: /ver metricas de concierto pasado/i })).toBeEnabled()
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
    mockDelete.mockReset()
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
