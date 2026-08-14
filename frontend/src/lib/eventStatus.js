/**
 * Event approval status → Badge variant + Spanish label mapping (EA-008/EA-009).
 * The backend serializes EventStatus as "Pending"/"Approved"/"Rejected"
 * (per-enum JsonStringEnumConverter), so these receive the raw string.
 */

const variantByStatus = {
  Pending: 'warning',
  Approved: 'success',
  Rejected: 'error',
}

const labelByStatus = {
  Pending: 'Pendiente',
  Approved: 'Aprobado',
  Rejected: 'Rechazado',
}

export function statusBadgeVariant(status) {
  return variantByStatus[status] || 'info'
}

export function statusLabel(status) {
  if (!status) return ''
  return labelByStatus[status] || status
}
