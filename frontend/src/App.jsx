import { Routes, Route } from 'react-router-dom'
import { MotionConfig } from 'framer-motion'
import Layout from './components/layout/Layout.jsx'
import ScrollToTop from './components/ScrollToTop.jsx'
import ProtectedRoute from './components/ProtectedRoute.jsx'
import RoleGuard from './components/RoleGuard.jsx'
import ErrorBoundary from './components/ErrorBoundary.jsx'
import Home from './pages/Home.jsx'
import Login from './pages/Login.jsx'
import EventList from './pages/EventList.jsx'
import EventDetail from './pages/EventDetail.jsx'
import Checkout from './pages/Checkout.jsx'
import CheckoutReturn from './pages/CheckoutReturn.jsx'
import CheckoutSuccess from './pages/CheckoutSuccess.jsx'
import TicketLookup from './pages/TicketLookup.jsx'
import Faq from './pages/Faq.jsx'
import StaffScan from './pages/StaffScan.jsx'
import OrganizerDashboard from './pages/OrganizerDashboard.jsx'
import OrganizerEventNew from './pages/OrganizerEventNew.jsx'
import OrganizerEventDetail from './pages/OrganizerEventDetail.jsx'
import EventReadOnlyView from './pages/EventReadOnlyView.jsx'
import AdminPanel from './pages/AdminPanel.jsx'
import AdminPurchases from './pages/AdminPurchases.jsx'
import NotFound from './pages/NotFound.jsx'

function App() {
  return (
    <ErrorBoundary>
      <MotionConfig reducedMotion="user">
        <ScrollToTop />
        <Layout>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/events" element={<EventList />} />
          <Route path="/events/:id" element={<EventDetail />} />

          <Route path="/checkout" element={<Checkout />} />
          <Route path="/checkout/return" element={<CheckoutReturn />} />
          <Route path="/checkout/success" element={<CheckoutSuccess />} />
          <Route path="/tickets/lookup" element={<TicketLookup />} />
          <Route path="/faq" element={<Faq />} />

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
            path="/organizer/events/:id/view"
            element={
              <ProtectedRoute>
                <RoleGuard allowedRoles={['Organizador', 'Admin']}>
                  <EventReadOnlyView />
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

          <Route
            path="/admin/events/:id/purchases"
            element={
              <ProtectedRoute>
                <RoleGuard allowedRoles={['Admin']}>
                  <AdminPurchases />
                </RoleGuard>
              </ProtectedRoute>
            }
          />

          <Route path="*" element={<NotFound />} />
        </Routes>
        </Layout>
      </MotionConfig>
    </ErrorBoundary>
  )
}

export default App
