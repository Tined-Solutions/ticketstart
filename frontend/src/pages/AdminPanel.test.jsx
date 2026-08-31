import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminPanel from './AdminPanel.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()
const mockDelete = vi.fn()
const mockPost = vi.fn()
const mockInvalidateQueries = vi.fn()

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}))

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: (...args) => mockInvalidateQueries(...args) }),
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
    delete: (...args) => mockDelete(...args),
    post: (...args) => mockPost(...args),
  },
}))

// Far-future dates computed at runtime so the mock events never become "past"
// and trip the isPast/read-only UI logic (PEM-002), which would break the
// mutable-path tests for reasons unrelated to what they assert.
const futureDate = (days) =>
  new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString()

const mockEvents = [
  {
    id: 'event-1',
    name: 'Recital de Rock Nacional',
    date: futureDate(400),
    location: 'Estadio Luna Park, Buenos Aires',
    organizerId: 'user-2',
    createdAt: '2026-06-01T10:00:00Z',
    status: 'Approved',
  },
  {
    id: 'event-2',
    name: 'Feria de Emprendedores',
    date: futureDate(300),
    location: 'La Rural, Buenos Aires',
    organizerId: 'user-3',
    createdAt: '2026-06-15T10:00:00Z',
    status: 'Pending',
  },
  {
    id: 'event-3',
    name: 'Workshop de Fotografia',
    date: futureDate(365),
    location: null,
    organizerId: 'user-2',
    createdAt: '2026-07-01T10:00:00Z',
    status: 'Rejected',
  },
]

const mockUsers = [
  {
    id: 'user-1',
    email: 'admin@ticketera.com',
    role: 'Admin',
    createdAt: '2026-01-01T10:00:00Z',
  },
  {
    id: 'user-2',
    email: 'organizador@ticketera.com',
    role: 'Organizador',
    createdAt: '2026-02-01T10:00:00Z',
  },
  {
    id: 'user-3',
    email: 'staff@ticketera.com',
    role: 'Staff',
    createdAt: '2026-03-01T10:00:00Z',
  },
]

// Events render as flat rows inside the events section GlassCard (no per-event
// glass card). Each row carries the `hover:bg-surface-elevated` utility, so we
// scope queries to a single event by walking up from its name heading.
const eventRow = (name) =>
  screen.getByRole('heading', { name: new RegExp(name, 'i') }).closest('[class*="bg-surface-elevated"]')

