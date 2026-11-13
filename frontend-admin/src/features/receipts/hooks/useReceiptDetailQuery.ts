import { useQuery } from '@tanstack/react-query';

import { getReceiptDetailForensics } from '@/features/receipts/api/forensics-client';

import { RECEIPT_KEYS } from './useReceiptListQuery';

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

/**
 * Fetches a single receipt by ID (includes items + taxLines).
 * Disabled when receiptId is falsy.
 */
export function useReceiptDetailQuery(receiptId: string | undefined) {
  return useQuery({
    queryKey: RECEIPT_KEYS.detail(receiptId!),
    queryFn: () => getReceiptDetailForensics(receiptId!),
    enabled: !!receiptId,
    staleTime: 30_000,
  });
}
