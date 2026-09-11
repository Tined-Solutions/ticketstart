import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import { useManagementEvent } from '../hooks/useManagementEvent.js'
import { useDialog } from '../hooks/useDialog.js'
import Button from './Button.jsx'

// ATE-009: validation mirrors the backend (ATE-004).
const MAX_NAME_LENGTH = 100
const MAX_QUANTITY = 1000

function toRow(ticketType) {
  return {
    key: ticketType.id,
    id: ticketType.id,
    name: ticketType.name,
    price: String(ticketType.price),
    quantity: String(ticketType.quantity),
  }
}

/**
 * ATE-009: admin full-edit modal for the ticket types of a pre-approval event.
 *
 * Loads through `useManagementEvent` — NOT `useEvent`, which returns 404 for any
 * non-Approved event — lets the admin add/edit/delete rows locally, and submits
 * the COMPLETE list in one atomic PUT. On success it invalidates the management,
 * public-detail and catalog caches (managementEvent/event/events) so both admin
 * and buyer views refresh. A 409 ticket-types-referenced explains that add-only
 * remains available.
 */
export default function EditTicketsModal({ eventId, eventName, onClose, onSuccess }) {
  const queryClient = useQueryClient()
  const { data: eventData, isLoading } = useManagementEvent(eventId)

  const [rows, setRows] = useState([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [formErrors, setFormErrors] = useState({})
  const initialised = useRef(false)
  const newRowSeq = useRef(0)

  const dialogRef = useDialog({ onClose })

  // Seed the local editable rows from the management payload exactly once.
  useEffect(() => {
    if (initialised.current || !eventData?.ticketTypes) return
    initialised.current = true
    setRows(eventData.ticketTypes.map(toRow))
  }, [eventData])

  const clearError = (field) => {
    setFormErrors((prev) => {
      if (!(field in prev)) return prev
      const next = { ...prev }
      delete next[field]
      return next
    })
  }

  const updateRow = (index, field, value) => {
    setRows((prev) => prev.map((row, i) => (i === index ? { ...row, [field]: value } : row)))
    clearError(`${field}-${index}`)
  }

  const addRow = () => {
    newRowSeq.current += 1
    setRows((prev) => [
      ...prev,
      { key: `new-${newRowSeq.current}`, id: null, name: '', price: '', quantity: '' },
    ])
  }

  const removeRow = (index) => {
    setRows((prev) => prev.filter((_, i) => i !== index))
    setFormErrors({})
  }

  const fieldId = (field, index) => `etm-${field}-${index}`

  function validate() {
    const errors = {}

    if (rows.length === 0) {
      errors.list = 'Debes tener al menos un tipo de entrada'
      return errors
    }

    rows.forEach((row, index) => {
      const name = row.name.trim()
      const price = Number(row.price)
      const quantity = Number(row.quantity)

      if (!name) {
        errors[`name-${index}`] = 'El nombre es obligatorio'
      } else if (name.length > MAX_NAME_LENGTH) {
        errors[`name-${index}`] = `El nombre no puede superar los ${MAX_NAME_LENGTH} caracteres`
      }

      if (row.price === '' || !Number.isFinite(price) || price < 0) {
        errors[`price-${index}`] = 'El precio debe ser mayor o igual a 0'
      }

      if (
        row.quantity === '' ||
        !Number.isInteger(quantity) ||
        quantity <= 0 ||
        quantity > MAX_QUANTITY
      ) {
        errors[`quantity-${index}`] = `La cantidad debe ser un entero entre 1 y ${MAX_QUANTITY}`
      }
    })

    return errors
  }

  function focusFirstError(errors) {
    if (errors.list) {
      document.getElementById('etm-list-error')?.focus()
      return
    }

    for (let index = 0; index < rows.length; index += 1) {
      for (const field of ['name', 'price', 'quantity']) {
        if (!errors[`${field}-${index}`]) continue
        document.getElementById(fieldId(field, index))?.focus()
        return
      }
    }
  }

  async function handleSubmit(event) {
    event.preventDefault()
    if (busy) return

    const errors = validate()
    setFormErrors(errors)
    if (Object.keys(errors).length > 0) {
      focusFirstError(errors)
      return
    }

    setBusy(true)
    setError('')

    try {
      const ticketTypes = rows.map((row) => {
        const item = {
          name: row.name.trim(),
          price: Number(row.price),
          quantity: Number(row.quantity),
        }
        if (row.id) item.id = row.id
        return item
      })

      await apiClient.put(`/admin/events/${eventId}/ticket-types`, { ticketTypes })

      // ATE-009/ATS-006/007: refresh the management, public-detail and catalog caches.
      queryClient.invalidateQueries({ queryKey: queryKeys.managementEvent(eventId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.events })
      onSuccess()
    } catch (err) {
      const data = err?.response?.data
      if (err?.response?.status === 409 && data?.type === 'ticket-types-referenced') {
        setError(
          'Este evento ya tiene ventas o reservas: no se puede editar la lista completa. Todavía podés agregar entradas con "Agregar entradas".'
        )
      } else {
        setError(getErrorMessage(err))
      }
      setBusy(false)
    }
  }

  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5 overscroll-contain"
      role="dialog"
      aria-modal="true"
      aria-labelledby="edit-tickets-title"
    >
      <div className="glass-surface p-8 sm:p-6 max-w-2xl sm:max-w-3xl w-full shadow-xl text-left rounded-[--radius-glass] max-h-[90vh] overflow-y-auto">
        <h2 id="edit-tickets-title" className="text-xl sm:text-lg font-display font-semibold text-text-1 mb-1">
          Editar entradas
        </h2>
        <p className="text-text-2 sm:text-sm mb-6 sm:mb-4 leading-relaxed">{eventName}</p>

        {isLoading ? (
          <p className="text-text-2 text-sm" role="status">
            Cargando tipos de entrada...
          </p>
        ) : (
          <form onSubmit={handleSubmit} noValidate>
            {formErrors.list && (
              <span id="etm-list-error" tabIndex={-1} className="form-error" role="alert">
                {formErrors.list}
              </span>
            )}

            <div className="flex flex-col gap-4 sm:gap-3">
              {rows.map((row, index) => (
                <div
                  key={row.key}
                  className="grid grid-cols-1 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_auto] gap-3 items-start border-b border-border pb-4 sm:pb-3"
                >
                  <div className="form-group min-w-0 sm:mb-0">
                    <label htmlFor={fieldId('name', index)}>Nombre</label>
                    <input
                      id={fieldId('name', index)}
                      type="text"
                      className="w-full"
                      value={row.name}
                      aria-label={`Nombre de la entrada ${index + 1}`}
                      onChange={(e) => updateRow(index, 'name', e.target.value)}
                      disabled={busy}
                      aria-invalid={formErrors[`name-${index}`] ? 'true' : undefined}
                      aria-describedby={
                        formErrors[`name-${index}`] ? `${fieldId('name', index)}-error` : undefined
                      }
                    />
                    {formErrors[`name-${index}`] && (
                      <span id={`${fieldId('name', index)}-error`} className="form-error" role="alert">
                        {formErrors[`name-${index}`]}
                      </span>
                    )}
                  </div>

                  <div className="form-group min-w-0 sm:mb-0">
                    <label htmlFor={fieldId('price', index)}>Precio ($)</label>
                    <input
                      id={fieldId('price', index)}
                      type="number"
                      className="w-full"
                      min="0"
                      step="0.01"
                      value={row.price}
                      aria-label={`Precio de la entrada ${index + 1}`}
                      onChange={(e) => updateRow(index, 'price', e.target.value)}
                      disabled={busy}
                      aria-invalid={formErrors[`price-${index}`] ? 'true' : undefined}
                      aria-describedby={
                        formErrors[`price-${index}`] ? `${fieldId('price', index)}-error` : undefined
                      }
                    />
                    {formErrors[`price-${index}`] && (
                      <span id={`${fieldId('price', index)}-error`} className="form-error" role="alert">
                        {formErrors[`price-${index}`]}
                      </span>
                    )}
                  </div>

                  <div className="form-group min-w-0 sm:mb-0">
                    <label htmlFor={fieldId('quantity', index)}>Cantidad</label>
                    <input
                      id={fieldId('quantity', index)}
                      type="number"
                      className="w-full"
                      min="1"
                      step="1"
                      value={row.quantity}
                      aria-label={`Cantidad de la entrada ${index + 1}`}
                      onChange={(e) => updateRow(index, 'quantity', e.target.value)}
                      disabled={busy}
                      aria-invalid={formErrors[`quantity-${index}`] ? 'true' : undefined}
                      aria-describedby={
                        formErrors[`quantity-${index}`]
                          ? `${fieldId('quantity', index)}-error`
                          : undefined
                      }
                    />
                    {formErrors[`quantity-${index}`] && (
                      <span
                        id={`${fieldId('quantity', index)}-error`}
                        className="form-error"
                        role="alert"
                      >
                        {formErrors[`quantity-${index}`]}
                      </span>
                    )}
                  </div>

                  <div className="form-group justify-self-end sm:mb-0 sm:self-center">
                    <Button
                      variant="danger"
                      size="sm"
                      onClick={() => removeRow(index)}
                      disabled={busy}
                      aria-label={`Eliminar ${row.name || `entrada ${index + 1}`}`}
                    >
                      Eliminar
                    </Button>
                  </div>
                </div>
              ))}
            </div>

            <Button variant="secondary" size="sm" onClick={addRow} disabled={busy} className="mt-4 sm:mt-3">
              Agregar tipo de entrada
            </Button>

            {error && (
              <div className="error-container" role="alert">
                <p>{error}</p>
              </div>
            )}

            <div className="flex gap-3 justify-end mt-6 sm:mt-4">
              <Button variant="secondary" onClick={onClose} disabled={busy}>
                Cancelar
              </Button>
              <Button type="submit" variant="primary" disabled={busy} className="min-h-[44px]">
                {busy ? 'Guardando…' : 'Guardar'}
              </Button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
