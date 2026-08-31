import { useState } from 'react'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import Modal from './Modal.jsx'
import Button from './Button.jsx'

// All UserRole values the role-edit modal MUST offer (AUM-005). The value is
// the enum name the API expects; the label is the display copy.
const ROLE_OPTIONS = [
  { value: 'Organizador', label: 'Organizador' },
  { value: 'Staff', label: 'Staff' },
  { value: 'Admin', label: 'Admin' },
  { value: 'SinAcceso', label: 'Sin acceso' },
]

/**
 * Role-edit modal (AUM-001 / AUM-005, D13): offers every UserRole value and
 * PUTs the new role on confirm. Success hands control back to the parent
 * (which reloads the users list, D16); a rejected request surfaces the error
 * as feedback and keeps the modal open with no change applied.
 */
export default function RoleEditModal({ user, onClose, onSuccess }) {
  const [role, setRole] = useState(user?.role ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (event) => {
    event.preventDefault()
    if (!role || !user) return

    setSaving(true)
    setError('')
    try {
      await apiClient.put(`/admin/users/${user.id}/role`, { role })
      onSuccess?.()
    } catch (err) {
      setError(getErrorMessage(err))
      setSaving(false)
    }
  }

  return (
    <Modal open onClose={onClose} title="Editar rol">
      <form onSubmit={handleSubmit} noValidate>
        <p className="text-sm text-text-2 mb-4">
          Usuario: <span className="font-medium text-text-1">{user?.email}</span>
        </p>

        {error && (
          <div className="bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30 rounded-lg py-3 px-4 mb-4 font-medium" role="alert">
            {error}
          </div>
        )}

        <div className="form-group">
          <label htmlFor="role-edit-select">Rol</label>
          <select
            id="role-edit-select"
            value={role}
            onChange={(e) => {
              setRole(e.target.value)
              setError('')
            }}
            disabled={saving}
          >
            {ROLE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="flex justify-end gap-3 mt-4">
          <Button type="button" variant="glass" onClick={onClose} disabled={saving}>
            Cancelar
          </Button>
          <Button type="submit" variant="primary" disabled={saving || !role}>
            {saving ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
