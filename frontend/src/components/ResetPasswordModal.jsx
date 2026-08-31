import { useState } from 'react'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import Modal from './Modal.jsx'
import Button from './Button.jsx'

/**
 * Password-reset modal (AUM-003 / AUM-005, D14): two steps — confirm, then the
 * one-time credential. The temporary password is displayed EXACTLY ONCE with a
 * copy affordance; it lives in React state only and is cleared when the modal
 * closes, so it is not retrievable from the UI afterwards. It is never
 * persisted to storage.
 */
export default function ResetPasswordModal({ user, onClose }) {
  const [resetting, setResetting] = useState(false)
  const [error, setError] = useState('')
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const [copied, setCopied] = useState(false)

  const handleConfirm = async () => {
    if (!user) return

    setResetting(true)
    setError('')
    try {
      const response = await apiClient.post(`/admin/users/${user.id}/reset-password`)
      setTemporaryPassword(response.data?.temporaryPassword ?? '')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setResetting(false)
    }
  }

  const handleCopy = async () => {
    if (!temporaryPassword) return
    await navigator.clipboard.writeText(temporaryPassword)
    setCopied(true)
  }

  const handleClose = () => {
    // D14: clearing here (together with the parent unmounting the modal)
    // guarantees the credential is not retrievable after closing.
    setTemporaryPassword('')
    onClose?.()
  }

  const hasCredential = temporaryPassword !== ''

  return (
    <Modal
      open
      onClose={handleClose}
      title={hasCredential ? 'Contraseña temporal generada' : 'Restablecer contraseña'}
    >
      {hasCredential ? (
        <div>
          <p className="text-sm text-text-2 mb-4">
            Entrega esta contraseña a <span className="font-medium text-text-1">{user?.email}</span>.
            Por seguridad, <strong>no se volverá a mostrar</strong>.
          </p>

          <div
            className="flex items-center justify-between gap-3 bg-black/5 rounded-lg px-4 py-3 mb-4"
            data-testid="one-time-credential"
          >
            <code className="font-mono text-base text-text-1 break-all">{temporaryPassword}</code>
            <Button type="button" variant="glass" size="sm" onClick={handleCopy} aria-label="Copiar contraseña temporal">
              {copied ? '¡Copiada!' : 'Copiar'}
            </Button>
          </div>

          <div className="flex justify-end">
            <Button type="button" variant="primary" onClick={handleClose}>
              Entendido
            </Button>
          </div>
        </div>
      ) : (
        <div>
          {error && (
            <div className="bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30 rounded-lg py-3 px-4 mb-4 font-medium" role="alert">
              {error}
            </div>
          )}

          <p className="text-sm text-text-2 mb-4">
            Se generará una contraseña temporal para <span className="font-medium text-text-1">{user?.email}</span>. El
            usuario deberá iniciar sesión con ella y luego cambiarla.
          </p>

          <div className="flex justify-end gap-3">
            <Button type="button" variant="glass" onClick={handleClose} disabled={resetting}>
              Cancelar
            </Button>
            <Button type="button" variant="primary" onClick={handleConfirm} disabled={resetting}>
              {resetting ? 'Generando…' : 'Generar contraseña temporal'}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  )
}
