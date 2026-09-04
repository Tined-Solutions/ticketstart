import { useState } from 'react'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { formatCurrency, toDateTimeLocalValue } from '../lib/format.js'

let ticketTypeCounter = 0
function nextTicketTypeKey() {
  ticketTypeCounter += 1
  return `tt-new-${ticketTypeCounter}`
}

function emptyTicketType() {
  return { key: nextTicketTypeKey(), name: '', price: '', quantity: '' }
}

export default function EventForm({
  initialData,
  mode,
  onSuccess,
  // D-6: read-only consultation mode — disables every input, hides the submit
  // button and the image upload control (preview stays). Used by
  // EventReadOnlyView for past/finalized events.
  readOnly = false,
}) {
  const [name, setName] = useState(initialData?.name || '')
  const [date, setDate] = useState(toDateTimeLocalValue(initialData?.date) || '')
  const [location, setLocation] = useState(initialData?.location || '')
  const [description, setDescription] = useState(initialData?.description || '')
  const [imageFile, setImageFile] = useState(null)
  const [imagePreview, setImagePreview] = useState(initialData?.imageUrl || '')
  const [ticketTypes, setTicketTypes] = useState(() => {
    if (initialData?.ticketTypes?.length) {
      return initialData.ticketTypes.map((tt) => ({
        key: tt.id || nextTicketTypeKey(),
        name: tt.name || '',
        price: tt.price != null ? String(tt.price) : '',
        quantity: tt.quantity != null ? String(tt.quantity) : '',
      }))
    }
    return [emptyTicketType()]
  })
  const [errors, setErrors] = useState({})
  // EIM-003/004: two-phase submit — '' | 'uploading' | 'saving'. The upload
  // happens BEFORE the event save and blocks it: a failed upload never reaches
  // POST /events / PUT /events/{id} and shows an honest red error instead of the
  // old green false-success.
  const [phase, setPhase] = useState('')
  const submitting = phase !== ''
  const [feedback, setFeedback] = useState({ type: '', message: '' })

  const isCreate = mode === 'create'

  function validate() {
    const newErrors = {}

    if (!name.trim()) {
      newErrors.name = 'El nombre del evento es obligatorio'
    }

    if (!date) {
      newErrors.date = 'La fecha es obligatoria'
    }

    if (!location.trim()) {
      newErrors.location = 'La ubicacion es obligatoria'
    }

    const ticketErrors = []
    let hasTicketError = false

    // Ticket types are only validated in create mode: edit mode hides the fieldset
    // and the PUT body omits ticket types (D-2 / ATS-008 — no silent no-op).
    if (isCreate) {
      for (let i = 0; i < ticketTypes.length; i++) {
        const tt = ticketTypes[i]
        const rowErrors = {}

        if (!tt.name.trim()) {
          rowErrors.name = 'El nombre es obligatorio'
          hasTicketError = true
        }

        const priceNum = Number(tt.price)
        if (tt.price === '' || Number.isNaN(priceNum)) {
          rowErrors.price = 'El precio es obligatorio'
          hasTicketError = true
        } else if (priceNum <= 0) {
          rowErrors.price = 'El precio debe ser mayor a 0'
          hasTicketError = true
        }

        const quantityNum = Number(tt.quantity)
        if (tt.quantity === '' || Number.isNaN(quantityNum)) {
          rowErrors.quantity = 'La cantidad es obligatoria'
          hasTicketError = true
        } else if (!Number.isInteger(quantityNum) || quantityNum <= 0) {
          rowErrors.quantity = 'La cantidad debe ser un numero entero mayor a 0'
          hasTicketError = true
        }

        ticketErrors.push(rowErrors)
      }

      if (hasTicketError) {
        newErrors.ticketTypes = ticketErrors
      }

      if (ticketTypes.length === 0) {
        newErrors.ticketTypes = 'Debe agregar al menos un tipo de entrada'
      }
    }

    return newErrors
  }

  async function handleSubmit(event) {
    event.preventDefault()
    setFeedback({ type: '', message: '' })

    const validationErrors = validate()
    setErrors(validationErrors)

    if (Object.keys(validationErrors).length > 0) {
      return
    }

    const payload = {
      name: name.trim(),
      date: new Date(date).toISOString(),
      location: location.trim(),
      description: description.trim(),
      ticketTypes: ticketTypes.map((tt) => ({
        name: tt.name.trim(),
        price: Number(tt.price),
        quantity: Number(tt.quantity),
      })),
    }

    let eventId = initialData?.id

    try {
      // EIM-004: upload FIRST — a failed upload blocks the save in both modes.
      // No manual multipart Content-Type header: axios sets the boundary itself.
      let uploadedUrl = null
      if (imageFile) {
        setPhase('uploading')
        const formData = new FormData()
        formData.append('image', imageFile)
        const uploadResponse = await apiClient.post('/uploads/event-image', formData)
        uploadedUrl = uploadResponse.data.imageUrl
      }

      setPhase('saving')

      if (isCreate) {
        const response = await apiClient.post('/events', {
          ...payload,
          // CreateEventRequest.ImageUrl is non-nullable — no photo means ""
          imageUrl: uploadedUrl || '',
        })
        eventId = response.data.id
        // EA-009: new events start Pending — communicate the moderation gate
        setFeedback({ type: 'success', message: 'Evento creado correctamente. Queda pendiente de aprobacion.' })
      } else {
        if (!eventId) {
          setFeedback({ type: 'error', message: 'No se pudo identificar el evento para actualizar' })
          return
        }
        await apiClient.put(`/events/${eventId}`, {
          name: payload.name,
          date: payload.date,
          location: payload.location,
          description: payload.description,
          // EIM-004/005: a fresh upload wins; without one the CURRENT URL is
          // re-sent so a text-only edit never wipes the image (old ≠ new guard).
          imageUrl: uploadedUrl ?? (initialData?.imageUrl || ''),
        })
        setFeedback({ type: 'success', message: 'Evento actualizado correctamente' })
      }

      if (onSuccess) {
        onSuccess(eventId)
      }
    } catch (error) {
      setFeedback({ type: 'error', message: getErrorMessage(error) })
    } finally {
      setPhase('')
    }
  }

  function handleImageChange(event) {
    const file = event.target.files?.[0]
    if (!file) {
      setImageFile(null)
      setImagePreview(initialData?.imageUrl || '')
      return
    }

    // Validate file type and size
    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp']
    if (!allowedTypes.includes(file.type)) {
      setFeedback({
        type: 'error',
        message: 'Formato de imagen no valido. Use JPEG, PNG o WebP.',
      })
      event.target.value = ''
      return
    }

    if (file.size > 5 * 1024 * 1024) {
      setFeedback({
        type: 'error',
        message: 'La imagen no debe superar los 5 MB.',
      })
      event.target.value = ''
      return
    }

    setImageFile(file)
    setImagePreview(URL.createObjectURL(file))
  }

  function handleTicketTypeChange(index, field, value) {
    setTicketTypes((prev) => {
      const updated = [...prev]
      updated[index] = { ...updated[index], [field]: value }
      return updated
    })
  }

  function handleAddTicketType() {
    setTicketTypes((prev) => [...prev, emptyTicketType()])
  }

  function handleRemoveTicketType(index) {
    setTicketTypes((prev) => prev.filter((_, i) => i !== index))
  }

  return (
    <form onSubmit={handleSubmit} className="event-form" noValidate>
      {feedback.message && (
        <div
          className={`feedback-message feedback-message--${feedback.type}`}
          role={feedback.type === 'error' ? 'alert' : 'status'}
        >
          {feedback.message}
        </div>
      )}

      <div className="form-group">
        <label htmlFor="eventName">Nombre del evento</label>
        <input
          id="eventName"
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          disabled={submitting || readOnly}
          aria-invalid={errors.name ? 'true' : undefined}
          aria-describedby={errors.name ? 'eventName-error' : undefined}
        />
        {errors.name && (
          <span id="eventName-error" className="form-error">
            {errors.name}
          </span>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="eventDate">Fecha y hora</label>
        <input
          id="eventDate"
          type="datetime-local"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          required
          disabled={submitting || readOnly}
          aria-invalid={errors.date ? 'true' : undefined}
          aria-describedby={errors.date ? 'eventDate-error' : undefined}
        />
        {errors.date && (
          <span id="eventDate-error" className="form-error">
            {errors.date}
          </span>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="eventLocation">Ubicacion</label>
        <input
          id="eventLocation"
          type="text"
          value={location}
          onChange={(e) => setLocation(e.target.value)}
          required
          disabled={submitting || readOnly}
          aria-invalid={errors.location ? 'true' : undefined}
          aria-describedby={errors.location ? 'eventLocation-error' : undefined}
        />
        {errors.location && (
          <span id="eventLocation-error" className="form-error">
            {errors.location}
          </span>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="eventDescription">Descripcion</label>
        <textarea
          id="eventDescription"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={4}
          disabled={submitting || readOnly}
          style={{
            padding: '10px 12px',
            border: '1px solid var(--border)',
            borderRadius: '6px',
            fontSize: '16px',
            fontFamily: 'inherit',
            background: 'var(--bg)',
            color: 'var(--text-h)',
            resize: 'vertical',
          }}
        />
      </div>

      <div className="form-group">
        <label htmlFor="eventImage">Imagen del evento (opcional)</label>
        {!readOnly && (
          <>
            <input
              id="eventImage"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              onChange={handleImageChange}
              disabled={submitting}
              aria-describedby="eventImage-hint"
            />
            <small id="eventImage-hint" style={{ color: 'var(--text)', fontSize: '13px' }}>
              Formatos: JPEG, PNG, WebP. Maximo 5 MB.
            </small>
          </>
        )}
        {imagePreview && (
          <div style={{ marginTop: '8px' }}>
            <img
              src={imagePreview}
              alt="Vista previa"
              style={{
                maxWidth: '200px',
                maxHeight: '150px',
                borderRadius: '8px',
                border: '1px solid var(--border)',
                objectFit: 'cover',
              }}
            />
          </div>
        )}
      </div>

      {isCreate ? (
        <fieldset className="ticket-types-section" disabled={submitting || readOnly}>
          <legend>
            <h2>Tipos de entrada</h2>
          </legend>

          {typeof errors.ticketTypes === 'string' && (
            <div className="error-container" role="alert">
              <p>{errors.ticketTypes}</p>
            </div>
          )}

          {ticketTypes.map((tt, index) => (
            <div key={tt.key} className="ticket-type-row">
              <div className="ticket-type-fields">
                <div className="form-group ticket-type-name-group">
                  <label htmlFor={`tt-name-${tt.key}`}>Nombre</label>
                  <input
                    id={`tt-name-${tt.key}`}
                    type="text"
                    value={tt.name}
                    onChange={(e) =>
                      handleTicketTypeChange(index, 'name', e.target.value)
                    }
                    placeholder="Ej: General, VIP"
                    required
                    disabled={submitting}
                    aria-invalid={
                      errors.ticketTypes?.[index]?.name ? 'true' : undefined
                    }
                  />
                  {errors.ticketTypes?.[index]?.name && (
                    <span className="form-error">
                      {errors.ticketTypes[index].name}
                    </span>
                  )}
                </div>

                <div className="form-group ticket-type-price-group">
                  <label htmlFor={`tt-price-${tt.key}`}>Precio ($)</label>
                  <input
                    id={`tt-price-${tt.key}`}
                    type="number"
                    value={tt.price}
                    onChange={(e) =>
                      handleTicketTypeChange(index, 'price', e.target.value)
                    }
                    placeholder="15000"
                    min="0"
                    step="0.01"
                    required
                    disabled={submitting}
                    aria-invalid={
                      errors.ticketTypes?.[index]?.price ? 'true' : undefined
                    }
                  />
                  {errors.ticketTypes?.[index]?.price && (
                    <span className="form-error">
                      {errors.ticketTypes[index].price}
                    </span>
                  )}
                </div>

                <div className="form-group ticket-type-quantity-group">
                  <label htmlFor={`tt-quantity-${tt.key}`}>Cantidad</label>
                  <input
                    id={`tt-quantity-${tt.key}`}
                    type="number"
                    value={tt.quantity}
                    onChange={(e) =>
                      handleTicketTypeChange(index, 'quantity', e.target.value)
                    }
                    placeholder="100"
                    min="1"
                    step="1"
                    required
                    disabled={submitting}
                    aria-invalid={
                      errors.ticketTypes?.[index]?.quantity ? 'true' : undefined
                    }
                  />
                  {errors.ticketTypes?.[index]?.quantity && (
                    <span className="form-error">
                      {errors.ticketTypes[index].quantity}
                    </span>
                  )}
                </div>
              </div>

              {ticketTypes.length > 1 && (
                <button
                  type="button"
                  className="button-secondary ticket-type-remove"
                  onClick={() => handleRemoveTicketType(index)}
                  disabled={submitting}
                  aria-label={`Eliminar tipo de entrada ${tt.name || index + 1}`}
                >
                  Eliminar
                </button>
              )}
            </div>
          ))}

          <button
            type="button"
            className="button-secondary ticket-type-add"
            onClick={handleAddTicketType}
            disabled={submitting}
          >
            + Agregar tipo de entrada
          </button>
        </fieldset>
      ) : readOnly ? (
        // Read-only preview (moderation review / past-event consultation):
        // show EVERYTHING the organizer entered — prices are public catalog
        // content, so the reviewer must see them before approving.
        <div className="ticket-types-section">
          <h2>Tipos de entrada</h2>
          {ticketTypes.filter((tt) => tt.name.trim()).length === 0 ? (
            <p>El organizador no cargo tipos de entrada.</p>
          ) : (
            <ul className="ticket-types-preview">
              {ticketTypes
                .filter((tt) => tt.name.trim())
                .map((tt) => (
                  <li key={tt.key}>
                    {tt.name} — {formatCurrency(tt.price)} — {tt.quantity} entradas
                  </li>
                ))}
            </ul>
          )}
        </div>
      ) : (
        <div className="ticket-types-section" role="note">
          <h2>Tipos de entrada</h2>
          <p>
            El stock de entradas se gestiona desde el panel de administracion
            (accion &quot;Agregar entradas&quot;). Los tipos de entrada no se
            editan aqui.
          </p>
        </div>
      )}

      {!readOnly && (
        <div className="form-actions">
          <button
            type="submit"
            className="button-primary"
            disabled={submitting}
          >
            {submitting
              ? phase === 'uploading'
                ? 'Subiendo imagen…'
                : 'Guardando…'
              : isCreate
                ? 'Crear evento'
                : 'Guardar cambios'}
          </button>
        </div>
      )}
    </form>
  )
}
