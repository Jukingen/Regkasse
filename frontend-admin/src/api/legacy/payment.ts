/**
 * ADMIN-ONLY legacy Payment API boundary (GET/POST /api/Payment/*).
 *
 * - Mobile POS must NOT use this module. POS canonical client: `frontend/services/api/paymentService.ts`
 *   + `posPaymentPaths.ts` → `/api/pos/payment/*`.
 * - Do not import `@/api/generated/payment/payment` directly in pages; use hooks/wrappers from here
 *   so list/detail/statistics/cancel/refund stay on the legacy surface until a deliberate admin migration.
 * - Mixing POS and admin payment clients in one feature is a regression risk — keep boundaries separate.
 * - RKSV/offline/replay correlation tooling: `@/api/admin-incident` and `@/api/replay-batch` (not this module).
 */

import {
  useGetApiPaymentDateRange,
  useGetApiPaymentId,
  useGetApiPaymentStatistics,
  getGetApiPaymentDateRangeQueryKey,
  getGetApiPaymentIdQueryKey,
} from '@/api/generated/payment/payment';
import type { GetApiPaymentDateRangeParams, GetApiPaymentStatisticsParams } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';

/**
 * GET /api/Payment/{id} often returns `{ success, message, data: payment, timestamp }`.
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
  list: (params?: GetApiPaymentDateRangeParams) =>
    [...legacyPaymentQueryKeys.all, 'date-range', params] as const,
  detail: (id: string) => [...legacyPaymentQueryKeys.all, 'detail', id] as const,
  /** Align with generated key for compatibility. */
  dateRangeKey: getGetApiPaymentDateRangeQueryKey,
  detailKey: getGetApiPaymentIdQueryKey,
} as const;

/**
 * Payments list by date range. Uses GET /api/Payment/date-range (legacy).
 * Prefer this over importing useGetApiPaymentDateRange directly.
 */
export function useLegacyPaymentList(
  params?: GetApiPaymentDateRangeParams,
  options?: Parameters<typeof useGetApiPaymentDateRange>[1]
) {
  return useGetApiPaymentDateRange(params, options);
}

/**
 * Single payment by id. Uses GET /api/Payment/{id} (legacy).
 */
export function useLegacyPaymentById(
  id: string,
  options?: Parameters<typeof useGetApiPaymentId>[1]
) {
  return useGetApiPaymentId(id, options);
}

/**
 * Payment statistics in date range. Uses GET /api/Payment/statistics (legacy).
 */
export function useLegacyPaymentStatistics(
  params?: GetApiPaymentStatisticsParams,
  options?: Parameters<typeof useGetApiPaymentStatistics>[1]
) {
  return useGetApiPaymentStatistics(params, options);
}

export interface LegacyPaymentActionRequest {
  reason: string;
}

export interface LegacyPaymentRefundRequest extends LegacyPaymentActionRequest {
  amount: number;
}

export async function cancelLegacyPayment(id: string, payload: LegacyPaymentActionRequest): Promise<unknown> {
  return customInstance<unknown>({
    url: `/api/Payment/${id}/cancel`,
    method: 'POST',
    data: payload,
  });
}

export async function refundLegacyPayment(id: string, payload: LegacyPaymentRefundRequest): Promise<unknown> {
  return customInstance<unknown>({
    url: `/api/Payment/${id}/refund`,
    method: 'POST',
    data: payload,
  });
}

/** Re-export for consumers that need the raw fetcher (e.g. prefetch). */
export {
  getGetApiPaymentDateRangeQueryKey,
  getGetApiPaymentIdQueryKey,
} from '@/api/generated/payment/payment';
