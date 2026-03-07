/**
 * Legacy Payment API boundary. All admin usage of /api/Payment/* goes through this module.
 * Do not import from @/api/generated/payment/payment in pages or features.
 * Replacement direction: /api/admin/* or /api/pos/* when backend provides them.
 */

import {
  useGetApiPaymentDateRange,
  useGetApiPaymentId,
  getGetApiPaymentDateRangeQueryKey,
  getGetApiPaymentIdQueryKey,
} from '@/api/generated/payment/payment';
import type { GetApiPaymentDateRangeParams } from '@/api/generated/model';

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

/** Re-export for consumers that need the raw fetcher (e.g. prefetch). */
export {
  getGetApiPaymentDateRangeQueryKey,
  getGetApiPaymentIdQueryKey,
} from '@/api/generated/payment/payment';
