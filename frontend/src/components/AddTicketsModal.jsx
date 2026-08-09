import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import { useEvent } from '../hooks/useEvent.js'
import { useDialog } from '../hooks/useDialog.js'
import Button from './Button.jsx'

/**
 * Modal for admins to add ticket capacity to an existing event.
 * Two modes (D-8):
 *  - 'increase': increment the Quantity of an existing ticket type.
 *  - 'newType':  create a new ticket type (different zone/price).
 * On success invalidates ['event', id] + ['events'] so buyer EventDetail/catalog
 * refetch, then calls onSuccess() so AdminPanel refreshes its manual list (ATS-006/007).
 */
export default function AddTicketsModal({ eventId, eventName, onClose, onSuccess }) {
  const queryClient = useQueryClient()
  const { data: eventData, isLoading: eventLoading } = useEvent(eventId)
  const ticketTypes = eventData?.ticketTypes || []

  const [mode, setMode] = useState('increase')
  const [selectedTypeId, setSelectedTypeId] = useState('')
  const [additionalQuantity, setAdditionalQuantity] = useState('')
  const [newName, setNewName] = useState('')
  const [newPrice, setNewPrice] = useState('')
  const [newQuantity, setNewQuantity] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [formErrors, setFormErrors] = useState({})

  const dialogRef = useDialog({ onClose })

  const quantityNum = Number(additionalQuantity)
  const priceNum = Number(newPrice)
  const newQtyNum = Number(newQuantity)

  const clearError = (field) => {
    setFormErrors((prev) => {
      if (!(field in prev)) return prev
      const next = { ...prev }
      delete next[field]
      return next
    })
  }

  function validate() {
    const errors = {}

    if (mode === 'increase') {
      if (!selectedTypeId) {
        errors.ticketType = 'Debes seleccionar un tipo de entrada'
      }
      if (additionalQuantity === '' || !Number.isInteger(quantityNum) || quantityNum <= 0) {
        errors.additionalQuantity = 'La cantidad debe ser un número entero mayor a 0'
      }
    } else {
      if (!newName.trim()) {
        errors.newName = 'El nombre es obligatorio'
      }
      if (newPrice === '' || Number.isNaN(priceNum) || priceNum <= 0) {
        errors.newPrice = 'El precio debe ser mayor a 0'
      }
      if (newQuantity === '' || !Number.isInteger(newQtyNum) || newQtyNum <= 0) {
        errors.newQuantity = 'La cantidad debe ser un número entero mayor a 0'
      }
    }

    return errors
  }

  const ERROR_ID = {
    ticketType: 'ats-ticket-type',
    additionalQuantity: 'ats-additional-qty',
    newName: 'ats-new-name',
    newPrice: 'ats-new-price',
    newQuantity: 'ats-new-qty',
  }

  function focusFirstError(errors) {
    const fieldOrder = mode === 'increase'
      ? ['ticketType', 'additionalQuantity']
      : ['newName', 'newPrice', 'newQuantity']

    for (const key of fieldOrder) {
      if (!errors[key]) continue
      const el = document.getElementById(ERROR_ID[key])
      if (el) {
        el.focus()
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
      if (mode === 'increase') {
        await apiClient.post(
          `/admin/events/${eventId}/ticket-types/${selectedTypeId}/stock`,
          { additionalQuantity: quantityNum }
        )
      } else {
        await apiClient.post(`/admin/events/${eventId}/ticket-types`, {
          name: newName.trim(),
          price: priceNum,
          quantity: newQtyNum,
        })
      }

      // ATS-007: success invalidates buyer-facing queries, then refreshes the admin list.
      queryClient.invalidateQueries({ queryKey: queryKeys.event(eventId) })
      queryClient.invalidateQueries({ queryKey: queryKeys.events })
      onSuccess()
    } catch (err) {
      // ATS-007: failure shows error inline; local state is left untouched.
      setError(getErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  const increaseQtyInvalid = additionalQuantity !== '' && (!Number.isInteger(quantityNum) || quantityNum <= 0)
  const newPriceInvalid = newPrice !== '' && (Number.isNaN(priceNum) || priceNum <= 0)
  const newQtyInvalid = newQuantity !== '' && (!Number.isInteger(newQtyNum) || newQtyNum <= 0)

  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5 overscroll-contain"
      role="dialog"
      aria-modal="true"
      aria-labelledby="add-tickets-title"
    >
      <div className="glass-surface p-8 max-w-md w-full shadow-xl text-left rounded-[--radius-glass]">
        <h2 id="add-tickets-title" className="text-xl font-display font-semibold text-text-1 mb-1">
          Agregar entradas
        </h2>
        <p className="text-text-2 mb-6 leading-relaxed">
          {eventName}
        </p>

        <div className="flex gap-2 mb-5">
          <Button
            variant={mode === 'increase' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setMode('increase')}
            disabled={busy}
            className="min-h-[44px]"
          >
            Sumar stock
          </Button>
          <Button
            variant={mode === 'newType' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setMode('newType')}
            disabled={busy}
            className="min-h-[44px]"
          >
            Nuevo tipo de entrada
          </Button>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          {mode === 'increase' ? (
            <>
              <div className="form-group">
                <label htmlFor="ats-ticket-type">Tipo de entrada</label>
                {eventLoading ? (
                  <p className="text-text-2 text-sm">Cargando tipos de entrada...</p>
                ) : ticketTypes.length === 0 ? (
                  <p className="text-text-2 text-sm">Este evento no tiene tipos de entrada.</p>
                ) : (
                  <select
                    id="ats-ticket-type"
                    value={selectedTypeId}
                    onChange={(e) => {
                      setSelectedTypeId(e.target.value)
                      clearError('ticketType')
                    }}
                    disabled={busy}
                    aria-invalid={formErrors.ticketType ? 'true' : undefined}
                    aria-describedby={formErrors.ticketType ? 'ats-ticket-type-error' : undefined}
                  >
                    <option value="">Seleccionar tipo</option>
                    {ticketTypes.map((tt) => (
                      <option key={tt.id} value={tt.id}>
                        {tt.name} — {tt.available ?? tt.quantity} disponibles de {tt.quantity}
                      </option>
                    ))}
                  </select>
                )}
                {formErrors.ticketType && (
                  <span id="ats-ticket-type-error" className="form-error" role="alert">
                    {formErrors.ticketType}
                  </span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="ats-additional-qty">Cantidad a sumar</label>
                <input
                  id="ats-additional-qty"
                  type="number"
                  min="1"
                  step="1"
                  value={additionalQuantity}
                  onChange={(e) => {
                    setAdditionalQuantity(e.target.value)
                    clearError('additionalQuantity')
                  }}
                  placeholder="Ej: 50"
                  disabled={busy}
                  aria-invalid={formErrors.additionalQuantity || increaseQtyInvalid ? 'true' : undefined}
                  aria-describedby={formErrors.additionalQuantity || increaseQtyInvalid ? 'ats-additional-qty-error' : undefined}
                />
                {(formErrors.additionalQuantity || increaseQtyInvalid) && (
                  <span id="ats-additional-qty-error" className="form-error" role="alert">
                    La cantidad debe ser un número entero mayor a 0
                  </span>
                )}
              </div>
            </>
          ) : (
            <>
              <div className="form-group">
                <label htmlFor="ats-new-name">Nombre</label>
                <input
                  id="ats-new-name"
                  type="text"
                  value={newName}
                  onChange={(e) => {
                    setNewName(e.target.value)
                    clearError('newName')
                  }}
                  placeholder="Ej: VIP, Platea alta"
                  disabled={busy}
                  aria-invalid={formErrors.newName ? 'true' : undefined}
                  aria-describedby={formErrors.newName ? 'ats-new-name-error' : undefined}
                />
                {formErrors.newName && (
                  <span id="ats-new-name-error" className="form-error" role="alert">
                    {formErrors.newName}
                  </span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="ats-new-price">Precio ($)</label>
                <input
                  id="ats-new-price"
                  type="number"
                  min="0"
                  step="0.01"
                  value={newPrice}
                  onChange={(e) => {
                    setNewPrice(e.target.value)
                    clearError('newPrice')
                  }}
                  placeholder="15000"
                  disabled={busy}
                  aria-invalid={formErrors.newPrice || newPriceInvalid ? 'true' : undefined}
                  aria-describedby={formErrors.newPrice || newPriceInvalid ? 'ats-new-price-error' : undefined}
                />
                {(formErrors.newPrice || newPriceInvalid) && (
                  <span id="ats-new-price-error" className="form-error" role="alert">
                    El precio debe ser mayor a 0
                  </span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="ats-new-qty">Cantidad</label>
                <input
                  id="ats-new-qty"
                  type="number"
                  min="1"
                  step="1"
                  value={newQuantity}
                  onChange={(e) => {
                    setNewQuantity(e.target.value)
                    clearError('newQuantity')
                  }}
                  placeholder="Ej: 100"
                  disabled={busy}
                  aria-invalid={formErrors.newQuantity || newQtyInvalid ? 'true' : undefined}
                  aria-describedby={formErrors.newQuantity || newQtyInvalid ? 'ats-new-qty-error' : undefined}
                />
                {(formErrors.newQuantity || newQtyInvalid) && (
                  <span id="ats-new-qty-error" className="form-error" role="alert">
                    La cantidad debe ser un número entero mayor a 0
                  </span>
                )}
              </div>
            </>
          )}

          {error && (
            <div className="error-container" role="alert">
              <p>{error}</p>
            </div>
          )}

          <div className="flex gap-3 justify-end mt-6">
            <Button variant="secondary" onClick={onClose} disabled={busy}>
              Cancelar
            </Button>
            <Button type="submit" variant="primary" disabled={busy} className="min-h-[44px]">
              {busy ? 'Guardando…' : 'Guardar'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
