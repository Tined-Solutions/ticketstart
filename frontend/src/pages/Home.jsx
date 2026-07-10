import { Link } from 'react-router-dom'

export default function Home() {
  return (
    <div className="home-page">
      <section className="hero">
        <h1>Ticketera Online</h1>
        <p>
          La plataforma mas simple para descubrir eventos, reservar entradas y
          gestionar tus propios shows.
        </p>
        <Link to="/events" className="hero-button">
          Ver catalogo de eventos
        </Link>
      </section>

      <section className="home-features">
        <h2>Que podes hacer?</h2>
        <ul>
          <li>Explorar eventos publicados por organizadores.</li>
          <li>Reservar entradas con seguridad.</li>
          <li>Pagar con Mercado Pago.</li>
          <li>Gestionar tus eventos si sos organizador.</li>
        </ul>
      </section>
    </div>
  )
}
