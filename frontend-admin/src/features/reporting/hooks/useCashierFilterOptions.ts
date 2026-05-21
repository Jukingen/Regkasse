'use client';

import { useMemo, useState } from 'react';
import { useGetApiUserManagement } from '@/api/generated/user-management/user-management';
import { useDebounce } from '@/hooks/useDebounce';

const CASHIER_LOOKUP_PAGE_SIZE = 50;

/**
 * Server-backed cashier dropdown for operational reporting filters (replaces pageSize 500 preload).
 */
export function useCashierFilterOptions() {
    const [search, setSearch] = useState('');
    const debouncedQuery = useDebounce(search, 300);

    const { data, isLoading, isFetching } = useGetApiUserManagement(
        {
            page: 1,
            pageSize: CASHIER_LOOKUP_PAGE_SIZE,
            isActive: true,
            query: debouncedQuery.trim() || undefined,
        },
        {
            query: {
                staleTime: 60_000,
                refetchOnWindowFocus: false,
            },
        },
    );

    const options = useMemo(
        () =>
            (data?.items ?? [])
                .filter((u) => u.id)
                .map((u) => ({
                    value: u.id as string,
                    label: [u.firstName, u.lastName].filter(Boolean).join(' ') || u.userName || u.id,
                })),
        [data?.items],
    );

    return {
        options,
        loading: isLoading || isFetching,
        onSearch: setSearch,
    };
}
