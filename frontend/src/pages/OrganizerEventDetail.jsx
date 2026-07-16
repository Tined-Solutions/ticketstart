import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import apiClient from '../api/client.js'
import EventForm from '../components/EventForm.jsx'
import { getErrorMessage } from '../lib/apiError.js'

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
        const response = await apiClient.get(`/events/${id}/manage`)
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
