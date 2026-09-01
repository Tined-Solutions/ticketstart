import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import RoleEditModal from '../RoleEditModal.jsx'

const mockPut = vi.fn()

vi.mock('../../api/client.js', () => ({
  default: {
    put: (...args) => mockPut(...args),
  },
}))

vi.mock('../../lib/apiError.js', () => ({
  getErrorMessage: (err) => err?.response?.data?.error || 'Error inesperado',
}))

const user = { id: 'user-1', email: 'staff@ticketera.com', role: 'Staff' }

describe('RoleEditModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPut.mockReset()
  })

  it('offers all four UserRole values including SinAcceso', async () => {
    render(<RoleEditModal user={user} onClose={vi.fn()} onSuccess={vi.fn()} />)

    const select = await screen.findByLabelText('Rol')

    const values = Array.from(select.options).map((option) => option.value)
    expect(values).toEqual(['Organizador', 'Staff', 'Admin', 'SinAcceso'])

    // Display copy renders SinAcceso as "Sin acceso"
    const labels = Array.from(select.options).map((option) => option.textContent)
    expect(labels).toContain('Sin acceso')
  })

  it('pre-selects the current role and PUTs the new role on confirm, then fires success', async () => {
    mockPut.mockResolvedValue({ data: {} })
    const onSuccess = vi.fn()
    const onClose = vi.fn()

    render(<RoleEditModal user={user} onClose={onClose} onSuccess={onSuccess} />)

    const select = await screen.findByLabelText('Rol')
    expect(select.value).toBe('Staff')

    await userEvent.selectOptions(select, 'Organizador')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => {
      expect(mockPut).toHaveBeenCalledWith('/admin/users/user-1/role', { role: 'Organizador' })
      expect(onSuccess).toHaveBeenCalledTimes(1)
    })
  })

  it('surfaces the 400 self-edit error as feedback, keeps the modal open and applies no change', async () => {
    mockPut.mockRejectedValue({
      response: { status: 400, data: { error: 'You cannot change your own role' } },
    })
    const onSuccess = vi.fn()

    render(<RoleEditModal user={user} onClose={vi.fn()} onSuccess={onSuccess} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Guardar' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/you cannot change your own role/i)

    // The modal stays open (dialog still present) and success never fired.
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeInTheDocument()
    expect(onSuccess).not.toHaveBeenCalled()
  })

  it('calls onClose when cancelled', async () => {
    const onClose = vi.fn()
    render(<RoleEditModal user={user} onClose={onClose} onSuccess={vi.fn()} />)

    await userEvent.click(await screen.findByRole('button', { name: 'Cancelar' }))

    expect(onClose).toHaveBeenCalledTimes(1)
    expect(mockPut).not.toHaveBeenCalled()
  })
})
