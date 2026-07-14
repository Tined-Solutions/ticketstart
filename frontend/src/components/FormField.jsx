import { forwardRef } from 'react'

const FormField = forwardRef(function FormField(
  {
    id,
    label,
    error,
    hint,
    type = 'text',
    as: Component = 'input',
    className = '',
    children,
    ...rest
  },
  ref
) {
  const errorId = error ? `${id}-error` : undefined
  const hintId = hint ? `${id}-hint` : undefined

  const baseInputClasses =
    'w-full px-3 py-2.5 border rounded-md text-base font-sans bg-white text-gray-900 transition-colors focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 disabled:opacity-60 disabled:cursor-not-allowed'

  const errorInputClasses = error ? 'border-danger focus:border-danger focus:ring-danger/20' : 'border-border'

  const inputProps = {
    id,
    ref: Component === 'input' || Component === 'select' || Component === 'textarea' ? ref : undefined,
    type: type !== 'textarea' && type !== 'select' ? type : undefined,
    className: `${baseInputClasses} ${errorInputClasses} ${className}`,
    'aria-invalid': error ? 'true' : undefined,
    'aria-describedby': [errorId, hintId].filter(Boolean).join(' ') || undefined,
    ...rest,
  }

  return (
    <div className="flex flex-col gap-1 mb-4">
      {label && (
        <label htmlFor={id} className="font-medium text-gray-900 text-sm">
          {label}
        </label>
      )}

      {Component === 'textarea' ? (
        <textarea ref={ref} {...inputProps} />
      ) : Component === 'select' ? (
        <select ref={ref} {...inputProps}>
          {children}
        </select>
      ) : (
        <input ref={ref} {...inputProps} />
      )}

      {hint && (
        <span id={hintId} className="text-neutral-500 text-xs">
          {hint}
        </span>
      )}

      {error && (
        <span id={errorId} className="text-danger text-sm" role="alert">
          {error}
        </span>
      )}
    </div>
  )
})

export default FormField