describe('AdminPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockDelete.mockReset()
    mockPost.mockReset()
    mockNavigate.mockReset()
    mockInvalidateQueries.mockReset()

    // Default: resolve both endpoints
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: { items: mockEvents, total: 3, page: 1, pageSize: 200 },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: mockUsers, total: 3, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
  })

  // ── 27.1: Event list display ──────────────────────────────────────

  it('renders events fetched from admin API', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
  })

  it('resolves organizer email from users list', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Recital de Rock: organizerId = user-2 -> organizador@ticketera.com
    // Appears in both events and users tables, so use getAllByText
    const orgEmails = screen.getAllByText('organizador@ticketera.com')
    expect(orgEmails.length).toBeGreaterThanOrEqual(2) // events table + users table
    // Feria: organizerId = user-3 -> staff@ticketera.com
    const staffEmails = screen.getAllByText('staff@ticketera.com')
    expect(staffEmails.length).toBeGreaterThanOrEqual(1)
  })

  it('shows "—" for events with no location or unknown organizer', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    // Workshop has location: null
    const workshopRow = eventRow('workshop de fotografia')
    expect(workshopRow).toBeTruthy()

    // Should have "—" for location
    expect(within(workshopRow).getByText('—')).toBeInTheDocument()
  })

  // ── 27.1: User list display ──────────────────────────────────────

  it('renders users fetched from admin API', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText('admin@ticketera.com')).toBeInTheDocument()
    // organizador@ticketera.com appears in both events (organizer) and users tables
    const orgEmails = screen.getAllByText('organizador@ticketera.com')
    expect(orgEmails.length).toBeGreaterThanOrEqual(2)
    // staff@ticketera.com appears in both events (organizer) and users tables
    const staffEmails = screen.getAllByText('staff@ticketera.com')
    expect(staffEmails.length).toBeGreaterThanOrEqual(1)
  })

  it('displays role badges for users', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Check role badges exist
    const adminBadges = screen.getAllByText('Admin')
    const orgBadges = screen.getAllByText('Organizador')
    const staffBadges = screen.getAllByText('Staff')

    // At least one user with each role
    expect(adminBadges.length).toBeGreaterThanOrEqual(1)
    expect(orgBadges.length).toBeGreaterThanOrEqual(1)
    expect(staffBadges.length).toBeGreaterThanOrEqual(1)
  })

  // ── 27.1: Section headers with counts ────────────────────────────

  it('shows event count in section header', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText('Eventos (3)')).toBeInTheDocument()
    expect(screen.getByText('Usuarios (3)')).toBeInTheDocument()
  })

  // ── 27.1: Edit button navigation ─────────────────────────────────

  it('edit button navigates to event edit page', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const editRow = eventRow('recital de rock nacional')
    await userEvent.click(within(editRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /editar recital de rock nacional/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1')
  })

  it('compras button navigates to the purchases page (APR-010)', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const comprasRow = eventRow('recital de rock nacional')
    await userEvent.click(within(comprasRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /compras de recital de rock nacional/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/admin/events/event-1/purchases')
  })

  // ── 27.1: Delete button and dialog ───────────────────────────────

  it('delete button opens confirmation dialog', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    const feriaRow = eventRow('feria de emprendedores')
    await userEvent.click(within(feriaRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /eliminar feria de emprendedores/i }))

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/confirmar eliminación/i)).toBeInTheDocument()
    expect(within(dialog).getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /^eliminar$/i })).toBeInTheDocument()
  })

  it('cancel button closes confirmation dialog', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    const feriaRow = eventRow('feria de emprendedores')
    await userEvent.click(within(feriaRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /eliminar feria de emprendedores/i }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()

    const cancelBtn = screen.getByRole('button', { name: /cancelar/i })
    await userEvent.click(cancelBtn)

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(mockDelete).not.toHaveBeenCalled()
  })

  it('confirm delete sends DELETE and removes event from list', async () => {
    mockDelete.mockResolvedValue({})

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/workshop de fotografia/i)).toBeInTheDocument()
    })

    const workshopRow = eventRow('workshop de fotografia')
    await userEvent.click(within(workshopRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /eliminar workshop de fotografia/i }))

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

  it('shows delete error feedback when API call fails', async () => {
    mockDelete.mockRejectedValue({
      response: { data: { error: { message: 'No autorizado' } } },
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const recitalRow = eventRow('recital de rock nacional')
    await userEvent.click(within(recitalRow).getByRole('button', { name: /^acciones/i }))
    await userEvent.click(await screen.findByRole('menuitem', { name: /eliminar recital de rock nacional/i }))

    const dialog = screen.getByRole('dialog')
    const confirmBtn = within(dialog).getByRole('button', { name: /^eliminar$/i })
    await userEvent.click(confirmBtn)

    await waitFor(() => {
      expect(screen.getByText(/no autorizado/i)).toBeInTheDocument()
    })

    // Dialog should be closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    // Event should still be in the list
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
  })

  // ── 27.1: Loading state ──────────────────────────────────────────

  it('shows loading state while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<AdminPanel />)

    expect(screen.getByRole('heading', { name: /panel de administración/i })).toBeInTheDocument()
    expect(screen.getAllByRole('status').length).toBeGreaterThan(0)
  })

  // ── 27.1: Error state ────────────────────────────────────────────

  it('shows error state when events fetch fails', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.reject({
          response: { data: { error: { message: 'Error del servidor' } } },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: mockUsers, total: 3, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/error del servidor/i)).toBeInTheDocument()
    })
  })

  it('shows error state when users fetch fails', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: { items: mockEvents, total: 3, page: 1, pageSize: 200 },
        })
      }
      if (url === '/admin/users') {
        return Promise.reject({
          response: { data: { error: { message: 'Base de datos no disponible' } } },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/base de datos no disponible/i)).toBeInTheDocument()
    })
  })

  it('retry button re-fetches data', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Error temporal' } } },
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/error temporal/i)).toBeInTheDocument()
    })

    // Reset mocks for retry
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: { items: mockEvents, total: 3, page: 1, pageSize: 200 },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: mockUsers, total: 3, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    await userEvent.click(screen.getByRole('button', { name: /reintentar/i }))

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText('admin@ticketera.com')).toBeInTheDocument()
  })

  // ── 27.2: Empty states ───────────────────────────────────────────

  it('shows empty state for events when no events exist', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: { items: [], total: 0, page: 1, pageSize: 200 },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: mockUsers, total: 3, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText('Eventos (0)')).toBeInTheDocument()
    })

    expect(screen.getByText(/no hay eventos en el sistema/i)).toBeInTheDocument()
    // Users should still show
    expect(screen.getByText('admin@ticketera.com')).toBeInTheDocument()
  })

  it('shows empty state for users when no users exist', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: { items: mockEvents, total: 3, page: 1, pageSize: 200 },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: [], total: 0, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText('Usuarios (0)')).toBeInTheDocument()
    })

    expect(screen.getByText(/no hay usuarios registrados/i)).toBeInTheDocument()
    // Events should still show
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
  })

  // ── 27.2: Access control — already handled by route config ───────

  it('fetches both admin endpoints on mount', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(mockGet).toHaveBeenCalledWith('/admin/events', expect.any(Object))
    expect(mockGet).toHaveBeenCalledWith('/admin/users', expect.any(Object))
  })

  // ── Edge: handles flat array response (non-paginated) ────────────

  it('handles non-paginated API response (flat array)', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: mockEvents })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: mockUsers })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    expect(screen.getByText('admin@ticketera.com')).toBeInTheDocument()
  })

  // ── Edge: event with unknown organizerId ──────────────────────────

  it('shows "—" for unknown organizer', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: {
            items: [
              {
                id: 'event-x',
                name: 'Evento Sin Organizador',
                date: futureDate(365),
                location: 'Algun lugar',
                organizerId: 'unknown-user',
                createdAt: '2026-07-01T10:00:00Z',
              },
            ],
            total: 1,
            page: 1,
            pageSize: 200,
          },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({
          data: { items: [], total: 0, page: 1, pageSize: 200 },
        })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/evento sin organizador/i)).toBeInTheDocument()
    })

    // Should show "—" for unknown organizer
    const eventRowEl = eventRow('evento sin organizador')
    expect(within(eventRowEl).getByText('—')).toBeInTheDocument()
  })

  // ── User Creation ────────────────────────────────────────────

  describe('User Creation', () => {
    // The create-user form is hidden by default (toggled inside the Users
    // section). Open it by clicking the "Crear nuevo usuario" button first.
    const openCreateForm = async () => {
      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /crear nuevo usuario/i })
        ).toBeInTheDocument()
      })
      await userEvent.click(screen.getByRole('button', { name: /crear nuevo usuario/i }))
      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })
    }

    it('renders the user creation form with all fields', async () => {
      render(<AdminPanel />)

      await openCreateForm()

      expect(screen.getByLabelText('Nombre')).toBeInTheDocument()
      expect(screen.getByLabelText('Email')).toBeInTheDocument()
      expect(screen.getByLabelText('Contraseña')).toBeInTheDocument()
      expect(screen.getByLabelText('Rol')).toBeInTheDocument()

      const roleSelect = screen.getByLabelText('Rol')
      const options = within(roleSelect).getAllByRole('option')
      const optionTexts = options.map((o) => o.textContent)
      expect(optionTexts).toContain('Organizador')
      expect(optionTexts).toContain('Staff')
      expect(optionTexts).not.toContain('Admin')

      expect(
        screen.getByRole('button', { name: /crear usuario/i })
      ).toBeInTheDocument()
    })

    it('shows validation errors for empty fields', async () => {
      render(<AdminPanel />)

      await openCreateForm()

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/el nombre es obligatorio/i)).toBeInTheDocument()
      expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
      expect(screen.getByText(/la contraseña es obligatoria/i)).toBeInTheDocument()
      expect(screen.getByText(/debes seleccionar un rol/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('shows validation error for invalid email', async () => {
      render(<AdminPanel />)

      await openCreateForm()

      const emailInput = screen.getByLabelText('Email')
      await userEvent.type(emailInput, 'not-an-email')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/email no es válido/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('shows validation error for short password', async () => {
      render(<AdminPanel />)

      await openCreateForm()

      const passwordInput = screen.getByLabelText('Contraseña')
      await userEvent.type(passwordInput, '1234567')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/al menos 8 caracteres/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('creates a user successfully and returns to the list', async () => {
      mockPost.mockResolvedValueOnce({
        status: 201,
        data: { id: 'user-4', name: 'Nuevo Usuario', email: 'nuevo@example.com', role: 'Staff' },
      })

      render(<AdminPanel />)

      await openCreateForm()

      await userEvent.type(screen.getByLabelText('Nombre'), 'Nuevo Usuario')
      await userEvent.type(screen.getByLabelText('Email'), 'nuevo@example.com')
      await userEvent.type(screen.getByLabelText('Contraseña'), 'password123')
      await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Staff')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      await waitFor(() => {
        expect(mockPost).toHaveBeenCalledWith('/admin/users', {
          name: 'Nuevo Usuario',
          email: 'nuevo@example.com',
          password: 'password123',
          role: 'Staff',
        })
      })

      // Success returns the user to the list: create button visible again, form gone
      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /crear nuevo usuario/i })
        ).toBeInTheDocument()
      })
      expect(screen.queryByRole('heading', { name: /crear usuario/i })).not.toBeInTheDocument()
    })

    it('shows error when email is already registered (409)', async () => {
      mockPost.mockRejectedValueOnce({
        response: { status: 409, data: { error: { message: 'El email ya esta registrado' } } },
      })

      render(<AdminPanel />)

      await openCreateForm()

      await userEvent.type(screen.getByLabelText('Nombre'), 'Duplicado')
      await userEvent.type(screen.getByLabelText('Email'), 'existe@example.com')
      await userEvent.type(screen.getByLabelText('Contraseña'), 'password123')
      await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Staff')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(
        await screen.findByText(/el email ya esta registrado/i)
      ).toBeInTheDocument()
    })

    it('shows error feedback on server error', async () => {
      mockPost.mockRejectedValueOnce({
        response: { status: 500, data: { error: { message: 'Error interno del servidor' } } },
      })

      render(<AdminPanel />)

      await openCreateForm()

      await userEvent.type(screen.getByLabelText('Nombre'), 'Test')
      await userEvent.type(screen.getByLabelText('Email'), 'test@example.com')
      await userEvent.type(screen.getByLabelText('Contraseña'), 'password123')
      await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Staff')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(
        await screen.findByText(/error interno del servidor/i)
      ).toBeInTheDocument()
    })

    it('shows loading state during submission', async () => {
      let resolvePost
      mockPost.mockImplementation(
        () =>
          new Promise((resolve) => {
            resolvePost = resolve
          })
      )

      render(<AdminPanel />)

      await openCreateForm()

      await userEvent.type(screen.getByLabelText('Nombre'), 'Test')
      await userEvent.type(screen.getByLabelText('Email'), 'test@example.com')
      await userEvent.type(screen.getByLabelText('Contraseña'), 'password123')
      await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Staff')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByRole('button', { name: /creando/i })).toBeDisabled()
      expect(screen.getByLabelText('Nombre')).toBeDisabled()
      expect(screen.getByLabelText('Email')).toBeDisabled()
      expect(screen.getByLabelText('Contraseña')).toBeDisabled()
      expect(screen.getByLabelText('Rol')).toBeDisabled()

      resolvePost({ status: 201, data: {} })

      // After success the user returns to the list (create button visible again)
      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /crear nuevo usuario/i })
        ).toBeInTheDocument()
      })
    })

    it('toggles between the user list and the create form', async () => {
      render(<AdminPanel />)

      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /crear nuevo usuario/i })
        ).toBeInTheDocument()
      })

      // List/filters visible initially, form hidden
      expect(screen.getByRole('table')).toBeInTheDocument()
      expect(screen.getByLabelText('Buscar usuarios')).toBeInTheDocument()
      expect(screen.queryByRole('heading', { name: /crear usuario/i })).not.toBeInTheDocument()

      // Open the form → list/filters hidden, form shown
      await userEvent.click(screen.getByRole('button', { name: /crear nuevo usuario/i }))
      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })
      expect(screen.queryByRole('table')).not.toBeInTheDocument()
      expect(screen.queryByLabelText('Buscar usuarios')).not.toBeInTheDocument()
      expect(screen.getByLabelText('Nombre')).toBeInTheDocument()

      // Back to the list
      await userEvent.click(screen.getByRole('button', { name: /volver a la lista/i }))
      await waitFor(() => {
        expect(screen.getByRole('table')).toBeInTheDocument()
      })
      expect(screen.queryByRole('heading', { name: /crear usuario/i })).not.toBeInTheDocument()
    })
  })

  // ── EA-008: pending count + status badges + approve/reject ────────

  it('shows the pending count badge', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Only event-2 is Pending → "Pendientes: 1" next to the events header
    expect(screen.getByText('Pendientes: 1')).toBeInTheDocument()
  })

  it('renders a status badge per event row', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const approvedRow = eventRow('recital de rock nacional')
    const pendingRow = eventRow('feria de emprendedores')
    const rejectedRow = eventRow('workshop de fotografia')

    expect(within(approvedRow).getByText('Aprobado')).toBeInTheDocument()
    expect(within(pendingRow).getByText('Pendiente')).toBeInTheDocument()
    expect(within(rejectedRow).getByText('Rechazado')).toBeInTheDocument()
  })

  it('shows Approve/Reject actions per status (EA-008)', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    // Pending (event-2): both actions
    expect(screen.getByRole('button', { name: /aprobar feria de emprendedores/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /rechazar feria de emprendedores/i })).toBeInTheDocument()
    // Approved (event-1): no moderation actions — Reject is hidden for
    // already-approved events (decided; EA-005 backend stays untouched)
    expect(screen.queryByRole('button', { name: /aprobar recital de rock nacional/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /rechazar recital de rock nacional/i })).not.toBeInTheDocument()
    // Rejected (event-3): Approve only — re-publish (EA-005)
    expect(screen.getByRole('button', { name: /aprobar workshop de fotografia/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /rechazar workshop de fotografia/i })).not.toBeInTheDocument()
  })

  it('approve success invalidates queries, refetches and shows Approved badge', async () => {
    // Mutate the source data BEFORE the POST resolves so the refetch reflects it
    let eventsData = mockEvents.map((e) => ({ ...e }))
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: { items: eventsData, total: 3, page: 1, pageSize: 200 } })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: mockUsers, total: 3, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
    mockPost.mockImplementation(async (url) => {
      if (url === '/admin/events/event-2/approve') {
        eventsData = eventsData.map((e) =>
          e.id === 'event-2' ? { ...e, status: 'Approved' } : e
        )
      }
      return { data: {} }
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /aprobar feria de emprendedores/i }))

    await waitFor(() => {
      expect(mockPost).toHaveBeenCalledWith('/admin/events/event-2/approve')
    })

    // D-6: catalog + detail queries invalidated
    expect(mockInvalidateQueries).toHaveBeenCalledWith(['events'])
    expect(mockInvalidateQueries).toHaveBeenCalledWith(['event', 'event-2'])

    // Refetch applied → the row now shows the Approved badge and pending count drops
    await waitFor(() => {
      const feriaRow = eventRow('feria de emprendedores')
      expect(within(feriaRow).getByText('Aprobado')).toBeInTheDocument()
    })
    expect(screen.getByText('Pendientes: 0')).toBeInTheDocument()
    expect(screen.getByText(/aprobado correctamente/i)).toBeInTheDocument()
  })

  it('approve failure shows error and leaves state unchanged (EA-008)', async () => {
    mockPost.mockRejectedValue({
      response: { data: { error: { message: 'Error del servidor' } } },
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /aprobar feria de emprendedores/i }))

    await waitFor(() => {
      expect(screen.getByText(/error del servidor/i)).toBeInTheDocument()
    })

    // No query invalidation and the Pending badge stays
    expect(mockInvalidateQueries).not.toHaveBeenCalled()
    expect(screen.getByText('Pendientes: 1')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /aprobar feria de emprendedores/i })).toBeInTheDocument()
    const feriaRow = eventRow('feria de emprendedores')
    expect(within(feriaRow).getByText('Pendiente')).toBeInTheDocument()
  })

  it('keeps past events read-only: Ver + Finalizado, mutations disabled (PEM-002)', async () => {
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: {
            items: [
              {
                id: 'event-past',
                name: 'Concierto Pasado',
                date: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(),
                location: 'Teatro Colón',
                organizerId: 'user-2',
                createdAt: '2026-01-01T10:00:00Z',
                status: 'Approved',
              },
            ],
            total: 1,
            page: 1,
            pageSize: 200,
          },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: mockUsers, total: 3, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/concierto pasado/i)).toBeInTheDocument()
    })

    const pastRow = eventRow('concierto pasado')

    // Finalizado badge + Ver button (read-only view) are shown
    expect(within(pastRow).getByText('Finalizado')).toBeInTheDocument()
    await userEvent.click(within(pastRow).getByRole('button', { name: /ver concierto pasado/i }))
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-past/view')

    // Visible mutation button (Agregar entradas) is disabled for past events
    expect(
      within(pastRow).getByRole('button', { name: /agregar entradas a concierto pasado/i })
    ).toBeDisabled()

    // Kebab: Editar/Eliminar disabled, Compras always enabled
    await userEvent.click(within(pastRow).getByRole('button', { name: /^acciones/i }))
    expect(await screen.findByRole('menuitem', { name: /editar concierto pasado/i })).toBeDisabled()
    expect(screen.getByRole('menuitem', { name: /eliminar concierto pasado/i })).toBeDisabled()
    expect(screen.getByRole('menuitem', { name: /compras de concierto pasado/i })).toBeEnabled()
  })

  it('sorts upcoming events soonest-first by date (all dates in the future)', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    // mockEvents dates (days from now): event-1=400, event-2=300, event-3=365.
    // All are future, so they are grouped as upcoming and sorted ascending.
    const headings = screen.getAllByRole('heading', { level: 3 })
    const names = headings.map((h) => h.textContent)
    expect(names).toEqual([
      'Feria de Emprendedores',
      'Workshop de Fotografia',
      'Recital de Rock Nacional',
    ])

    // Header counts and Pendientes badge still read the unsorted source
    expect(screen.getByText('Eventos (3)')).toBeInTheDocument()
    expect(screen.getByText('Pendientes: 1')).toBeInTheDocument()
  })

  it('orders upcoming before past events, soonest-upcoming first, oldest-past last', async () => {
    const day = 24 * 60 * 60 * 1000
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({
          data: {
            items: [
              { id: 'e-gala', name: 'Gala Anual', date: new Date(Date.now() - 400 * day).toISOString(), location: null, organizerId: 'user-2', status: 'Approved' },
              { id: 'e-taller', name: 'Taller de Arte', date: new Date(Date.now() + 10 * day).toISOString(), location: null, organizerId: 'user-2', status: 'Pending' },
              { id: 'e-vintage', name: 'Concierto Vintage', date: new Date(Date.now() - 30 * day).toISOString(), location: null, organizerId: 'user-2', status: 'Approved' },
              { id: 'e-festival', name: 'Festival Primavera', date: new Date(Date.now() + 50 * day).toISOString(), location: null, organizerId: 'user-2', status: 'Approved' },
            ],
            total: 4,
            page: 1,
            pageSize: 200,
          },
        })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: mockUsers, total: 3, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

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
  })

  it('kebab menu opens with a high z-index panel (not clipped by the row below)', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const row = eventRow('recital de rock nacional')
    await userEvent.click(within(row).getByRole('button', { name: /^acciones/i }))

    const menu = await screen.findByRole('menu')
    expect(menu).toBeInTheDocument()
    // The panel carries a high z-index so it paints above sibling rows
    expect(menu.className).toContain('z-50')
  })
})

