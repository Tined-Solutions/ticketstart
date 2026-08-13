import { useNavigate, useParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import EventForm from '../components/EventForm.jsx'
import { useManagementEvent } from '../hooks/useManagementEvent.js'
import { getErrorMessage } from '../lib/apiError.js'
import { queryKeys } from '../lib/queryKeys.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Button from '../components/Button.jsx'
import { fadeIn } from '../lib/motion.js'

export default function OrganizerEventDetail() {
  const { id } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { data: eventData, isLoading, isError, error } = useManagementEvent(id)

  const errorMessage = isError ? getErrorMessage(error) : ''

  function handleSuccess() {
    // Event edited → details/stock may have changed → invalidate the catalog
    // and this event's detail (both public and management variants) before
    // returning to the dashboard.
    queryClient.invalidateQueries({ queryKey: queryKeys.events })
    queryClient.invalidateQueries({ queryKey: queryKeys.event(id) })
    queryClient.invalidateQueries({ queryKey: queryKeys.managementEvent(id) })
    navigate('/organizer/dashboard', { replace: true })
  }

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
