import Button from './Button.jsx'
import { useDialog } from '../hooks/useDialog.js'

/**
 * Shared delete-confirmation dialog (glass surface, focus trap via useDialog).
 * Used by AdminPanel and OrganizerDashboard; the only variable is the event
 * name shown in the body copy. ESC / backdrop focus behavior is provided by
 * the useDialog hook.
 */
function DeleteConfirmationDialog({ eventName, onConfirm, onCancel, deleting }) {
  const dialogRef = useDialog({ onClose: onCancel })
  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5 overscroll-contain"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-dialog-title"
    >
      <div className="glass-surface p-8 max-w-md w-full shadow-xl text-left rounded-[--radius-glass]">
        <h2 id="delete-dialog-title" className="text-xl font-display font-semibold text-text-1 mb-3">
          Confirmar Eliminación
        </h2>
        <p className="text-text-2 mb-6 leading-relaxed">
          Estas seguro que deseas eliminar el evento <strong>{eventName}</strong>?
          Esta accion no se puede deshacer.
        </p>
        <div className="flex gap-3 justify-end">
          <Button variant="secondary" onClick={onCancel} disabled={deleting} className="min-h-[44px]">
            Cancelar
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={deleting} className="min-h-[44px]">
            {deleting ? 'Eliminando…' : 'Eliminar'}
          </Button>
        </div>
      </div>
    </div>
  )
}

export default DeleteConfirmationDialog
