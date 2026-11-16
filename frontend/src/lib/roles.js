/**
 * Display labels for the backend `UserRole` enum. The keys are the enum names
 * the API sends and expects (never change them); only the visible copy lives
 * here so every role list, badge and filter shows the same names.
 */
export const ROLE_LABELS = {
  Organizador: 'Productor/Organizador',
  Staff: 'Staff/Acomodador',
  Admin: 'Admin',
  SinAcceso: 'Sin acceso',
}

export function roleLabel(role) {
  if (!role) return ''
  return ROLE_LABELS[role] || role
}