// ── Users: filter & pagination ────────────────────────────────────────

describe('AdminPanel — Users filter & pagination', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockDelete.mockReset()
    mockPost.mockReset()
    mockNavigate.mockReset()
    mockInvalidateQueries.mockReset()

    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: { items: mockEvents, total: 3, page: 1, pageSize: 200 } })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: mockUsers, total: 3, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
  })

  // The users table is the only <table> on the page (events are flat rows),
  // so scope assertions to it to avoid matching organizer emails in events.
  const usersTable = () => screen.getByRole('table')

  it('filters users by role', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await userEvent.selectOptions(screen.getByLabelText('Filtrar por rol'), 'Staff')

    const table = usersTable()
    expect(within(table).getByText('staff@ticketera.com')).toBeInTheDocument()
    expect(within(table).queryByText('admin@ticketera.com')).not.toBeInTheDocument()
    expect(within(table).queryByText('organizador@ticketera.com')).not.toBeInTheDocument()
  })

  it('filters users by search (email/name)', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await userEvent.type(screen.getByLabelText('Buscar usuarios'), 'admin')

    const table = usersTable()
    expect(within(table).getByText('admin@ticketera.com')).toBeInTheDocument()
    expect(within(table).queryByText('staff@ticketera.com')).not.toBeInTheDocument()
    expect(within(table).queryByText('organizador@ticketera.com')).not.toBeInTheDocument()
  })

  it('shows filtered-empty state when no users match', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    await userEvent.type(screen.getByLabelText('Buscar usuarios'), 'zzz-no-match')

    expect(
      screen.getByText('No se encontraron usuarios con esos filtros.')
    ).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('paginates users 10 per page', async () => {
    const manyUsers = Array.from({ length: 12 }, (_, i) => ({
      id: `u-${i}`,
      email: `user${i}@example.com`,
      name: `User ${i}`,
      role: 'Staff',
      createdAt: '2026-01-01T10:00:00Z',
    }))
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: { items: [], total: 0, page: 1, pageSize: 200 } })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: manyUsers, total: 12, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText('Usuarios (12)')).toBeInTheDocument()
    })

    const countDataRows = () => within(usersTable()).getAllByRole('row').length - 1

    expect(countDataRows()).toBe(10)
    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    expect(screen.getByText('user0@example.com')).toBeInTheDocument()
    expect(screen.queryByText('user11@example.com')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /página siguiente/i }))

    expect(countDataRows()).toBe(2)
    expect(screen.getByText('Página 2 de 2')).toBeInTheDocument()
    expect(screen.getByText('user11@example.com')).toBeInTheDocument()
    expect(screen.queryByText('user0@example.com')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /página siguiente/i })).toBeDisabled()
  })

  it('resets to page 1 when filtering from a later page', async () => {
    // 24 users: even index = Admin (12), odd index = Staff (12) → 3 pages unfiltered
    const manyUsers = Array.from({ length: 24 }, (_, i) => ({
      id: `u-${i}`,
      email: `user${i}@example.com`,
      name: `User ${i}`,
      role: i % 2 === 0 ? 'Admin' : 'Staff',
      createdAt: '2026-01-01T10:00:00Z',
    }))
    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: { items: [], total: 0, page: 1, pageSize: 200 } })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: manyUsers, total: 24, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText('Página 1 de 3')).toBeInTheDocument()
    })

    await userEvent.click(screen.getByRole('button', { name: /página siguiente/i }))
    expect(screen.getByText('Página 2 de 3')).toBeInTheDocument()

    // Filtering by Admin (12 users → 2 pages) resets back to page 1
    await userEvent.selectOptions(screen.getByLabelText('Filtrar por rol'), 'Admin')

    expect(screen.getByText('Página 1 de 2')).toBeInTheDocument()
    expect(screen.getByText('user0@example.com')).toBeInTheDocument()
  })
})

