/**
 * Extracts a human-readable error message from various API error shapes.
 * Handles Axios error responses, plain Error objects, and unknown shapes.
 * Returns a fallback message for null, undefined, or unparseable errors.
 *
 * @param {*} error — the caught error from a try/catch
 * @returns {string}
 */
export function getErrorMessage(error) {
  if (!error) return 'Ocurrio un error inesperado'
  if (error.response?.data?.error?.message) {
    return error.response.data.error.message
  }
  if (error.response?.data?.error) {
    const backendError = error.response.data.error
    return typeof backendError === 'string'
      ? backendError
      : backendError.title || backendError.detail || 'Ocurrio un error inesperado'
  }
  if (error.response?.data?.message) {
    return error.response.data.message
  }
  if (error.response?.data?.detail) {
    return error.response.data.detail
  }
  if (error.message) {
    return error.message
  }
  return 'Ocurrio un error inesperado'
}
