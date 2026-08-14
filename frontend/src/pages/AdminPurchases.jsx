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
  const unitPrice = purchase.amount / purchase.quantity
  const ticketsLabel = purchase.quantity === 1 ? 'entrada' : 'entradas'
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
            onChange={(e) => setSelectedQuantity(Number(e.target.value))}
            className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-text-1 min-h-[44px]"
            aria-label="Cantidad a reembolsar"
          />
          <p className="mt-2 text-sm text-text-2">
            Reembolsar {selectedQuantity} × {formatCurrency(unitPrice)} ={' '}
            {formatCurrency(unitPrice * selectedQuantity)}
          </p>
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
          <Button variant="danger" onClick={() => onConfirm(selectedQuantity)} disabled={refunding} className="min-h-[44px]">
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
    mutationFn: async ({ reservationId, quantity }) => {
      const response = await apiClient.post(`/admin/events/${id}/purchases/${reservationId}/refund`, { quantity })
      return response.data
    },
    onSuccess: () => {
      setRefundTarget(null)
      setRefundError('')
      // APR-010: invalidate so the list reflects the new Refunded state.
      queryClient.invalidateQueries({ queryKey })
    },
  })

  const handleConfirmRefund = (quantity) => {
    if (!refundTarget) return
    setRefundError('')
    refundMutation.mutate({ reservationId: refundTarget.reservationId, quantity }, {
      onError: (err) => setRefundError(getErrorMessage(err)),
    })
  }

  const handleCancelRefund = () => {
    setRefundError('')
    setRefundTarget(null)
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <header className="mb-8">
        <h1 className="text-4xl md:text-5xl font-display font-bold text-text-1 text-center mb-2">
          Compras del evento
        </h1>
        {data && (
          <p className="text-text-2 text-center">
            {data.eventName} · Reembolsado: {formatCurrency(data.totalRefunded)}
          </p>
        )}
      </header>

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
          {data.purchases.length === 0 ? (
            <p className="text-text-2 text-center py-8">No hay compras confirmadas para este evento.</p>
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
                  {data.purchases.map((purchase) => {
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
                        <td className="py-3.5 px-4 text-text-2 align-middle" data-label="Monto">{formatCurrency(purchase.amount)}</td>
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
  )
}
