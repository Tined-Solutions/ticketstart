import { Link } from 'react-router-dom'

export default function NotFound() {
  return (
    <div>
      <h1>404 - Pagina no encontrada</h1>
      <p>La pagina que buscas no existe o fue movida.</p>
      <Link to="/">Volver al inicio</Link>
    </div>
  )
}
