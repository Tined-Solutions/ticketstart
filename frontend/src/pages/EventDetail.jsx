import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import apiClient from '../api/client.js'

function formatEventDate(dateString) {
  if (!dateString) return 'Fecha por confirmar'
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return 'Fecha no valida'
  return date.toLocaleDateString('es-AR', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatCurrency(amount) {
  if (amount === undefined || amount === null) return '$ --'
  return `$ ${Number(amount).toLocaleString('es-AR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

function TicketTypeRow({ ticketType, quantity, onChange }) {
  const available = ticketType.available ?? ticketType.quantity ?? 0
  const isSoldOut = available <= 0

  return (
    <div className="ticket-type-row">
      <div className="ticket-type-info">
        <h3>{ticketType.name}</h3>
        <p className="ticket-type-price">{formatCurrency(ticketType.price)}</p>
        <p className="ticket-type-availability">
          {isSoldOut
            ? 'Agotado'
            : `${available} disponibles de ${ticketType.quantity}`}
        </p>
      </div>
      <div className="ticket-type-selector">
        <button
          type="button"
          aria-label={`Disminuir cantidad de ${ticketType.name}`}
          onClick={() => onChange(Math.max(0, quantity - 1))}
          disabled={isSoldOut || quantity <= 0}
        >
          -
        </button>
        <span aria-live="polite">{quantity}</span>
        <button
          type="button"
          aria-label={`Aumentar cantidad de ${ticketType.name}`}
          onClick={() => onChange(Math.min(available, quantity + 1))}
          disabled={isSoldOut || quantity >= available}
        >
          +
        </button>
      </div>
    </div>
  )
}

export default function EventDetail() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [event, setEvent] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [quantities, setQuantities] = useState({})

  const loadEvent = useCallback(
    (controller) => {
      apiClient
        .get(`/events/${id}`, { signal: controller.signal })
        .then((response) => {
          if (controller.signal.aborted) return
          const eventData = response.data
          setEvent(eventData)
          const initialQuantities = {}
          eventData.ticketTypes?.forEach((ticketType) => {
            initialQuantities[ticketType.id] = 0
          })
          setQuantities(initialQuantities)
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

  const updateQuantity = (ticketTypeId) => (quantity) => {
    setQuantities((prev) => ({ ...prev, [ticketTypeId]: quantity }))
  }

  const totalTickets = Object.values(quantities).reduce(
    (sum, quantity) => sum + quantity,
    0
  )

  const totalPrice = event?.ticketTypes?.reduce((sum, ticketType) => {
    const quantity = quantities[ticketType.id] || 0
    return sum + quantity * (ticketType.price || 0)
  }, 0)

  const handleReserve = () => {
    if (totalTickets === 0) return

    const selections = event.ticketTypes
      .filter((ticketType) => (quantities[ticketType.id] || 0) > 0)
      .map((ticketType) => ({
        ticketTypeId: ticketType.id,
        name: ticketType.name,
        price: ticketType.price,
        quantity: quantities[ticketType.id],
      }))

    navigate('/checkout', {
      state: {
        eventId: event.id,
        eventName: event.name,
        eventDate: event.date,
        eventLocation: event.location,
        eventImageUrl: event.imageUrl,
        selections,
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
            {event.ticketTypes?.map((ticketType) => (
              <TicketTypeRow
                key={ticketType.id}
                ticketType={ticketType}
                quantity={quantities[ticketType.id] || 0}
                onChange={updateQuantity(ticketType.id)}
              />
            ))}
            <div className="reservation-summary">
              <p>Entradas seleccionadas: {totalTickets}</p>
              <p>Total: {formatCurrency(totalPrice)}</p>
            </div>
            <button
              type="button"
              className="reserve-button"
              onClick={handleReserve}
              disabled={totalTickets === 0}
            >
              Reservar entradas
            </button>
          </>
        )}
      </section>
    </div>
  )
}
