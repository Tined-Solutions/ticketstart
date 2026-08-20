import { useState, useEffect, useRef } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { useAuth } from '../../context/auth.js'

export default function Navbar() {
  const { user, isAuthenticated, logout } = useAuth()
  const { pathname } = useLocation()
  const isHome = pathname === '/'
  const [scrolled, setScrolled] = useState(false)
  const [progress, setProgress] = useState(0)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [dropdownOpen, setDropdownOpen] = useState(false)
  const dropdownRef = useRef(null)

  // ── Scroll-linked reveal: the navbar unfolds proportionally to how far the
  //    full-viewport hero has been scrolled, so it slides down in sync with the
  //    scroll instead of popping in at a fixed threshold. ──
  useEffect(() => {
    const handler = () => {
      const y = window.scrollY
      setScrolled(y > 0)
      const progressValue = Math.min(1, Math.max(0, y / window.innerHeight))
      setProgress(progressValue)
    }
    handler()
    window.addEventListener('scroll', handler, { passive: true })
    return () => window.removeEventListener('scroll', handler)
  }, [])

  // On non-home pages the navbar is always fully visible.
  const translateY = isHome ? `${(progress - 1) * 100}%` : '0%'

  // ── Close dropdown on outside click ──
  useEffect(() => {
    const handler = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  // ── NavLink class resolver ──
  const navLinkClass = ({ isActive }) =>
    [
      'px-3 py-1.5 rounded-md text-sm font-medium transition-colors',
      'duration-[var(--dur-micro)]',
      isActive
        ? 'text-purpura-dark bg-purpura/10'
        : 'text-text-2 hover:text-gris-oscuro hover:bg-black/5',
    ].join(' ')

  // ── Role-aware link helpers ──
  const role = user?.role
  const showStaff = role === 'Staff' || role === 'Admin'
  const showOrganizer = role === 'Organizador' || role === 'Admin'
  const showAdmin = role === 'Admin'

  return (
    <nav
      className={`fixed inset-x-0 top-0 z-50 will-change-transform transition-[box-shadow] duration-300 ${
        scrolled ? 'shadow-lg shadow-black/20' : ''
      }`}
      style={{ transform: `translateY(${translateY})` }}
    >
      <div className="relative bg-white/80 backdrop-blur-md">
        {/* Brand signature: 5-color gradient accent line along the bottom */}
        <div
          aria-hidden="true"
          className="absolute inset-x-0 bottom-0 h-[3px] bg-[linear-gradient(90deg,#F78B2D,#F5C01F,#67CF65,#18C8DB,#B65DC2)]"
        />
        <div className="max-w-7xl mx-auto px-4 sm:px-6">
          <div className="flex items-center h-14">
            {/* ── Left: brand wordmark ── */}
            <div className="flex flex-1 items-center">
            <Link
              to="/"
              className="inline-flex items-center focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:ring-offset-2 rounded"
            >
              <span
                className="leading-none tracking-[0.04em] uppercase"
                style={{ fontFamily: "'Bebas Neue', 'Poppins', sans-serif" }}
              >
                <span className="text-lg text-gris-oscuro">Ticket</span>
                <span
                  className="text-2xl font-black bg-[linear-gradient(90deg,#F78B2D,#F5C01F,#67CF65,#18C8DB,#B65DC2)] bg-clip-text text-transparent [-webkit-text-stroke:0.9px_rgba(74,74,74,0.45)]"
                  style={{ fontFamily: "'Beckman', 'Bebas Neue', 'Poppins', sans-serif" }}
                >
                  Start
                </span>
              </span>
            </Link>
          </div>

          {/* ── Centered desktop nav links ── */}
          <div className="hidden md:flex items-center gap-1">
            <NavLink to="/" end className={navLinkClass}>
              Inicio
            </NavLink>
            <NavLink to="/events" className={navLinkClass}>
              Eventos
            </NavLink>
            <NavLink to="/tickets/lookup" className={navLinkClass}>
              Mis Entradas
            </NavLink>
            {showStaff && (
              <NavLink to="/staff/scan" className={navLinkClass}>
                Escanear
              </NavLink>
            )}
            {showOrganizer && (
              <NavLink to="/organizer/dashboard" className={navLinkClass}>
                Panel
              </NavLink>
            )}
            {showAdmin && (
              <NavLink to="/admin" className={navLinkClass}>
                Administración
              </NavLink>
            )}
          </div>

          {/* ── Right: auth + mobile toggle ── */}
          <div className="flex flex-1 items-center justify-end gap-3">
            <div className="hidden md:flex items-center gap-3">
              {isAuthenticated && (
                <div className="relative" ref={dropdownRef}>
                  <button
                    type="button"
                    onClick={() => setDropdownOpen((prev) => !prev)}
                    className="flex items-center gap-2 px-3 py-1.5 rounded-md text-sm
                      text-text-1 hover:bg-surface-elevated transition-colors
                      duration-[var(--dur-micro)]"
                    aria-expanded={dropdownOpen}
                    aria-haspopup="true"
                  >
                    <span className="w-7 h-7 rounded-full bg-brand-1/20 flex items-center justify-center text-xs font-medium text-brand-1">
                      {user?.name?.[0]?.toUpperCase() || 'U'}
                    </span>
                    <span className="hidden lg:inline">{user?.name}</span>
                  </button>

                  {dropdownOpen && (
                    <div className="absolute right-0 mt-2 w-48 glass-surface rounded-lg shadow-xl py-1 border border-glass-border">
                      <div className="px-4 py-2 text-sm text-text-2 border-b border-glass-border">
                        {user?.email}
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          logout()
                          setDropdownOpen(false)
                        }}
                        className="w-full text-left px-4 py-2 text-sm text-text-1
                          hover:bg-white/10 transition-colors duration-[var(--dur-micro)]"
                      >
                        Cerrar sesión
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* ── Mobile hamburger ── */}
            <div className="md:hidden flex items-center gap-2">
              <button
                type="button"
                onClick={() => setMobileOpen((prev) => !prev)}
                className="p-2 rounded-md text-text-2 hover:text-text-1
                  hover:bg-surface-elevated transition-colors duration-[var(--dur-micro)]"
                aria-label={mobileOpen ? 'Cerrar menú' : 'Abrir menú'}
                aria-expanded={mobileOpen}
              >
                {mobileOpen ? (
                  <svg
                    width="24"
                    height="24"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    aria-hidden="true"
                  >
                    <path d="M6 18L18 6M6 6l12 12" />
                  </svg>
                ) : (
                  <svg
                    width="24"
                    height="24"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    aria-hidden="true"
                  >
                    <path d="M4 6h16M4 12h16M4 18h16" />
                  </svg>
                )}
              </button>
            </div>
          </div>
        </div>

        {/* ── Mobile dropdown menu ── */}
        {mobileOpen && (
          <div className="md:hidden pb-4 border-t border-glass-border pt-2">
            <div className="flex flex-col gap-1">
              <NavLink
                to="/"
                end
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                Inicio
              </NavLink>
              <NavLink
                to="/events"
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                Eventos
              </NavLink>
              <NavLink
                to="/tickets/lookup"
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                Mis Entradas
              </NavLink>
              {showStaff && (
                <NavLink
                  to="/staff/scan"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Escanear
                </NavLink>
              )}
              {showOrganizer && (
                <NavLink
                  to="/organizer/dashboard"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Panel
                </NavLink>
              )}
              {showAdmin && (
                <NavLink
                  to="/admin"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Administración
                </NavLink>
              )}
              {isAuthenticated && (
                <>
                  <hr className="border-glass-border my-2" />
                  <div className="px-3 py-2 text-sm text-text-2">
                    {user?.email}
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      logout()
                      setMobileOpen(false)
                    }}
                    className="text-left px-3 py-2 rounded-md text-sm text-text-1
                      hover:bg-white/10 transition-colors"
                  >
                    Cerrar sesión
                  </button>
                </>
              )}
            </div>
          </div>
        )}
      </div>
      </div>
    </nav>
  )
}
