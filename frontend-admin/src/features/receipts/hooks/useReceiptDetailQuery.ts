import { useQuery } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import type { ReceiptDetailDto } from '@/features/receipts/types/receipts';
import { RECEIPT_KEYS } from './useReceiptListQuery';

// ---------------------------------------------------------------------------
// Fetcher
// ---------------------------------------------------------------------------

async function fetchReceiptDetail(receiptId: string): Promise<ReceiptDetailDto> {
    return customInstance<ReceiptDetailDto>({
        url: `/admin/receipts/${receiptId}`,
        method: 'GET',
    });
}

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
        queryFn: () => fetchReceiptDetail(receiptId!),
        enabled: !!receiptId,
        staleTime: 30_000,
    });
}
