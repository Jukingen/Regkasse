import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { getReceiptListForensics } from '@/features/receipts/api/forensics-client';
import type { ReceiptListParams } from '@/features/receipts/types/receipts';

// ---------------------------------------------------------------------------
// Query Key Factory
// ---------------------------------------------------------------------------

export const RECEIPT_KEYS = {
  all: ['admin', 'receipts'] as const,
  lists: () => [...RECEIPT_KEYS.all, 'list'] as const,
  list: (params: ReceiptListParams) => [...RECEIPT_KEYS.lists(), params] as const,
  details: () => [...RECEIPT_KEYS.all, 'detail'] as const,
  detail: (id: string) => [...RECEIPT_KEYS.details(), id] as const,
} as const;

/**
 * Fetches the paginated receipts list.
 *
 * - Uses `placeholderData: keepPreviousData` so the table doesn't flash empty
 *   between page/filter transitions.
 * - The full params object is part of the queryKey, giving automatic cache
 *   separation per filter+page combo.
 */
export function useReceiptListQuery(params: ReceiptListParams) {
  return useQuery({
    queryKey: RECEIPT_KEYS.list(params),
    queryFn: () => getReceiptListForensics(params),
    placeholderData: keepPreviousData,
    staleTime: 30_000, // match global default
  });
}
