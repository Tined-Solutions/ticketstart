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

  it('uses error.message as last resort for non-Axios errors', () => {
    const error = new Error('Network Error')
    const result = getErrorMessage(error)
    expect(result).toBe('Network Error')
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