// ── Visual Regression: Glass & Theme ──────────────────────────────────

describe('AdminPanel — Visual Regression', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockDelete.mockReset()
    mockPost.mockReset()
    mockNavigate.mockReset()
    mockInvalidateQueries.mockReset()

    mockGet.mockImplementation((url) => {
      if (url === '/admin/events') {
        return Promise.resolve({ data: { items: mockEvents, total: 3, page: 1, pageSize: 200 } })
      }
      if (url === '/admin/users') {
        return Promise.resolve({ data: { items: mockUsers, total: 3, page: 1, pageSize: 200 } })
      }
      return Promise.reject(new Error('Unknown endpoint'))
    })
  })

  it('renders GlassCard wrappers for admin sections', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(2) // events + users sections
  })

  it('renders Badge components for user roles', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
    })

    // Badge renders as <span> with role labels; getAllByText finds the text
    const adminBadges = screen.getAllByText('Admin')
    const orgBadges = screen.getAllByText('Organizador')
    const staffBadges = screen.getAllByText('Staff')

    expect(adminBadges.length).toBeGreaterThanOrEqual(1)
    expect(orgBadges.length).toBeGreaterThanOrEqual(1)
    expect(staffBadges.length).toBeGreaterThanOrEqual(1)
  })

  it('renders GlassCard in the loading state', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<AdminPanel />)

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })

  it('renders GlassCard in the error state', async () => {
    mockGet.mockRejectedValue({
      response: { data: { error: { message: 'Server error' } } },
    })

    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/server error/i)).toBeInTheDocument()
    })

    const glassElements = document.querySelectorAll('.glass-surface')
    expect(glassElements.length).toBeGreaterThanOrEqual(1)
  })
})
