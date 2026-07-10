import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import Register from './Register.jsx'

const mockNavigate = vi.fn()
const mockRegister = vi.fn()

vi.mock('react-router-dom', () => ({
  Link: ({ to, children }) => <a href={to}>{children}</a>,
  useNavigate: () => mockNavigate,
}))

vi.mock('../context/auth.js', () => ({
  useAuth: () => ({ register: mockRegister }),
}))

function fillForm({
  name = 'Juan Perez',
  email = 'juan@example.com',
  password = 'password123',
  confirmPassword = 'password123',
  role = 'Organizador',
} = {}) {
  return {
    nameInput: screen.getByLabelText(/nombre/i),
    emailInput: screen.getByLabelText(/email/i),
    passwordInput: screen.getByLabelText(/^contrasena/i),
    confirmInput: screen.getByLabelText(/confirmar contrasena/i),
    roleSelect: screen.getByLabelText(/rol/i),
    submitButton: screen.getByRole('button', { name: /registrarse/i }),
    async fill() {
      await userEvent.clear(this.nameInput)
      await userEvent.clear(this.emailInput)
      await userEvent.clear(this.passwordInput)
      await userEvent.clear(this.confirmInput)

      await userEvent.type(this.nameInput, name)
      await userEvent.type(this.emailInput, email)
      await userEvent.type(this.passwordInput, password)
      await userEvent.type(this.confirmInput, confirmPassword)
      await userEvent.selectOptions(this.roleSelect, role)
    },
  }
}

describe('Register', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockRegister.mockReset()
    mockNavigate.mockReset()
  })

  it('renders the registration form', () => {
    render(<Register />)

    expect(screen.getByRole('heading', { name: /crear cuenta/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/nombre/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^contrasena/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/confirmar contrasena/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/rol/i)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /iniciar sesion/i })).toHaveAttribute(
      'href',
      '/login'
    )
  })

  it('shows validation errors for empty fields', async () => {
    render(<Register />)

    await userEvent.click(screen.getByRole('button', { name: /registrarse/i }))

    expect(screen.getByText(/el nombre es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/el email es obligatorio/i)).toBeInTheDocument()
    expect(screen.getByText(/la contrasena es obligatoria/i)).toBeInTheDocument()
    expect(screen.getByText(/debes confirmar la contrasena/i)).toBeInTheDocument()
    expect(screen.getByText(/debes seleccionar un rol/i)).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('shows an error for invalid email', async () => {
    render(<Register />)
    const form = fillForm({ email: 'not-an-email' })
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(screen.getByText(/el formato del email no es valido/i)).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('shows an error when password is too short', async () => {
    render(<Register />)
    const form = fillForm({ password: '1234567', confirmPassword: '1234567' })
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(
      screen.getByText(/la contrasena debe tener al menos 8 caracteres/i)
    ).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('shows an error when passwords do not match', async () => {
    render(<Register />)
    const form = fillForm({ confirmPassword: 'different-password' })
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(screen.getByText(/las contrasenas no coinciden/i)).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('shows an error when name is too short', async () => {
    render(<Register />)
    const form = fillForm({ name: 'J' })
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(
      screen.getByText(/el nombre debe tener al menos 2 caracteres/i)
    ).toBeInTheDocument()
    expect(mockRegister).not.toHaveBeenCalled()
  })

  it('redirects organizer to the dashboard after successful registration', async () => {
    mockRegister.mockResolvedValue({
      id: 'user-1',
      name: 'Juan Perez',
      email: 'juan@example.com',
      role: 'Organizador',
    })

    render(<Register />)
    const form = fillForm({ role: 'Organizador' })
    await form.fill()

    await userEvent.click(form.submitButton)

    await waitFor(() => {
      expect(mockRegister).toHaveBeenCalledWith(
        'Juan Perez',
        'juan@example.com',
        'password123',
        'Organizador'
      )
      expect(mockNavigate).toHaveBeenCalledWith('/organizer/dashboard', { replace: true })
    })
  })

  it('redirects staff to the scanner after successful registration', async () => {
    mockRegister.mockResolvedValue({
      id: 'user-2',
      name: 'Maria Gomez',
      email: 'maria@example.com',
      role: 'Staff',
    })

    render(<Register />)
    const form = fillForm({ email: 'maria@example.com', role: 'Staff' })
    await form.fill()

    await userEvent.click(form.submitButton)

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/staff/scan', { replace: true })
    })
  })

  it('displays the API error message when registration fails', async () => {
    mockRegister.mockRejectedValue({
      response: { data: { error: { message: 'El email ya esta registrado' } } },
    })

    render(<Register />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(await screen.findByText(/el email ya esta registrado/i)).toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('displays a fallback error message when the API error has no message', async () => {
    mockRegister.mockRejectedValue(new Error('Network error'))

    render(<Register />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(await screen.findByText(/ocurrio un error al crear la cuenta/i)).toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('shows loading state during submission', async () => {
    let resolveRegister
    mockRegister.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRegister = resolve
        })
    )

    render(<Register />)
    const form = fillForm()
    await form.fill()

    await userEvent.click(form.submitButton)

    expect(screen.getByRole('button', { name: /creando cuenta/i })).toBeDisabled()
    expect(screen.getByLabelText(/nombre/i)).toBeDisabled()

    resolveRegister({ id: 'user-3', role: 'Organizador' })

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /registrarse/i })).toBeInTheDocument()
    })
  })
})
