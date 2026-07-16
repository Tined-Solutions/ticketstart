import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import apiClient from '../api/client.js'
import { formatEventDate, formatCurrency } from '../lib/format.js'

function TicketTypeRow({ ticketType, isSelected, quantity, onSelect, onChange }) {
  const available = ticketType.available ?? ticketType.quantity ?? 0
  const isSoldOut = available <= 0

  return (
    <div className={`ticket-type-row ${isSelected ? 'ticket-type-row-selected' : ''}`}>
      <div className="ticket-type-selector">
        <input
          type="radio"
          name="ticket-type"
          id={`ticket-type-${ticketType.id}`}
          value={ticketType.id}
          checked={isSelected}
          onChange={() => onSelect(ticketType.id)}
          disabled={isSoldOut}
          aria-label={`Seleccionar ${ticketType.name}`}
        />
      </div>
      <div className="ticket-type-info">
        <label htmlFor={`ticket-type-${ticketType.id}`}>
          <h3>{ticketType.name}</h3>
        </label>
        <p className="ticket-type-price">{formatCurrency(ticketType.price)}</p>
        <p className="ticket-type-availability">
          {isSoldOut
            ? 'Agotado'
            : `${available} disponibles de ${ticketType.quantity}`}
        </p>
      </div>
      {isSelected && (
        <div className="ticket-type-quantity">
          <button
            type="button"
            aria-label={`Disminuir cantidad de ${ticketType.name}`}
            onClick={() => onChange(Math.max(1, quantity - 1))}
            disabled={quantity <= 1}
          >
            -
          </button>
          <span aria-live="polite">{quantity}</span>
          <button
            type="button"
            aria-label={`Aumentar cantidad de ${ticketType.name}`}
            onClick={() => onChange(Math.min(available, quantity + 1))}
            disabled={quantity >= available}
          >
            +
          </button>
        </div>
      )}
    </div>
  )
}

export default function EventDetail() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [event, setEvent] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [selectedTicketTypeId, setSelectedTicketTypeId] = useState(null)
  const [quantity, setQuantity] = useState(0)

  const loadEvent = useCallback(
    (controller) => {
      apiClient
        .get(`/events/${id}`, { signal: controller.signal })
        .then((response) => {
          if (controller.signal.aborted) return
          const eventData = response.data
          setEvent(eventData)
          setSelectedTicketTypeId(null)
          setQuantity(0)
          setError('')
          setLoading(false)
        })
        .catch((error) => {
          if (controller.signal.aborted) return
          if (error.response?.status === 404) {
            setError('El evento no existe o no esta disponible')
          } else {
            const message =
              error.response?.data?.error?.message ||
              error.response?.data?.message ||
              'Ocurrio un error al cargar el evento'
            setError(message)
          }
          setLoading(false)
        })
    },
    [id]
  )

  useEffect(() => {
    const controller = new AbortController()
    loadEvent(controller)
    return () => controller.abort()
  }, [loadEvent])

  const handleRetry = () => {
    setLoading(true)
    setError('')
    const controller = new AbortController()
    loadEvent(controller)
  }

  const handleSelectTicketType = (ticketTypeId) => {
    setSelectedTicketTypeId(ticketTypeId)
    setQuantity((prev) => (prev > 0 ? prev : 1))
  }

  const updateQuantity = (nextQuantity) => {
    setQuantity(nextQuantity)
  }

  const selectedTicketType = event?.ticketTypes?.find(
    (ticketType) => ticketType.id === selectedTicketTypeId
  )

  const totalTickets = selectedTicketTypeId ? quantity : 0
  const totalPrice = selectedTicketType
    ? quantity * (selectedTicketType.price || 0)
    : 0

  const handleReserve = () => {
    if (!selectedTicketType || quantity === 0) return

    navigate('/checkout', {
      state: {
        eventId: event.id,
        eventName: event.name,
        eventDate: event.date,
        eventLocation: event.location,
        eventImageUrl: event.imageUrl,
        selection: {
          ticketTypeId: selectedTicketType.id,
          name: selectedTicketType.name,
          price: selectedTicketType.price,
          quantity,
        },
        totalTickets,
        totalPrice,
      },
    })
  }

  if (loading) {
    return (
      <div className="event-detail-page">
        <p>Cargando evento...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="event-detail-page">
        <Link to="/events" className="back-link">
          ← Volver al catalogo
        </Link>
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button type="button" onClick={handleRetry}>
            Reintentar
          </button>
        </div>
      </div>
    )
  }

  if (!event) {
    return (
      <div className="event-detail-page">
        <Link to="/events" className="back-link">
          ← Volver al catalogo
        </Link>
        <p>El evento no esta disponible.</p>
      </div>
    )
  }

  return (
    <div className="event-detail-page">
      <Link to="/events" className="back-link">
        ← Volver al catalogo
      </Link>

      <div className="event-detail-header">
        {event.imageUrl ? (
          <img
            src={event.imageUrl}
            alt={event.name}
            className="event-detail-image"
          />
        ) : (
          <div className="event-detail-image event-detail-image-placeholder">
            Sin imagen
          </div>
        )}
        <div className="event-detail-info">
          <h1>{event.name}</h1>
          <p className="event-detail-date">{formatEventDate(event.date)}</p>
          <p className="event-detail-location">{event.location}</p>
        </div>
      </div>

      <section className="event-detail-description">
        <h2>Descripcion</h2>
        <p>{event.description || 'Sin descripcion'}</p>
      </section>

      <section className="event-detail-tickets">
        <h2>Entradas</h2>
        {event.ticketTypes?.length === 0 ? (
          <p>No hay entradas disponibles para este evento.</p>
        ) : (
          <>
            <fieldset className="ticket-type-list">
              <legend className="sr-only">Selecciona un tipo de entrada</legend>
              {event.ticketTypes?.map((ticketType) => (
                <TicketTypeRow
                  key={ticketType.id}
                  ticketType={ticketType}
                  isSelected={selectedTicketTypeId === ticketType.id}
                  quantity={quantity}
                  onSelect={handleSelectTicketType}
                  onChange={updateQuantity}
                />
              ))}
            </fieldset>
            <div className="reservation-summary">
              <p>Entradas seleccionadas: {totalTickets}</p>
              <p>Total: {formatCurrency(totalPrice)}</p>
            </div>
            <button
              type="button"
              className="reserve-button"
              onClick={handleReserve}
              disabled={!selectedTicketType || quantity === 0}
            >
              Reservar entradas
            </button>
          </>
        )}
      </section>
    </div>
  )
}
