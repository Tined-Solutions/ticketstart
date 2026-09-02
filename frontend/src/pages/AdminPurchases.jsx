import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams } from 'react-router-dom'
import apiClient from '../api/client.js'
import { getErrorMessage } from '../lib/apiError.js'
import { formatCurrency } from '../lib/format.js'
import GlassCard from '../components/ui/GlassCard.jsx'
import Badge from '../components/ui/Badge.jsx'
import Button from '../components/Button.jsx'
import Skeleton from '../components/ui/Skeleton.jsx'
import { useDialog } from '../hooks/useDialog.js'

function formatDate(dateString) {
  if (!dateString) return ''
  const date = new Date(dateString)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleDateString('es-AR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

// D4: decimal-STRING → integer cents. Cents derive from the string representation,
// NEVER from float arithmetic (0.29 × 100 → 28.999…); String(number) is
// shortest-round-trip so 2-decimal API values round-trip exactly.
function toCents(value) {
  const [intPart, decPart = ''] = String(value).split('.')
  return Number(intPart) * 100 + Number(`${decPart}00`.slice(0, 2))
}

// Integer cents → decimal string for the number input ("5000" → "50").
function centsToInput(cents) {
  return String(cents / 100)
}

function refundBadge(qty, refundedQty) {
  if (refundedQty === 0) return { variant: 'success', label: 'Confirmada' }
  const de = refundedQty >= qty
    ? { variant: 'error', label: 'Reembolsada' } // fully → rose
    : { variant: 'warning', label: 'Reembolsada' } // partial → amber
  return { ...de, label: `${refundedQty} de ${qty} reembolsadas` } // APR-010
}

function RefundConfirmationDialog({ purchase, eventName, onConfirm, onCancel, refunding, error }) {
  const dialogRef = useDialog({ onClose: onCancel })
  const [selectedQuantity, setSelectedQuantity] = useState(1)
  const activeRemaining = purchase.quantity - purchase.refundedQuantity
  // D4: cents math — unit price derived from the transaction amount; Math.round is
  // the anomaly fallback (exact while tx.Amount = unit price × quantity).
  const unitPriceCents = Math.round(toCents(purchase.amount) / purchase.quantity)
  const capCents = unitPriceCents * selectedQuantity

  // D4: the amount state + isAmountDirty flag. Not dirty → the prefill (K × unit
  // price) recomputes on quantity change; dirty → the value is kept and re-validated
  // against the new cap (validation below is derived, so it flags immediately).
  const [amount, setAmount] = useState(() => centsToInput(unitPriceCents))
  const [isAmountDirty, setIsAmountDirty] = useState(false)

  const ticketsLabel = purchase.quantity === 1 ? 'entrada' : 'entradas'

  const handleQuantityChange = (e) => {
    const next = Number(e.target.value)
    setSelectedQuantity(next)
    if (!isAmountDirty && next >= 1) {
      setAmount(centsToInput(unitPriceCents * next))
    }
  }

  const handleAmountChange = (e) => {
    setAmount(e.target.value)
    setIsAmountDirty(true)
  }

  // D1: a percent click is a ONE-SHOT amount write (no persistent percent state).
  // D4: percent → amount via integer-cents math, half-up.
  const applyPercent = (pct) => {
    setAmount(centsToInput(Math.round((pct * capCents) / 100)))
    setIsAmountDirty(true)
  }

  // D4 inline validation, mirroring the backend D3 guards: > 2 decimals → ≤ 0 →
  // > cap (unit price × K). Blocks submit with an inline error, no mutation.
  const decPart = String(amount).split('.')[1] ?? ''
  const amountCents = toCents(amount)
  let amountError = ''
  if (decPart.length > 2) {
    amountError = 'El monto no puede tener más de 2 decimales'
  } else if (Number.isNaN(amountCents) || amountCents <= 0) {
    amountError = 'El monto debe ser mayor a cero'
  } else if (amountCents > capCents) {
    amountError = `El monto no puede superar ${formatCurrency(capCents / 100, { fractionDigits: 2 })} para ${selectedQuantity} ${ticketsLabel}`
  }

  const handleConfirm = () => {
    if (amountError) return
    onConfirm(selectedQuantity, Number(amount))
  }

  return (
    <div
      ref={dialogRef}
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-5 overscroll-contain"
      role="dialog"
      aria-modal="true"
      aria-labelledby="refund-dialog-title"
    >
      <div className="glass-surface p-8 max-w-md w-full shadow-xl text-left rounded-[--radius-glass]">
        <h2 id="refund-dialog-title" className="text-xl font-display font-semibold text-text-1 mb-3">
          Confirmar Reembolso
        </h2>
        <p className="text-text-2 mb-4 leading-relaxed">
          Vas a reembolsar la compra de <strong>{purchase.purchaserEmail}</strong> en{' '}
          <strong>{eventName}</strong> ({purchase.quantity} {ticketsLabel},{' '}
          {formatCurrency(purchase.amount)}). Esta acción no se puede deshacer.
        </p>

        <div className="mb-4">
          <label htmlFor="refund-quantity" className="block text-sm text-text-2 mb-1">
            Cantidad a reembolsar
          </label>
          <input
            id="refund-quantity"
            type="number"
            min={1}
            max={activeRemaining}
            value={selectedQuantity}
            onChange={handleQuantityChange}
            className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-text-1 min-h-[44px]"
            aria-label="Cantidad a reembolsar"
          />
          <p className="mt-2 text-sm text-text-2">
            Reembolsar {selectedQuantity} ×{' '}
            {formatCurrency(unitPriceCents / 100, { fractionDigits: 2 })} ={' '}
            {formatCurrency(unitPriceCents * selectedQuantity / 100, { fractionDigits: 2 })}
          </p>
        </div>

        <div className="mb-4">
          <label htmlFor="refund-amount" className="block text-sm text-text-2 mb-1">
            Monto a reembolsar
          </label>
          <input
            id="refund-amount"
            type="number"
            step="0.01"
            min="0"
            value={amount}
            onChange={handleAmountChange}
            className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-text-1 min-h-[44px]"
            aria-label="Monto a reembolsar"
          />
          <div className="mt-2 flex flex-wrap gap-2" role="group" aria-label="Porcentajes rápidos">
            {[25, 50, 75, 100].map((pct) => (
              <button
                key={pct}
                type="button"
                onClick={() => applyPercent(pct)}
                aria-label={`Aplicar ${pct}% del monto`}
                className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-text-2 hover:bg-surface-elevated transition-colors min-h-[44px]"
              >
                {pct}%
              </button>
            ))}
          </div>
          <p className="mt-2 text-sm text-text-2">
            Monto a reembolsar:{' '}
            {formatCurrency(Number.isNaN(amountCents) ? null : amountCents / 100, { fractionDigits: 2 })}
          </p>
          {amountError && (
            <p className="mt-2 text-sm text-rose-600" role="alert">
              {amountError}
            </p>
          )}
        </div>

        {error && (
          <div className="bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/30 rounded-lg py-2 px-3 mb-4 text-sm" role="alert">
            {error}
          </div>
        )}

        <div className="flex gap-3 justify-end">
          <Button variant="secondary" onClick={onCancel} disabled={refunding} className="min-h-[44px]">
            Cancelar
          </Button>
          <Button variant="danger" onClick={handleConfirm} disabled={refunding} className="min-h-[44px]">
            {refunding ? 'Reembolsando…' : 'Reembolsar'}
          </Button>
        </div>
      </div>
    </div>
  )
}

export default function AdminPurchases() {
  const { id } = useParams()
  const queryClient = useQueryClient()

  const [refundTarget, setRefundTarget] = useState(null)
  const [refundError, setRefundError] = useState('')
  const [search, setSearch] = useState('')

  const queryKey = ['admin', 'purchases', id]

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey,
    queryFn: async () => {
      const response = await apiClient.get(`/admin/events/${id}/purchases`)
      return response.data
    },
    enabled: Boolean(id),
  })

  const refundMutation = useMutation({
    mutationFn: async ({ reservationId, quantity, amount }) => {
      // APR-010: the wire carries ONLY { quantity, amount } — never a percent.
      const response = await apiClient.post(`/admin/events/${id}/purchases/${reservationId}/refund`, { quantity, amount })
      return response.data
    },
    onSuccess: () => {
      setRefundTarget(null)
      setRefundError('')
      // APR-010: invalidate so the list reflects the new Refunded state.
      queryClient.invalidateQueries({ queryKey })
    },
  })

  const handleConfirmRefund = (quantity, amount) => {
    if (!refundTarget) return
    setRefundError('')
    refundMutation.mutate({ reservationId: refundTarget.reservationId, quantity, amount }, {
      onError: (err) => setRefundError(getErrorMessage(err)),
    })
  }

  const handleCancelRefund = () => {
    setRefundError('')
    setRefundTarget(null)
  }

  // APR-016: display-only derivations — NEVER mutate purchase.amount (the refund
  // dialog derives unitPriceCents/capCents from it). Y comes from the payload verbatim.
  const totalAmount = data?.purchases?.reduce((sum, purchase) => sum + purchase.amount, 0) ?? 0
  const netAmount = data ? totalAmount - data.totalRefunded : 0

  // Client-side smart search: compact a row when the free-text term matches the
  // purchaser email (case-insensitive substring) OR the DNI (substring). Empty
  // term matches everything. Purely derived — never mutates data.purchases.
  const filteredPurchases = (data?.purchases ?? []).filter((purchase) => {
    const q = search.trim().toLowerCase()
    if (q === '') return true
    return (
      (purchase.purchaserEmail || '').toLowerCase().includes(q) ||
      (purchase.purchaserDni || '').toLowerCase().includes(q)
    )
  })
  // Newest-first ordering (most recent purchasedAt at the top). Derived copy —
  // the stat cards and refund dialog still read the raw `data.purchases` rows.
  const sortedPurchases = [...filteredPurchases].sort(
    (a, b) => new Date(b.purchasedAt) - new Date(a.purchasedAt)
  )

  return (
    <div className="relative -mt-16 min-h-[calc(100svh-56px)] bg-gradient-to-b from-cian/10 via-canvas to-amarillo/10">
      {/* Gradient background identical to the "Mis entradas" (TicketLookup) page.
          It starts at the very top, behind the translucent fixed navbar, so there
          is no white gap between the navbar and the page background. */}
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-20 pb-12">
      <header className="mb-8">
        <h1 className="text-2xl md:text-3xl font-display font-bold text-text-1 text-center mb-2">
          Compras del evento
        </h1>
        {data && (
          <p className="text-lg md:text-2xl font-display text-text-2 text-center">
            {data.eventName}
          </p>
        )}
      </header>

      {data && !isLoading && !isError && (
        <section
          aria-label="Resumen de la compra del evento"
          className="mb-6 mx-auto grid grid-cols-1 gap-3 sm:grid-cols-3 max-w-3xl"
        >
          <GlassCard
            data-testid="stat-total"
            className="p-4 border-t-4"
            style={{ borderTopColor: 'var(--purpura)' }}
          >
            <p className="text-xs text-text-2 font-sans">Total</p>
            <p className="text-lg md:text-xl font-display font-bold text-text-1 mt-1">
              {formatCurrency(totalAmount)}
            </p>
          </GlassCard>
          <GlassCard
            data-testid="stat-reembolsado"
            className="p-4 border-t-4"
            style={{ borderTopColor: 'var(--naranja)' }}
          >
            <p className="text-xs text-text-2 font-sans">Reembolsado</p>
            <p className="text-lg md:text-xl font-display font-bold text-text-1 mt-1">
              {formatCurrency(data.totalRefunded)}
            </p>
          </GlassCard>
          <GlassCard
            data-testid="stat-neto"
            className="p-4 border-t-4"
            style={{ borderTopColor: 'var(--verde)' }}
          >
            <p className="text-xs text-text-2 font-sans">Neto</p>
            <p className="text-lg md:text-xl font-display font-bold text-text-1 mt-1">
              {formatCurrency(netAmount)}
            </p>
          </GlassCard>
        </section>
      )}

      {isLoading && (
        <GlassCard className="py-12">
          <div className="flex flex-col items-center gap-4" role="status" aria-label="Cargando compras…">
            <Skeleton width="240px" height="18px" />
            <Skeleton width="180px" height="18px" />
          </div>
        </GlassCard>
      )}

      {isError && (
        <GlassCard className="text-center py-12" role="alert">
          <p className="text-text-1 mb-3">{getErrorMessage(error)}</p>
          <Button variant="secondary" onClick={() => refetch()}>
            Reintentar
          </Button>
        </GlassCard>
      )}

      {data && !isLoading && !isError && (
        <GlassCard className="p-6">
          {data.purchases.length > 0 && (
            <div className="mb-4">
              <label htmlFor="purchase-search" className="sr-only">
                Buscar compras
              </label>
              <input
                id="purchase-search"
                type="search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Buscar por comprador o DNI…"
                aria-label="Buscar compras"
                className="w-full sm:w-72 bg-white/60 border border-gris-oscuro/15 rounded-lg px-3 py-2 text-sm text-gris-oscuro placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-brand-1 focus:border-transparent"
              />
            </div>
          )}
          {data.purchases.length === 0 ? (
            <p className="text-text-2 text-center py-8">No hay compras confirmadas para este evento.</p>
          ) : sortedPurchases.length === 0 ? (
            <p className="text-text-2 text-center py-8">No se encontraron compras con ese buscador.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="admin-table w-full border-collapse text-left text-sm">
                <thead>
                  <tr className="border-b-2 border-border">
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Comprador</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">DNI</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Tipo</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Cantidad</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Monto</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Fecha</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Estado</th>
                    <th className="py-3 px-4 text-text-1 font-semibold whitespace-nowrap">Acción</th>
                  </tr>
                </thead>
                <tbody>
                  {sortedPurchases.map((purchase) => {
                    const badge = refundBadge(purchase.quantity, purchase.refundedQuantity)
                    return (
                      <tr key={purchase.reservationId} className="border-b border-border hover:bg-surface-elevated transition-colors">
                        <td className="py-3.5 px-4 text-text-1 align-middle" data-label="Comprador">
                          {purchase.purchaserEmail}
                          {purchase.linkUnverified && (
                            <div>
                              <Badge variant="warning">Vínculo no verificado</Badge>
                            </div>
                          )}
                        </td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="DNI">{purchase.purchaserDni}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Tipo">{purchase.ticketType}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Cantidad">{purchase.quantity}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Monto">{formatCurrency(purchase.refundedQuantity > 0 ? purchase.amount - purchase.refundedAmount : purchase.amount)}</td>
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Fecha">{formatDate(purchase.purchasedAt)}</td>
                        <td className="py-3.5 px-4 align-middle" data-label="Estado">
                          <Badge variant={badge.variant}>{badge.label}</Badge>
                        </td>
                        <td className="py-3.5 px-4 align-middle" data-label="Acción">
                          <Button
                            variant="danger"
                            size="sm"
                            disabled={purchase.refundedQuantity >= purchase.quantity}
                            onClick={() => setRefundTarget(purchase)}
                            aria-label={`Reembolsar compra de ${purchase.purchaserEmail}`}
                            className="min-h-[44px]"
                          >
                            Reembolsar
                          </Button>
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          )}
        </GlassCard>
      )}

      {refundTarget && (
        <RefundConfirmationDialog
          purchase={refundTarget}
          eventName={data?.eventName || ''}
          onConfirm={handleConfirmRefund}
          onCancel={handleCancelRefund}
          refunding={refundMutation.isPending}
          error={refundError}
        />
      )}
      </div>
    </div>
  )
}
