import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminPanel from './AdminPanel.jsx'

const mockNavigate = vi.fn()
const mockGet = vi.fn()
const mockDelete = vi.fn()
const mockPost = vi.fn()

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}))

vi.mock('../api/client.js', () => ({
  default: {
    get: (...args) => mockGet(...args),
    delete: (...args) => mockDelete(...args),
    post: (...args) => mockPost(...args),
  },
}))

const mockEvents = [
  {
    id: 'event-1',
    name: 'Recital de Rock Nacional',
    date: '2026-08-15T21:00:00Z',
    location: 'Estadio Luna Park, Buenos Aires',
    organizerId: 'user-2',
    createdAt: '2026-06-01T10:00:00Z',
  },
  {
    id: 'event-2',
    name: 'Feria de Emprendedores',
    date: '2026-09-01T14:00:00Z',
    location: 'La Rural, Buenos Aires',
    organizerId: 'user-3',
    createdAt: '2026-06-15T10:00:00Z',
  },
  {
    id: 'event-3',
    name: 'Workshop de Fotografia',
    date: '2026-10-10T10:00:00Z',
    location: null,
    organizerId: 'user-2',
    createdAt: '2026-07-01T10:00:00Z',
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

describe('AdminPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockGet.mockReset()
    mockDelete.mockReset()
    mockPost.mockReset()
    mockNavigate.mockReset()

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
    // Look for the workshop row
    const rows = screen.getAllByRole('row')
    const workshopRow = rows.find((r) => r.textContent.includes('Workshop de Fotografia'))
    expect(workshopRow).toBeTruthy()

    // Should have "—" for location
    const locationCell = within(workshopRow).getByText('—', { selector: '[data-label="Ubicacion"]' })
    expect(locationCell).toBeInTheDocument()
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

    const editBtn = screen.getByRole('button', { name: /editar recital de rock nacional/i })
    await userEvent.click(editBtn)
    expect(mockNavigate).toHaveBeenCalledWith('/organizer/events/event-1')
  })

  // ── 27.1: Delete button and dialog ───────────────────────────────

  it('delete button opens confirmation dialog', async () => {
    render(<AdminPanel />)

    await waitFor(() => {
      expect(screen.getByText(/feria de emprendedores/i)).toBeInTheDocument()
    })

    const deleteBtn = screen.getByRole('button', { name: /eliminar feria de emprendedores/i })
    await userEvent.click(deleteBtn)

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText(/confirmar eliminacion/i)).toBeInTheDocument()
    expect(within(dialog).getByText(/feria de emprendedores/i)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /cancelar/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /^eliminar$/i })).toBeInTheDocument()
  })

  it('cancel button closes confirmation dialog', async () => {
    render(<AdminPanel />)

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
    mockDelete.mockResolvedValue({})

    render(<AdminPanel />)

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

    await waitFor(() => {
      expect(
        screen.getByText(/workshop de fotografia.*eliminado correctamente/i)
      ).toBeInTheDocument()
    })

    // Event should be removed from the table
    const rows = screen.getAllByRole('row')
    const workshopRow = rows.find((r) => r.textContent.includes('Workshop de Fotografia'))
    expect(workshopRow).toBeFalsy()
  })

  it('shows delete error feedback when API call fails', async () => {
    mockDelete.mockRejectedValue({
      response: { data: { error: { message: 'No autorizado' } } },
    })

    render(<AdminPanel />)

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

    // Dialog should be closed
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    // Event should still be in the list
    expect(screen.getByText(/recital de rock nacional/i)).toBeInTheDocument()
  })

  // ── 27.1: Loading state ──────────────────────────────────────────

  it('shows loading state while fetching', () => {
    mockGet.mockImplementation(() => new Promise(() => {}))

    render(<AdminPanel />)

    expect(screen.getByRole('heading', { name: /panel de administracion/i })).toBeInTheDocument()
    expect(screen.getByText(/cargando panel de administracion/i)).toBeInTheDocument()
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
                date: '2026-10-10T10:00:00Z',
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
    const rows = screen.getAllByRole('row')
    const eventRow = rows.find((r) => r.textContent.includes('Evento Sin Organizador'))
    const organizerCell = within(eventRow).getByText('—', { selector: '[data-label="Organizador"]' })
    expect(organizerCell).toBeInTheDocument()
  })

  // ── User Creation ────────────────────────────────────────────

  describe('User Creation', () => {
    it('renders the user creation form with all fields', async () => {
      render(<AdminPanel />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      expect(screen.getByLabelText('Nombre')).toBeInTheDocument()
      expect(screen.getByLabelText('Email')).toBeInTheDocument()
      expect(screen.getByLabelText('Contrasena')).toBeInTheDocument()
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

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/el nombre es obligatorio/i)).toBeInTheDocument()
      expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
      expect(screen.getByText(/la contrasena es obligatoria/i)).toBeInTheDocument()
      expect(screen.getByText(/debes seleccionar un rol/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('shows validation error for invalid email', async () => {
      render(<AdminPanel />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      const emailInput = screen.getByLabelText('Email')
      await userEvent.type(emailInput, 'not-an-email')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/email no es valido/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('shows validation error for short password', async () => {
      render(<AdminPanel />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      const passwordInput = screen.getByLabelText('Contrasena')
      await userEvent.type(passwordInput, '1234567')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByText(/al menos 8 caracteres/i)).toBeInTheDocument()
      expect(mockPost).not.toHaveBeenCalled()
    })

    it('creates a user successfully and shows feedback', async () => {
      mockPost.mockResolvedValueOnce({
        status: 201,
        data: { id: 'user-4', name: 'Nuevo Usuario', email: 'nuevo@example.com', role: 'Staff' },
      })

      render(<AdminPanel />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      await userEvent.type(screen.getByLabelText('Nombre'), 'Nuevo Usuario')
      await userEvent.type(screen.getByLabelText('Email'), 'nuevo@example.com')
      await userEvent.type(screen.getByLabelText('Contrasena'), 'password123')
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

      expect(
        screen.getByText(/usuario creado correctamente/i)
      ).toBeInTheDocument()
    })

    it('shows error when email is already registered (409)', async () => {
      mockPost.mockRejectedValueOnce({
        response: { status: 409, data: { error: { message: 'El email ya esta registrado' } } },
      })

      render(<AdminPanel />)

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      await userEvent.type(screen.getByLabelText('Nombre'), 'Duplicado')
      await userEvent.type(screen.getByLabelText('Email'), 'existe@example.com')
      await userEvent.type(screen.getByLabelText('Contrasena'), 'password123')
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

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      await userEvent.type(screen.getByLabelText('Nombre'), 'Test')
      await userEvent.type(screen.getByLabelText('Email'), 'test@example.com')
      await userEvent.type(screen.getByLabelText('Contrasena'), 'password123')
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

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /crear usuario/i })).toBeInTheDocument()
      })

      await userEvent.type(screen.getByLabelText('Nombre'), 'Test')
      await userEvent.type(screen.getByLabelText('Email'), 'test@example.com')
      await userEvent.type(screen.getByLabelText('Contrasena'), 'password123')
      await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Staff')

      await userEvent.click(screen.getByRole('button', { name: /crear usuario/i }))

      expect(screen.getByRole('button', { name: /creando/i })).toBeDisabled()
      expect(screen.getByLabelText('Nombre')).toBeDisabled()
      expect(screen.getByLabelText('Email')).toBeDisabled()
      expect(screen.getByLabelText('Contrasena')).toBeDisabled()
      expect(screen.getByLabelText('Rol')).toBeDisabled()

      resolvePost({ status: 201, data: {} })

      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /crear usuario/i })
        ).toBeInTheDocument()
      })
    })
  })
})
