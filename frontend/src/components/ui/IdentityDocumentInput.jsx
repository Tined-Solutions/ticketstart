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
          className="px-3 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
            text-text-1 text-sm
            focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
            transition-all duration-200 disabled:opacity-50"
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
          className={`flex-1 px-4 py-2.5 bg-surface-elevated border border-white/10 rounded-lg
            text-text-1 placeholder:text-text-muted
            focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent
            transition-all duration-200 disabled:opacity-50
            ${className}`}
          {...inputProps}
        />
      </div>

      {/* Inline validation error */}
      {hasValue && !result.valid && result.error && (
        <div className="mt-1.5">
          <Badge variant="error" className="px-4 py-1 text-xs">
            {result.error}
          </Badge>
        </div>
      )}
    </div>
  )
}
