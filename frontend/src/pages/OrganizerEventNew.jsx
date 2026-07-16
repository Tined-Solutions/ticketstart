import { useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import EventForm from '../components/EventForm.jsx'
import { fadeIn } from '../lib/motion.js'

export default function OrganizerEventNew() {
  const navigate = useNavigate()

  function handleSuccess() {
    navigate('/organizer/dashboard', { replace: true })
  }

  return (
    <motion.div variants={fadeIn} initial="initial" animate="animate" className="max-w-[800px] mx-auto px-5 py-10">
      <h1 className="text-4xl font-display font-bold text-text-1 text-center mb-8">
        Nuevo evento
      </h1>
      <EventForm mode="create" onSuccess={handleSuccess} />
    </motion.div>
  )
}
