import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ResetPasswordModal from '../ResetPasswordModal.jsx'

const mockPost = vi.fn()

vi.mock('../../api/client.js', () => ({
  default: {
    post: (...args) => mockPost(...args),
  },
}))

vi.mock('../../lib/apiError.js', () => ({
  getErrorMessage: (err) => err?.response?.data?.error || 'Error inesperado',
}))

const user = { id: 'user-2', email: 'staff@ticketera.com', role: 'Staff' }
const TEMP_PASSWORD = 'aB3dEf6hIj9l'

describe('ResetPasswordModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPost.mockReset()
    Object.assign(navigator, {
      clipboard: { writeText: vi.fn().mockResolvedValue(undefined) },
    })
  })

  it('shows the temporary password once with a copy affordance and a not-shown-again warning', async () => {
    mockPost.mockResolvedValue({ data: { temporaryPassword: TEMP_PASSWORD } })

    render(<ResetPasswordModal user={user} onClose={vi.fn()} />)

    // Confirm step first
    await userEvent.click(await screen.findByRole('button', { name: /generar contraseña temporal/i }))

    // Result step: the credential is displayed exactly once, with copy + warning
    expect(await screen.findByText(TEMP_PASSWORD)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /copiar/i })).toBeInTheDocument()
    expect(screen.getByText(/no se volverá a mostrar/i)).toBeInTheDocument()
    expect(mockPost).toHaveBeenCalledWith('/admin/users/user-2/reset-password')
  })

  it('copies the credential via navigator.clipboard.writeText', async () => {
    mockPost.mockResolvedValue({ data: { temporaryPassword: TEMP_PASSWORD } })

    render(<ResetPasswordModal user={user} onClose={vi.fn()} />)
    await userEvent.click(await screen.findByRole('button', { name: /generar contraseña temporal/i }))
    await screen.findByText(TEMP_PASSWORD)

    await userEvent.click(screen.getByRole('button', { name: /copiar/i }))

    await waitFor(() => {
      expect(navigator.clipboard.writeText).toHaveBeenCalledWith(TEMP_PASSWORD)
    })
    expect(await screen.findByText(/¡copiada!/i)).toBeInTheDocument()
  })

  it('clears the credential on close so it is not retrievable afterwards', async () => {
    mockPost.mockResolvedValue({ data: { temporaryPassword: TEMP_PASSWORD } })
    const onClose = vi.fn()

    const { unmount } = render(<ResetPasswordModal user={user} onClose={onClose} />)
    await userEvent.click(await screen.findByRole('button', { name: /generar contraseña temporal/i }))
    await screen.findByText(TEMP_PASSWORD)

    // Close via "Entendido"
    await userEvent.click(screen.getByRole('button', { name: 'Entendido' }))
    expect(onClose).toHaveBeenCalledTimes(1)

    // After close the credential is gone from the DOM and cannot be re-shown
    unmount()
    expect(screen.queryByText(TEMP_PASSWORD)).not.toBeInTheDocument()
    expect(document.body.textContent).not.toContain(TEMP_PASSWORD)
  })

  it('surfaces a failed reset as error feedback and keeps the confirm step', async () => {
    mockPost.mockRejectedValue({
      response: { status: 404, data: { error: 'User not found' } },
    })

    render(<ResetPasswordModal user={user} onClose={vi.fn()} />)

    await userEvent.click(await screen.findByRole('button', { name: /generar contraseña temporal/i }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/user not found/i)
    expect(screen.queryByText(TEMP_PASSWORD)).not.toBeInTheDocument()
  })
})
