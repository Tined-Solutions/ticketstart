import { useNavigate } from 'react-router-dom'
import EventForm from '../components/EventForm.jsx'

export default function OrganizerEventNew() {
  const navigate = useNavigate()

  function handleSuccess() {
    navigate('/organizer/dashboard', { replace: true })
  }

  return (
    <div className="organizer-event-page">
      <h1>Nuevo evento</h1>
      <EventForm mode="create" onSuccess={handleSuccess} />
    </div>
  )
}
