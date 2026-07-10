import { Link, useSearchParams } from 'react-router-dom'

function resolveStatus(status) {
  const normalized = (status || '').toLowerCase()

  if (normalized === 'approved' || normalized === 'success') {
    return {
      title: '¡Pago confirmado!',
      message: 'Tus entradas fueron enviadas a tu email.',
      type: 'success',
    }
  }

  if (normalized === 'pending' || normalized === 'in_process') {
    return {
      title: 'Pago pendiente',
      message: 'Te avisaremos cuando se confirme.',
      type: 'pending',
    }
  }

  if (normalized === 'failure' || normalized === 'rejected') {
    return {
      title: 'Pago rechazado',
      message: 'El pago fue rechazado. Intentá nuevamente.',
      type: 'error',
    }
  }

  return {
    title: 'Resultado del pago',
    message:
      'No pudimos determinar el estado del pago. Si ya pagaste, tus entradas seran enviadas a tu email en los proximos minutos.',
    type: 'unknown',
  }
}

export default function CheckoutReturn() {
  const [searchParams] = useSearchParams()
  const status = searchParams.get('status')
  const paymentId = searchParams.get('payment_id')
  const externalReference = searchParams.get('external_reference')

  const result = resolveStatus(status)

  return (
    <div className="checkout-return-page">
      <h1>{result.title}</h1>
      <p className={`checkout-return-message checkout-return-message--${result.type}`}>
        {result.message}
      </p>

      {paymentId && (
        <p className="checkout-return-detail">
          ID de pago: <code>{paymentId}</code>
        </p>
      )}

      {externalReference && (
        <p className="checkout-return-detail">
          Referencia: <code>{externalReference}</code>
        </p>
      )}

      <p className="checkout-return-email-note">
        Revisá tu casilla de correo (incluyendo spam) para encontrar tus entradas con los códigos QR.
      </p>

      <Link to="/events" className="back-link">
        ← Volver al catalogo
      </Link>
    </div>
  )
}
