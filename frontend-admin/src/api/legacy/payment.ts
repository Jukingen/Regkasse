/**
 * ADMIN-ONLY legacy Payment API boundary (GET/POST /api/Payment/*).
 *
 * - Mobile POS must NOT use this module. POS canonical client: `frontend/services/api/paymentService.ts`
 *   + `posPaymentPaths.ts` → `/api/pos/payment/*`.
 * - Do not import `@/api/generated/payment/payment` directly in pages; use hooks/wrappers from here
 *   so list/detail/statistics/cancel/refund stay on the legacy surface until a deliberate admin migration.
 * - Mixing POS and admin payment clients in one feature is a regression risk — keep boundaries separate.
 */

import {
  useGetApiPaymentDateRange,
  useGetApiPaymentId,
  useGetApiPaymentStatistics,
  getGetApiPaymentDateRangeQueryKey,
  getGetApiPaymentIdQueryKey,
} from '@/api/generated/payment/payment';
import type { GetApiPaymentDateRangeParams } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';

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
  params?: { startDate?: string; endDate?: string },
  options?: Parameters<typeof useGetApiPaymentStatistics>[0]
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
