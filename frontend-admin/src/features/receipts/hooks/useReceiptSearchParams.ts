'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { useCallback, useMemo } from 'react';
import {
    ReceiptListParams,
    RECEIPT_LIST_DEFAULTS,
} from '@/features/receipts/types/receipts';

/**
 * Bidirectional sync between URL searchParams and typed ReceiptListParams.
 *
 * - URL is the single source of truth (no Zustand / useState).
 * - Changing any filter automatically resets page to 1.
 * - Missing params fall back to RECEIPT_LIST_DEFAULTS.
 */
export function useReceiptSearchParams() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const pathname = usePathname();

    /** Parse URL searchParams into typed params with defaults */
    const params: ReceiptListParams = useMemo(() => {
        const raw = Object.fromEntries(searchParams.entries());
        return {
            page: raw.page ? parseInt(raw.page, 10) : RECEIPT_LIST_DEFAULTS.page,
            pageSize: raw.pageSize ? parseInt(raw.pageSize, 10) : RECEIPT_LIST_DEFAULTS.pageSize,
            sort: raw.sort || RECEIPT_LIST_DEFAULTS.sort,
            receiptNumber: raw.receiptNumber || undefined,
            cashRegisterId: raw.cashRegisterId || undefined,
            cashierId: raw.cashierId || undefined,
            issuedFrom: raw.issuedFrom || undefined,
            issuedTo: raw.issuedTo || undefined,
        };
    }, [searchParams]);

    /** Merge partial updates into URL. Filter changes reset page to 1. */
    const setParams = useCallback(
        (partial: Partial<ReceiptListParams>) => {
            const isFilterChange = Object.keys(partial).some(
                (k) => k !== 'page' && k !== 'pageSize' && k !== 'sort',
            );

            const merged: ReceiptListParams = {
                ...params,
                ...partial,
                // Reset page when filters change
                page: isFilterChange ? 1 : (partial.page ?? params.page),
            };

            const qp = new URLSearchParams();
            // Only write non-default, non-undefined values to keep URL clean
            if (merged.page !== RECEIPT_LIST_DEFAULTS.page) qp.set('page', String(merged.page));
            if (merged.pageSize !== RECEIPT_LIST_DEFAULTS.pageSize) qp.set('pageSize', String(merged.pageSize));
            if (merged.sort && merged.sort !== RECEIPT_LIST_DEFAULTS.sort) qp.set('sort', merged.sort);
            if (merged.receiptNumber) qp.set('receiptNumber', merged.receiptNumber);
            if (merged.cashRegisterId) qp.set('cashRegisterId', merged.cashRegisterId);
            if (merged.cashierId) qp.set('cashierId', merged.cashierId);
            if (merged.issuedFrom) qp.set('issuedFrom', merged.issuedFrom);
            if (merged.issuedTo) qp.set('issuedTo', merged.issuedTo);

            const qs = qp.toString();
            router.push(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
        },
        [params, pathname, router],
    );

    /** Clear all filters and reset to defaults */
    const resetFilters = useCallback(() => {
        router.push(pathname, { scroll: false });
    }, [pathname, router]);

    return { params, setParams, resetFilters };
}
