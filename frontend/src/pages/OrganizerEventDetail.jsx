import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import apiClient from '../api/client.js'
import EventForm from '../components/EventForm.jsx'
import { getErrorMessage } from '../lib/apiError.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import { fadeIn } from '../lib/motion.js'

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
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
        <GlassCard className="text-center py-12">
          <p className="text-text-muted">Cargando evento...</p>
        </GlassCard>
      </motion.div>
    )
  }

  if (error) {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{error}</p>
          <Button variant="secondary" onClick={() => navigate('/organizer/dashboard')}>
            Volver al dashboard
          </Button>
        </GlassCard>
      </motion.div>
    )
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
      <h1 className="text-4xl font-display font-bold text-text-1 text-center mb-8">
        Editar evento
      </h1>
      {eventData && (
        <EventForm
          mode="edit"
          initialData={eventData}
          onSuccess={handleSuccess}
        />
      )}
    </motion.div>
  )
}
