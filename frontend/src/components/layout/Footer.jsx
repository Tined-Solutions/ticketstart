import { Link } from 'react-router-dom'

export default function Footer() {
  const year = new Date().getFullYear()

  return (
    <footer className="border-t border-border py-6 px-4 mt-auto">
      {/* 3-column grid on sm+ so the middle link ("Preguntas frecuentes") is
          truly centered; flex justify-between shifted it toward the narrower
          copyright column. Mobile stays stacked and centered. */}
      <div className="max-w-7xl mx-auto flex flex-col items-center gap-2 text-sm text-text-1 sm:grid sm:grid-cols-3 sm:items-center">
        <p className="sm:justify-self-start">&copy; {year} TicketStart</p>
        <nav className="flex items-center gap-4 sm:justify-self-center" aria-label="Enlaces del pie de página">
          <Link
            to="/faq"
            className="hover:text-brand-1 transition-colors duration-(--dur-micro)"
          >
            Preguntas frecuentes
          </Link>
        </nav>
        <a
          href="https://tinedsolutions.tech"
          target="_blank"
          rel="noopener noreferrer"
          className="hover:text-brand-1 transition-colors duration-(--dur-micro) sm:justify-self-end"
        >
          Desarrollada por Tined Solutions
        </a>
      </div>
    </footer>
  )
}
