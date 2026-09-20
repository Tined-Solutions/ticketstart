// Mensajes de resultado del escaneo de QR, en voseo rioplatense.
// El backend devuelve un `errorCode` estable; acá se traduce a copy
// orientada al staff que escanea (para poder explicarle al comprador).

import { formatEventDate } from './format.js'

const SCAN_ERROR_MESSAGES = {
  invalid_signature: 'Este QR no es de una entrada valida. Parece que esta adulterado.',
  invalid_format: 'El QR no tiene un formato valido.',
  ticket_not_found: 'No encontramos esta entrada en el sistema.',
  outside_window: 'Esta entrada no corresponde a la fecha/hora de este evento.',
  already_used: 'Esta entrada ya fue usada.',
  refunded: 'Esta entrada fue reembolsada.',
  wrong_event: 'Esta entrada es de otro evento.',
}

const SUCCESS_MESSAGE = 'Entrada valida. Podes dejarlo pasar.'
const FALLBACK_ERROR_MESSAGE = 'Entrada invalida.'

/**
 * Traduce la respuesta del backend a un mensaje en voseo para el staff.
 * @param {{ isValid: boolean, error?: string|null, errorCode?: string|null, ticket?: object|null }} response
 * @returns {string}
 */
export function getScanMessage(response) {
  if (!response) return FALLBACK_ERROR_MESSAGE

  if (response.isValid) return SUCCESS_MESSAGE

  if (response.errorCode === 'wrong_event' && response.ticket?.eventName) {
    return `Esta entrada es de otro evento: ${response.ticket.eventName}.`
  }

  // "Ya usada" incluye CUÁNDO, con el formato de fecha/hora del resto del app
  // (es-AR, hora local, 24h) — el backend manda el instante en UTC.
  if (response.errorCode === 'already_used' && response.ticket?.usedAt) {
    return `Esta entrada ya fue usada el ${formatEventDate(response.ticket.usedAt)}.`
  }

  return (
    SCAN_ERROR_MESSAGES[response.errorCode] ||
    response.error ||
    FALLBACK_ERROR_MESSAGE
  )
}