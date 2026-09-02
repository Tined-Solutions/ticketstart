import { Link } from 'react-router-dom'

export default function Footer() {
  const year = new Date().getFullYear()

  return (
    <footer className="border-t border-border py-6 px-4 mt-auto">
      <div className="max-w-7xl mx-auto flex flex-col sm:flex-row items-center justify-between gap-2 text-sm text-text-1">
        <p>&copy; {year} TicketStart</p>
        <nav className="flex items-center gap-4" aria-label="Enlaces del pie de página">
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
          className="hover:text-brand-1 transition-colors duration-(--dur-micro)"
        >
          Desarrollada por Tined Solutions
        </a>
      </div>
    </footer>
  )
}
