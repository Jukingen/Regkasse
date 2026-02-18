import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { customInstance } from '@/lib/axios';
import type {
    ReceiptListParams,
    ReceiptListResponse,
} from '@/features/receipts/types/receipts';

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

// ---------------------------------------------------------------------------
// Fetcher
// ---------------------------------------------------------------------------

/** Build query string from ReceiptListParams, omitting undefined values */
function buildQuery(params: ReceiptListParams): string {
    const qp = new URLSearchParams();
    qp.set('page', String(params.page));
    qp.set('pageSize', String(params.pageSize));
    if (params.sort) qp.set('sort', params.sort);
    if (params.receiptNumber) qp.set('receiptNumber', params.receiptNumber);
    if (params.cashRegisterId) qp.set('cashRegisterId', params.cashRegisterId);
    if (params.cashierId) qp.set('cashierId', params.cashierId);
    if (params.issuedFrom) qp.set('issuedFrom', params.issuedFrom);
    if (params.issuedTo) qp.set('issuedTo', params.issuedTo);
    return qp.toString();
}

async function fetchReceiptList(params: ReceiptListParams): Promise<ReceiptListResponse> {
    return customInstance<ReceiptListResponse>({
        url: `/admin/receipts?${buildQuery(params)}`,
        method: 'GET',
    });
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

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
        queryFn: () => fetchReceiptList(params),
        placeholderData: keepPreviousData,
        staleTime: 30_000, // match global default
    });
}
