import { Routes, Route } from 'react-router-dom'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import RoleGuard from './components/RoleGuard.jsx'
import Home from './pages/Home.jsx'
import Login from './pages/Login.jsx'
import Register from './pages/Register.jsx'
import EventList from './pages/EventList.jsx'
import EventDetail from './pages/EventDetail.jsx'
import Checkout from './pages/Checkout.jsx'
import CheckoutReturn from './pages/CheckoutReturn.jsx'
import TicketLookup from './pages/TicketLookup.jsx'
import StaffScan from './pages/StaffScan.jsx'
import OrganizerDashboard from './pages/OrganizerDashboard.jsx'
import OrganizerEventNew from './pages/OrganizerEventNew.jsx'
import OrganizerEventDetail from './pages/OrganizerEventDetail.jsx'
import OrganizerEventMetrics from './pages/OrganizerEventMetrics.jsx'
import AdminPanel from './pages/AdminPanel.jsx'
import NotFound from './pages/NotFound.jsx'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/events" element={<EventList />} />
      <Route path="/events/:id" element={<EventDetail />} />

      <Route path="/checkout" element={<Checkout />} />
      <Route path="/checkout/return" element={<CheckoutReturn />} />
      <Route path="/tickets/lookup" element={<TicketLookup />} />

      <Route
        path="/staff/scan"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Staff', 'Admin']}>
              <StaffScan />
            </RoleGuard>
          </ProtectedRoute>
        }
      />

      <Route
        path="/organizer/dashboard"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Organizador', 'Admin']}>
              <OrganizerDashboard />
            </RoleGuard>
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/new"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Organizador', 'Admin']}>
              <OrganizerEventNew />
            </RoleGuard>
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/:id/metrics"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Organizador', 'Admin']}>
              <OrganizerEventMetrics />
            </RoleGuard>
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/:id"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Organizador', 'Admin']}>
              <OrganizerEventDetail />
            </RoleGuard>
          </ProtectedRoute>
        }
      />

      <Route
        path="/admin"
        element={
          <ProtectedRoute>
            <RoleGuard allowedRoles={['Admin']}>
              <AdminPanel />
            </RoleGuard>
          </ProtectedRoute>
        }
      />

      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}

export default App
