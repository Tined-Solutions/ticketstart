import { useState, useMemo } from 'react'
import Badge from './Badge.jsx'
import { validateDocument } from '../../utils/identityValidation.js'

const COUNTRY_OPTIONS = [
  { value: 'AR', label: 'Argentina', flag: '🇦🇷' },
  { value: 'UY', label: 'Uruguay', flag: '🇺🇾' },
]

export default function IdentityDocumentInput({
  value = '',
  onChange,
  onBlur: onBlurProp,
  country = 'AR',
  onCountryChange,
  label = 'Documento de identidad',
  id,
  disabled = false,
  className = '',
  error,
  errorId,
  ...inputProps
}) {
  const [focused, setFocused] = useState(false)

  const result = useMemo(
    () => validateDocument(value, country),
    [value, country],
  )

  const displayValue = focused || !result.valid
    ? value
    : result.formatted

  const handleChange = (e) => {
    if (onChange) {
      onChange(e.target.value)
    }
  }

  const handleCountryChange = (e) => {
    if (onCountryChange) {
      onCountryChange(e.target.value)
    }
  }

  const handleFocus = () => setFocused(true)

  const handleBlur = (e) => {
    setFocused(false)
    if (onBlurProp) {
      onBlurProp(e)
    }
  }

  const inputId = id || 'identity-document'
  const hasValue = value.trim().length > 0
  const hasExternalError = Boolean(error)
  const hasInternalError = hasValue && !result.valid && result.error

  const externalErrorId = errorId || `${inputId}-error`
  const internalErrorId = `${inputId}-internal-error`
  const describedBy = [
    hasExternalError ? externalErrorId : null,
    hasInternalError ? internalErrorId : null,
  ].filter(Boolean).join(' ') || undefined
  const isInvalid = hasExternalError || hasInternalError

  return (
    <div>
      {label && (
        <label
          htmlFor={inputId}
          className="block text-sm font-medium text-text-2 mb-1"
        >
          {label}
        </label>
      )}

      <div className="flex gap-2">
        {/* Country selector */}
        <select
          value={country}
          onChange={handleCountryChange}
          disabled={disabled}
          aria-label="País del documento"
          className="px-3 py-2.5 bg-white/60 border border-gris-oscuro/15 rounded-lg backdrop-blur-sm
            text-gris-oscuro text-sm
            focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:border-transparent
            transition-[border-color,box-shadow] duration-200 disabled:opacity-50"
        >
          {COUNTRY_OPTIONS.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.flag} {opt.label}
            </option>
          ))}
        </select>

        {/* Document number input */}
        <input
          id={inputId}
          type="text"
          inputMode="numeric"
          value={displayValue}
          onChange={handleChange}
          onFocus={handleFocus}
          onBlur={handleBlur}
          disabled={disabled}
          placeholder={country === 'AR' ? '12.345.678' : '1.234.567-8'}
          aria-invalid={isInvalid ? 'true' : undefined}
          aria-describedby={describedBy}
          className={`flex-1 px-4 py-2.5 bg-white/60 border border-gris-oscuro/15 rounded-lg backdrop-blur-sm
            text-gris-oscuro placeholder:text-text-muted
            focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-1 focus-visible:border-transparent
            transition-[border-color,box-shadow] duration-200 disabled:opacity-50
            ${className}`}
          {...inputProps}
        />
      </div>

      {/* External error (e.g. from the parent form's submit validation) */}
      {hasExternalError && (
        <div className="mt-1.5">
          <p id={externalErrorId} role="alert" className="text-sm text-danger">
            {error}
          </p>
        </div>
      )}

      {/* Inline validation error */}
      {hasInternalError && !hasExternalError && (
        <div className="mt-1.5">
          <Badge id={internalErrorId} variant="error" className="px-4 py-1 text-xs" role="alert">
            {result.error}
          </Badge>
        </div>
      )}
    </div>
  )
}
