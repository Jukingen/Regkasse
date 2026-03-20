/**
 * @deprecated Legacy namespace kept for backward-compatible imports in admin pages.
 * Implementation now uses canonical POS payment endpoints (`/api/pos/payment/*`).
 * New code should migrate imports away from `api/legacy/payment`.
 * Manual-wrapper rule: keep only compatibility helpers here and document removal criteria.
 */

import {
  useGetApiPosPaymentDateRange,
  useGetApiPosPaymentId,
  useGetApiPosPaymentStatistics,
  getGetApiPosPaymentDateRangeQueryKey,
  getGetApiPosPaymentIdQueryKey,
} from '@/api/generated/pos/pos';
import type { GetApiPosPaymentDateRangeParams, GetApiPosPaymentStatisticsParams } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';

/**
 * GET /api/pos/payment/{id} often returns `{ success, message, data: payment, timestamp }`.
 * Returns the inner payment object when present; otherwise a flat object if it already looks like a payment row.
 */
export function unwrapLegacyPaymentDetailPayload(raw: unknown): Record<string, unknown> | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const inner = o.data;
  if (inner != null && typeof inner === 'object' && !Array.isArray(inner)) {
    return inner as Record<string, unknown>;
  }
  if (
    'transactionId' in o ||
    'totalAmount' in o ||
    'paymentMethod' in o ||
    'paymentMethodRaw' in o ||
    'id' in o
  ) {
    return o;
  }
  return null;
}

export interface LegacyPaymentOperationalDetail {
  paymentId: string;
  receiptNumber: string | null;
  receiptId: string | null;
  isOfflineOrigin: boolean;
  offlineTransactionId: string | null;
  replayBatchCorrelationId: string | null;
  finanzOnlineStatus: string | null;
  finanzOnlineError: string | null;
  finanzOnlineReferenceId: string | null;
  invoicePersisted: boolean | null;
}

function toNullableString(v: unknown): string | null {
  if (typeof v !== 'string') return null;
  const s = v.trim();
  return s.length > 0 ? s : null;
}

/** Maps legacy payment detail payload into stable operational fields for UI cards. */
export function normalizeLegacyPaymentOperationalDetail(
  paymentId: string,
  payload: Record<string, unknown> | null,
  receiptId?: string | null
): LegacyPaymentOperationalDetail {
  const receiptNumber = toNullableString(payload?.receiptNumber);
  const offlineTransactionId = toNullableString(payload?.offlineTransactionId);
  const replayBatchCorrelationId = toNullableString(payload?.offlineReplayBatchCorrelationId);
  const invoicePersistedRaw = payload?.invoicePersisted;
  const invoicePersisted = typeof invoicePersistedRaw === 'boolean' ? invoicePersistedRaw : null;

  return {
    paymentId,
    receiptNumber,
    receiptId: toNullableString(receiptId),
    isOfflineOrigin: offlineTransactionId != null,
    offlineTransactionId,
    replayBatchCorrelationId,
    finanzOnlineStatus: toNullableString(payload?.finanzOnlineStatus),
    finanzOnlineError: toNullableString(payload?.finanzOnlineError),
    finanzOnlineReferenceId: toNullableString(payload?.finanzOnlineReferenceId),
    invoicePersisted,
  };
}

/** Central query key namespace for legacy Payment. Use for invalidation and refetch. */
export const legacyPaymentQueryKeys = {
  all: ['legacy', 'payment'] as const,
  list: (params?: GetApiPosPaymentDateRangeParams) =>
    [...legacyPaymentQueryKeys.all, 'date-range', params] as const,
  detail: (id: string) => [...legacyPaymentQueryKeys.all, 'detail', id] as const,
  /** Align with generated key for compatibility. */
  dateRangeKey: getGetApiPosPaymentDateRangeQueryKey,
  detailKey: getGetApiPosPaymentIdQueryKey,
} as const;

/**
 * Payments list by date range. Uses GET /api/pos/payment/date-range.
 * Prefer this over importing useGetApiPaymentDateRange directly.
 */
export function useLegacyPaymentList(
  params?: GetApiPosPaymentDateRangeParams,
  options?: Parameters<typeof useGetApiPosPaymentDateRange>[1]
) {
  return useGetApiPosPaymentDateRange(params, options);
}

/**
 * Single payment by id. Uses GET /api/pos/payment/{id}.
 */
export function useLegacyPaymentById(
  id: string,
  options?: Parameters<typeof useGetApiPosPaymentId>[1]
) {
  return useGetApiPosPaymentId(id, options);
}

/**
 * Payment statistics in date range. Uses GET /api/pos/payment/statistics.
 */
export function useLegacyPaymentStatistics(
  params?: GetApiPosPaymentStatisticsParams,
  options?: Parameters<typeof useGetApiPosPaymentStatistics>[1]
) {
  return useGetApiPosPaymentStatistics(params, options);
}

export interface LegacyPaymentActionRequest {
  reason: string;
}

export interface LegacyPaymentRefundRequest extends LegacyPaymentActionRequest {
  amount: number;
}

export async function cancelLegacyPayment(id: string, payload: LegacyPaymentActionRequest): Promise<unknown> {
  return customInstance<unknown>({
    url: `/api/pos/payment/${id}/cancel`,
    method: 'POST',
    data: payload,
  });
}

export async function refundLegacyPayment(id: string, payload: LegacyPaymentRefundRequest): Promise<unknown> {
  return customInstance<unknown>({
    url: `/api/pos/payment/${id}/refund`,
    method: 'POST',
    data: payload,
  });
}

/** Re-export for consumers that need the raw fetcher (e.g. prefetch). */
export {
  getGetApiPosPaymentDateRangeQueryKey as getGetApiPaymentDateRangeQueryKey,
  getGetApiPosPaymentIdQueryKey as getGetApiPaymentIdQueryKey,
} from '@/api/generated/pos/pos';
