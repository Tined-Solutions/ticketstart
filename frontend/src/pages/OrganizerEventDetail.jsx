import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import apiClient from '../api/client.js'
import EventForm from '../components/EventForm.jsx'

function getErrorMessage(error) {
  if (!error) return 'Ocurrio un error inesperado'
  if (error.response?.data?.error?.message) {
    return error.response.data.error.message
  }
  if (error.response?.data?.error) {
    const backendError = error.response.data.error
    return typeof backendError === 'string'
      ? backendError
      : backendError.title || backendError.detail || 'Ocurrio un error inesperado'
  }
  if (error.response?.data?.message) {
    return error.response.data.message
  }
  if (error.response?.data?.detail) {
    return error.response.data.detail
  }
  if (error.message) {
    return error.message
  }
  return 'Ocurrio un error inesperado'
}

export default function OrganizerEventDetail() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [eventData, setEventData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let cancelled = false

    async function fetchEvent() {
      try {
        const response = await apiClient.get(`/events/${id}`)
        if (!cancelled) {
          setEventData(response.data)
        }
      } catch (err) {
        if (!cancelled) {
          setError(getErrorMessage(err))
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    fetchEvent()

    return () => {
      cancelled = true
    }
  }, [id])

  function handleSuccess() {
    navigate('/organizer/dashboard', { replace: true })
  }

  if (loading) {
    return (
      <div className="organizer-event-page">
        <p>Cargando evento...</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="organizer-event-page">
        <div className="error-container" role="alert">
          <p>{error}</p>
          <button
            type="button"
            className="button-secondary"
            onClick={() => navigate('/organizer/dashboard')}
          >
            Volver al dashboard
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="organizer-event-page">
      <h1>Editar evento</h1>
      {eventData && (
        <EventForm
          mode="edit"
          initialData={eventData}
          onSuccess={handleSuccess}
        />
      )}
    </div>
  )
}
