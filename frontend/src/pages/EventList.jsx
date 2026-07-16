import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import apiClient from '../api/client.js'
import { formatEventDate } from '../lib/format.js'

function EventCard({ event, onClick }) {
  return (
    <button
      type="button"
      className="event-card"
      onClick={onClick}
      aria-label={`Ver detalle de ${event.name}`}
    >
      <div className="event-card-image">
        {event.imageUrl ? (
          <img src={event.imageUrl} alt={event.name} loading="lazy" />
        ) : (
          <div className="event-card-placeholder">Sin imagen</div>
        )}
      </div>
      <div className="event-card-content">
        <h2>{event.name}</h2>
        <p className="event-card-date">{formatEventDate(event.date)}</p>
        <p className="event-card-location">{event.location}</p>
      </div>
    </button>
  )
}

function SkeletonCard() {
  return (
    <article className="event-card event-card-skeleton">
      <div className="event-card-image skeleton" />
      <div className="event-card-content">
        <div className="skeleton skeleton-title" />
        <div className="skeleton skeleton-line" />
        <div className="skeleton skeleton-line" />
      </div>
    </article>
  )
}

export default function EventList() {
  const navigate = useNavigate()
  const [events, setEvents] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const loadEvents = useCallback((controller) => {
    apiClient
      .get('/events', { signal: controller.signal })
      .then((response) => {
        if (controller.signal.aborted) return
        setEvents(response.data || [])
        setError('')
        setLoading(false)
      })
      .catch((error) => {
        if (controller.signal.aborted) return
        const message =
          error.response?.data?.error?.message ||
          error.response?.data?.message ||
          'Ocurrio un error al cargar los eventos'
        setError(message)
        setLoading(false)
      })
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    loadEvents(controller)
    return () => controller.abort()
  }, [loadEvents])

  const handleRetry = () => {
    setLoading(true)
    setError('')
    const controller = new AbortController()
    loadEvents(controller)
  }

  const handleEventClick = (eventId) => () => {
    navigate(`/events/${eventId}`)
  }

  return (
    <div className="event-list-page">
      <header className="page-header">
        <h1>Eventos</h1>
        <p>Descubri los mejores eventos y compra tus entradas</p>
      </header>

      {loading ? (
        <div className="event-grid">
          {Array.from({ length: 6 }).map((_, index) => (
            <SkeletonCard key={index} />
          ))}
        </div>
      ) : error ? (
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button type="button" onClick={handleRetry}>
            Reintentar
          </button>
        </div>
      ) : events.length === 0 ? (
        <div className="empty-state">
          <p>No hay eventos disponibles por el momento.</p>
          <Link to="/" className="button-link">
            Volver al inicio
          </Link>
        </div>
      ) : (
        <div className="event-grid">
          {events.map((event) => (
            <EventCard
              key={event.id}
              event={event}
              onClick={handleEventClick(event.id)}
            />
          ))}
        </div>
      )}
    </div>
  )
}
