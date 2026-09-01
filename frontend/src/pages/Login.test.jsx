import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Login from './Login.jsx'

const mockNavigate = vi.fn()
const mockLogin = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => ({ login: mockLogin }),
}))

function fillForm({
  email = 'juan@example.com',
  password = 'password123',
} = {}) {
  return {
    emailInput: screen.getByLabelText(/email/i),
    passwordInput: screen.getByLabelText(/contrasena/i),
    submitButton: screen.getByRole('button', { name: /ingresar/i }),
    async fill() {
      await userEvent.clear(this.emailInput)
      await userEvent.clear(this.passwordInput)

      await userEvent.type(this.emailInput, email)
      await userEvent.type(this.passwordInput, password)
    },
  }
}

describe('Login', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockLogin.mockReset()
    mockNavigate.mockReset()
  })

  it('renders the login form', () => {
    render(<Login />)

    expect(screen.getByRole('heading', { name: /iniciar sesion/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/contrasena/i)).toBeInTheDocument()
  })

  it('shows validation errors for empty fields', async () => {
    render(<Login />)

    await userEvent.click(screen.getByRole('button', { name: /ingresar/i }))

    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/la contrasena es obligatoria/i)).toBeInTheDocument()
    expect(mockLogin).not.toHaveBeenCalled()
  })

  it('shows an error for invalid email', async () => {
    render(<Login />)
    const form = fillForm({ email: 'not-an-email' })
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(screen.getByText(/el formato del email no es valido/i)).toBeInTheDocument()
    expect(mockLogin).not.toHaveBeenCalled()
  })

  it.each([
    { role: 'Organizador', path: '/organizer/dashboard' },
    { role: 'Staff', path: '/staff/scan' },
    { role: 'Admin', path: '/admin' },
    // AUM-002 `sinacceso-redirect-home`: no organizer/staff/admin surface is
    // offered — the revoked user lands on home.
    { role: 'SinAcceso', path: '/' },
  ])('redirects $role users to $path after login', async ({ role, path }) => {
    mockLogin.mockResolvedValue({
      id: 'user-1',
      email: 'juan@example.com',
      role,
      name: 'Juan Perez',
    })

    render(<Login />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    await waitFor(() => {
      expect(mockLogin).toHaveBeenCalledWith('juan@example.com', 'password123')
      expect(mockNavigate).toHaveBeenCalledWith(path, { replace: true })
    })
  })

  it('displays the API error message when login fails', async () => {
    mockLogin.mockRejectedValue({
      response: { data: { error: { message: 'Credenciales invalidas' } } },
    })

    render(<Login />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(await screen.findByText(/credenciales invalidas/i)).toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('displays a fallback error message when the API returns 401 without a message', async () => {
    mockLogin.mockRejectedValue({
      response: { status: 401, data: {} },
    })

    render(<Login />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(await screen.findByText(/credenciales invalidas/i)).toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('shows loading state during submission', async () => {
    let resolveLogin
    mockLogin.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveLogin = resolve
        })
    )

    render(<Login />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(screen.getByRole('button', { name: /ingresando/i })).toBeDisabled()
    expect(screen.getByLabelText(/email/i)).toBeDisabled()

    resolveLogin({ id: 'user-1', role: 'Organizador' })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /ingresar/i })).toBeInTheDocument()
    })
  })
})
