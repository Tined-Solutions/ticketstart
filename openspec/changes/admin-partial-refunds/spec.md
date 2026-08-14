# Delta Spec — Admin Partial Refunds

## Purpose

Change `admin-partial-refunds` modifies capability `admin-purchase-refunds` (canonical spec: `openspec/specs/admin-purchase-refunds/spec.md`). Admins currently record refunds all-or-nothing; this change makes refunds partial by quantity (K of N, tickets fungible) and cumulative, recording each operation in a new `Refunds` table so the audit trail and `TotalRefunded` become exact. The Approved Transaction flips to `Refunded` only when an operation leaves zero active tickets, preserving the existing invariant "Refunded transaction == fully refunded purchase" and all per-ticket consumers (APR-005/006/009 unchanged).

## Delta Overview

| Type | ID | Requirement | Behavior |
|------|----|-------------|----------|
| ADDED | APR-012 | Cumulative refund operation record | One `Refunds` row per op (ReservationId, TicketIds[], Quantity, Amount, AdminId?, CreatedAt); `TotalRefunded` = Σ `Refunds.Amount`; row exposes RefundedQuantity/RefundedAmount; `Refunded` derived |
| ADDED | APR-013 | Deterministic ticket selection | K oldest non-refunded/non-used tickets selected under lock |
| ADDED | APR-014 | Legacy refund backfill | `AddRefundsTable` pure-SQL `INSERT…SELECT` backfill (AdminId null); TotalRefunded must not regress |
| ADDED | APR-015 | Non-goals as negative requirements | No MP call, no motivo, no per-ticket UI, no Reservation change, no auto-refund edits, no refund editing/reverting |
| MODIFIED | APR-002 | List event purchases | Row gains RefundedQuantity/RefundedAmount; `totalRefunded` = Σ `Refunds.Amount` |
| MODIFIED | APR-003 | Atomic quantity-based refund | POST body `{ quantity }` (validated > 0); partial + cumulative; flip to Refunded ONLY at 0 active; guards: any used / K ≤ 0 / K > active / no Approved tx |
| MODIFIED | APR-010 | Admin UI | Quantity selector (1..active) + live amount preview; "X de Y reembolsadas" badge (error/warning); button disabled when fully refunded; mutation posts `{ quantity }` |
| MODIFIED | APR-011 | Test coverage | Replace 5 binary-refund tests + ~10 new partial/cumulative/backfill tests; frontend mock shape + assertions |

Unchanged: APR-001 (admin auth), APR-004 (used-ticket block — ANY used blocks the whole op), APR-005/006/009 (per-ticket `!IsRefunded` already correct), APR-007 (audit after commit), APR-008 (no MP/motivo).

## Non-Goals

No Mercado Pago / external refund call (manual MP dashboard return; APR-008 stands). No motivo/refund reason. No per-ticket UI selection (selection by quantity — tickets fungible). No Reservation status change. No change to the auto-refund path (`PaymentService.InitiateRefundAsync` untouched). No editing or reverting a refund operation.

## References

- Full delta with requirements and scenarios: `openspec/changes/admin-partial-refunds/specs/admin-purchase-refunds/spec.md`
- Proposal: `openspec/changes/admin-partial-refunds/proposal.md`
- Exploration: `openspec/changes/admin-partial-refunds/explore.md`
