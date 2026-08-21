import { useNavigate, useParams } from 'react-router-dom'
import { motion } from 'framer-motion'
import EventForm from '../components/EventForm.jsx'
import { useManagementEvent } from '../hooks/useManagementEvent.js'
import { getErrorMessage } from '../lib/apiError.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import { fadeIn } from '../lib/motion.js'

export default function EventReadOnlyView() {
  const { id } = useParams()
  const navigate = useNavigate()

  // D-5: management fetch (GET /events/{id}/manage, includeExpired) so past
  // Pending/Rejected/Approved events all return 200 (PEC-002). Never the public
  // GET /events/{id} path (404s non-Approved events).
  const { data: eventData, isLoading, isError, error } = useManagementEvent(id)

  const errorMessage = isError ? getErrorMessage(error) : ''

  // Loading / error states mirror OrganizerEventDetail.jsx:31-52.
  if (isLoading) {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
        <GlassCard className="text-center py-12">
          <p className="text-text-muted">Cargando evento...</p>
        </GlassCard>
      </motion.div>
    )
  }

  if (isError) {
    return (
      <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{errorMessage}</p>
          <Button variant="secondary" onClick={() => navigate(-1)}>
            Volver
          </Button>
        </GlassCard>
      </motion.div>
    )
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-5xl mx-auto px-4 sm:px-6 py-10">
      <h1 className="text-4xl font-display font-bold text-text-1 text-center mb-8">
        Ver evento
      </h1>
      {eventData && (
        <EventForm mode="edit" readOnly initialData={eventData} />
      )}
      <div className="mt-6 text-center">
        <Button variant="secondary" onClick={() => navigate(-1)}>
          Volver
        </Button>
      </div>
    </motion.div>
  )
}