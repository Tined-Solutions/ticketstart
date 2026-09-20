/**
 * Extracts a human-readable error message from various API error shapes.
 * Handles Axios error responses, plain Error objects, and unknown shapes.
 * Returns a fallback message for null, undefined, or unparseable errors.
 *
 * @param {*} error — the caught error from a try/catch
 * @returns {string}
 */
const AXIOS_STATUS_MESSAGES = {
  400: 'La solicitud no es válida. Revisá los datos e intentá de nuevo.',
  401: 'Tu sesión expiró. Iniciá sesión de nuevo.',
  403: 'No tenés permiso para hacer eso.',
  404: 'No se encontró lo que buscabas.',
  409: 'Hay un conflicto con el estado actual. Recargá e intentá de nuevo.',
  415: 'El formato enviado no es válido.',
  422: 'Los datos enviados no son válidos.',
  429: 'Demasiados intentos. Esperá un momento e intentá de nuevo.',
  500: 'Error interno del servidor. Intentá de nuevo en unos minutos.',
}

// Backend-owned English messages that reach the user, translated to voseo.
// Keys MUST stay byte-identical to the backend strings (AdminController /
// AuthService). Unknown messages pass through untouched.
const BACKEND_MESSAGE_TRANSLATIONS = {
  'You cannot change your own role': 'No podés cambiar tu propio rol.',
  'User with this email already exists': 'Ya existe un usuario con ese email.',
}

function translateBackendMessage(message) {
  return BACKEND_MESSAGE_TRANSLATIONS[message] ?? message
}

/**
 * Translates axios-generated English strings (no server body behind them)
 * to Spanish. Returns null when the message isn't a known axios shape —
 * backend-owned strings pass through untouched.
 */
function translateAxiosMessage(message) {
  if (message === 'Network Error') {
    return 'No se pudo conectar con el servidor. Revisá tu conexión e intentá de nuevo.'
  }
  if (/^timeout of .* exceeded$/.test(message)) {
    return 'La solicitud tardó demasiado. Intentá de nuevo.'
  }
  const statusMatch = /^Request failed with status code (\d+)$/.exec(message)
  if (statusMatch) {
    const status = Number(statusMatch[1])
    return (
      AXIOS_STATUS_MESSAGES[status] ??
      `Error del servidor (código ${status}). Intentá de nuevo.`
    )
  }
  return null
}

export function getErrorMessage(error) {
  if (!error) return 'Ocurrio un error inesperado'
  if (error.response?.data?.error?.message) {
    return translateBackendMessage(error.response.data.error.message)
  }
  if (error.response?.data?.error) {
    const backendError = error.response.data.error
    return typeof backendError === 'string'
      ? translateBackendMessage(backendError)
      : translateBackendMessage(
          backendError.title || backendError.detail || 'Ocurrio un error inesperado'
        )
  }
  if (error.response?.data?.message) {
    return translateBackendMessage(error.response.data.message)
  }
  if (error.response?.data?.detail) {
    return translateBackendMessage(error.response.data.detail)
  }
  if (error.message) {
    return translateAxiosMessage(error.message) ?? error.message
  }
  return 'Ocurrio un error inesperado'
}
