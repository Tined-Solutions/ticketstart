import { useState, useEffect, useRef } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../../context/auth.js'

export default function Navbar() {
  const { user, isAuthenticated, logout } = useAuth()
  const [scrolled, setScrolled] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [dropdownOpen, setDropdownOpen] = useState(false)
  const dropdownRef = useRef(null)

  // ── Scroll shadow ──
  useEffect(() => {
    const handler = () => setScrolled(window.scrollY > 0)
    handler()
    window.addEventListener('scroll', handler, { passive: true })
    return () => window.removeEventListener('scroll', handler)
  }, [])

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
      'px-3 py-2 rounded-md text-sm font-medium transition-colors',
      'duration-[var(--dur-micro)]',
      isActive
        ? 'text-brand-1 bg-brand-1/10'
        : 'text-text-2 hover:text-text-1 hover:bg-surface-elevated',
    ].join(' ')

  // ── Role-aware link helpers ──
  const role = user?.role
  const showStaff = role === 'Staff' || role === 'Admin'
  const showOrganizer = role === 'Organizador' || role === 'Admin'
  const showAdmin = role === 'Admin'

  return (
    <nav
      className={`glass-navbar sticky top-0 z-50 transition-shadow duration-300 ${
        scrolled ? 'shadow-lg shadow-black/20' : ''
      }`}
    >
      <div className="max-w-7xl mx-auto px-4 sm:px-6">
        <div className="flex items-center justify-between h-16">
          {/* ── Brand logo + wordmark ── */}
          <Link to="/" className="inline-flex items-center gap-2">
            <img
              src="/ticketera-logo.webp"
              alt=""
              width="32"
              height="32"
              className="h-8 w-auto"
            />
            <span className="font-display font-bold text-xl text-gris-oscuro">
              TicketStart
            </span>
          </Link>

          {/* ── Desktop nav links ── */}
          <div className="hidden md:flex items-center gap-1">
            <NavLink to="/" end className={navLinkClass}>
              Home
            </NavLink>
            <NavLink to="/events" className={navLinkClass}>
              Events
            </NavLink>
            <NavLink to="/tickets/lookup" className={navLinkClass}>
              My Tickets
            </NavLink>
            {showStaff && (
              <NavLink to="/staff/scan" className={navLinkClass}>
                Scan
              </NavLink>
            )}
            {showOrganizer && (
              <NavLink to="/organizer/dashboard" className={navLinkClass}>
                Dashboard
              </NavLink>
            )}
            {showAdmin && (
              <NavLink to="/admin" className={navLinkClass}>
                Admin
              </NavLink>
            )}
          </div>

          {/* ── Desktop right side ── */}
          <div className="hidden md:flex items-center gap-3">
            {isAuthenticated ? (
              <div className="relative" ref={dropdownRef}>
                <button
                  type="button"
                  onClick={() => setDropdownOpen((prev) => !prev)}
                  className="flex items-center gap-2 px-3 py-2 rounded-md text-sm
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
                      Sign Out
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <Link
                to="/login"
                className="backdrop-blur-md bg-white/10 border border-white/20
                  hover:bg-white/20 text-text-1 inline-flex items-center justify-center
                  px-3 py-1.5 text-sm rounded font-medium transition-all
                  focus-visible:outline-none focus-visible:ring-2
                  focus-visible:ring-brand-1 focus-visible:ring-offset-1"
              >
                Sign In
              </Link>
            )}
          </div>

          {/* ── Mobile hamburger ── */}
          <div className="md:hidden flex items-center gap-2">
            <button
              type="button"
              onClick={() => setMobileOpen((prev) => !prev)}
              className="p-2 rounded-md text-text-2 hover:text-text-1
                hover:bg-surface-elevated transition-colors duration-[var(--dur-micro)]"
              aria-label={mobileOpen ? 'Close menu' : 'Open menu'}
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
                Home
              </NavLink>
              <NavLink
                to="/events"
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                Events
              </NavLink>
              <NavLink
                to="/tickets/lookup"
                className={navLinkClass}
                onClick={() => setMobileOpen(false)}
              >
                My Tickets
              </NavLink>
              {showStaff && (
                <NavLink
                  to="/staff/scan"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Scan
                </NavLink>
              )}
              {showOrganizer && (
                <NavLink
                  to="/organizer/dashboard"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Dashboard
                </NavLink>
              )}
              {showAdmin && (
                <NavLink
                  to="/admin"
                  className={navLinkClass}
                  onClick={() => setMobileOpen(false)}
                >
                  Admin
                </NavLink>
              )}
              <hr className="border-glass-border my-2" />
              {isAuthenticated ? (
                <>
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
                    Sign Out
                  </button>
                </>
              ) : (
                <Link
                  to="/login"
                  onClick={() => setMobileOpen(false)}
                  className="backdrop-blur-md bg-white/10 border border-white/20
                    hover:bg-white/20 text-text-1 inline-flex items-center justify-center
                    px-3 py-1.5 text-sm rounded font-medium transition-all text-center"
                >
                  Sign In
                </Link>
              )}
            </div>
          </div>
        )}
      </div>
    </nav>
  )
}
