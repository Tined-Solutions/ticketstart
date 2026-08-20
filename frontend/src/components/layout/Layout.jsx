import { useLocation } from 'react-router-dom'
import Navbar from './Navbar.jsx'
import Footer from './Footer.jsx'

export default function Layout({ children }) {
  const { pathname } = useLocation()
  // The navbar is fixed (overlay). On the home page the hero owns the full
  // viewport (no padding), so we only reserve its height elsewhere.
  const isHome = pathname === '/'

  return (
    <div className="flex flex-col min-h-screen">
      <Navbar />
      <main className={`flex-1 ${isHome ? '' : 'pt-16'}`}>{children}</main>
      <Footer />
    </div>
  )
}
