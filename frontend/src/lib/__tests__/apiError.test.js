import { describe, it, expect } from 'vitest'
import { getErrorMessage } from '../apiError.js'

describe('getErrorMessage', () => {
  it('extracts error.response.data.error.message from a structured API error', () => {
    const error = {
      response: {
        data: {
          error: { message: 'El email ya esta registrado' },
        },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('El email ya esta registrado')
  })

  it('extracts error.response.data.error when it is a plain string', () => {
    const error = {
      response: {
        data: {
          error: 'QRCodeData is required',
        },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('QRCodeData is required')
  })

  it('extracts error.response.data.error.title when error is an object without message', () => {
    const error = {
      response: {
        data: {
          error: { title: 'Validation Error', detail: 'Field name is required' },
        },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('Validation Error')
  })

  it('falls back to error.response.data.error.detail when no title or message exists', () => {
    const error = {
      response: {
        data: {
          error: { detail: 'Missing required field' },
        },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('Missing required field')
  })

  it('extracts error.response.data.message when error.response.data.error is missing', () => {
    const error = {
      response: {
        data: { message: 'Operation failed' },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('Operation failed')
  })

  it('extracts error.response.data.detail when no other fields are present', () => {
    const error = {
      response: {
        data: { detail: 'Not found' },
      },
    }
    const result = getErrorMessage(error)
    expect(result).toBe('Not found')
  })

  it('translates the axios "Network Error" to Spanish', () => {
    const error = new Error('Network Error')
    const result = getErrorMessage(error)
    expect(result).toBe(
      'No se pudo conectar con el servidor. Revisá tu conexión e intentá de nuevo.'
    )
  })

  it('translates axios timeout errors to Spanish', () => {
    const result = getErrorMessage(new Error('timeout of 0ms exceeded'))
    expect(result).toBe('La solicitud tardó demasiado. Intentá de nuevo.')
  })

  it('translates axios status-code errors to Spanish', () => {
    expect(
      getErrorMessage(new Error('Request failed with status code 404'))
    ).toBe('No se encontró lo que buscabas.')
    expect(
      getErrorMessage(new Error('Request failed with status code 500'))
    ).toBe('Error interno del servidor. Intentá de nuevo en unos minutos.')
  })

  it('falls back to a generic Spanish message for unknown status codes', () => {
    expect(
      getErrorMessage(new Error('Request failed with status code 418'))
    ).toBe('Error del servidor (código 418). Intentá de nuevo.')
  })

  it('passes through non-axios error.message strings untouched', () => {
    expect(getErrorMessage(new Error('Something broke'))).toBe('Something broke')
  })

  it('returns a fallback message for null input', () => {
    const result = getErrorMessage(null)
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('')
  })

  it('returns a fallback message for undefined input', () => {
    const result = getErrorMessage(undefined)
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('')
  })

  it('returns a fallback message for an empty object', () => {
    const result = getErrorMessage({})
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
    expect(result).not.toBe('')
  })

  it('returns a fallback message for an unknown error shape', () => {
    const result = getErrorMessage({ someUnknown: 'field' })
    expect(typeof result).toBe('string')
    expect(result.length).toBeGreaterThan(0)
  })
})
