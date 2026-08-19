// Frontend-only category taxonomy (REQ-BDS-8). No backend category model/API
// exists, so the Home chips come from this local list and are purely decorative.
// Each chip pairs a brand tint background with its dark-variant text for
// WCAG AA contrast on white (brand 2.4). Tints are tuned per chip so the
// dark-variant text clears 4.5:1 (naranja uses /10; the rest use /15).

export const categories = [
  { id: 'musica',     label: 'Música',     colorKey: 'naranja',  hex: '#F78B2D', darkHex: '#B45309' },
  { id: 'teatro',     label: 'Teatro',     colorKey: 'purpura',  hex: '#B65DC2', darkHex: '#6A2176' },
  { id: 'deportes',   label: 'Deportes',   colorKey: 'verde',    hex: '#67CF65', darkHex: '#166534' },
  { id: 'standup',    label: 'Stand-up',   colorKey: 'amarillo', hex: '#F5C01F', darkHex: '#6B5300' },
  { id: 'festivales', label: 'Festivales', colorKey: 'cian',     hex: '#18C8DB', darkHex: '#0B6170' },
]

export const chipClass = {
  naranja:  'bg-naranja/10 text-naranja-dark',
  purpura:  'bg-purpura/15 text-purpura-dark',
  verde:    'bg-verde/15 text-verde-dark',
  amarillo: 'bg-amarillo/15 text-amarillo-dark',
  cian:     'bg-cian/15 text-cian-dark',
}

// Decorate categories with their chip class so consumers (GradientHero chips)
// can render them directly.
export const categoriesWithChipClass = categories.map((category) => ({
  ...category,
  chipClass: chipClass[category.colorKey],
}))
