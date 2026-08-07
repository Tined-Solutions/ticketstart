import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import { useEvent } from '../hooks/useEvent.js'
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

  const quantityNum = Number(additionalQuantity)
  const isIncreaseValid =
    selectedTypeId !== '' &&
    additionalQuantity !== '' &&
    Number.isInteger(quantityNum) &&
    quantityNum > 0

  const priceNum = Number(newPrice)
  const newQtyNum = Number(newQuantity)
  const isNewTypeValid =
    newName.trim() !== '' &&
    newPrice !== '' &&
    !Number.isNaN(priceNum) &&
    priceNum > 0 &&
    newQuantity !== '' &&
    Number.isInteger(newQtyNum) &&
    newQtyNum > 0

  const isValid = mode === 'increase' ? isIncreaseValid : isNewTypeValid

  async function handleSubmit(event) {
    event.preventDefault()
    if (busy || !isValid) return

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

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5"
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
          >
            Sumar stock
          </Button>
          <Button
            variant={mode === 'newType' ? 'primary' : 'secondary'}
            size="sm"
            onClick={() => setMode('newType')}
            disabled={busy}
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
                    onChange={(e) => setSelectedTypeId(e.target.value)}
                    disabled={busy}
                  >
                    <option value="">Seleccionar tipo</option>
                    {ticketTypes.map((tt) => (
                      <option key={tt.id} value={tt.id}>
                        {tt.name} — {tt.available ?? tt.quantity} disponibles de {tt.quantity}
                      </option>
                    ))}
                  </select>
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
                  onChange={(e) => setAdditionalQuantity(e.target.value)}
                  placeholder="Ej: 50"
                  disabled={busy}
                  aria-invalid={additionalQuantity !== '' && !isIncreaseValid ? 'true' : undefined}
                />
                {additionalQuantity !== '' && !isIncreaseValid && (
                  <span className="form-error">
                    La cantidad debe ser un numero entero mayor a 0
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
                  onChange={(e) => setNewName(e.target.value)}
                  placeholder="Ej: VIP, Platea alta"
                  disabled={busy}
                />
              </div>

              <div className="form-group">
                <label htmlFor="ats-new-price">Precio ($)</label>
                <input
                  id="ats-new-price"
                  type="number"
                  min="0"
                  step="0.01"
                  value={newPrice}
                  onChange={(e) => setNewPrice(e.target.value)}
                  placeholder="15000"
                  disabled={busy}
                  aria-invalid={newPrice !== '' && (Number.isNaN(priceNum) || priceNum <= 0) ? 'true' : undefined}
                />
                {newPrice !== '' && (Number.isNaN(priceNum) || priceNum <= 0) && (
                  <span className="form-error">El precio debe ser mayor a 0</span>
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
                  onChange={(e) => setNewQuantity(e.target.value)}
                  placeholder="Ej: 100"
                  disabled={busy}
                  aria-invalid={newQuantity !== '' && !isNewTypeValid ? 'true' : undefined}
                />
                {newQuantity !== '' && !isNewTypeValid && (
                  <span className="form-error">
                    La cantidad debe ser un numero entero mayor a 0
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
            <Button type="submit" variant="primary" disabled={busy || !isValid}>
              {busy ? 'Guardando...' : 'Guardar'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
