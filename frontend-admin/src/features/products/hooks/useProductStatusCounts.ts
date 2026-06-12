'use client';

import { useCallback, useMemo } from 'react';
import { useQueries } from '@tanstack/react-query';
import { getAdminProductsList, adminProductsQueryKeys } from '@/api/admin/products';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

export type ProductStatusCounts = {
    active: number;
    inactive: number;
    all: number;
    isLoading: boolean;
    isFetching: boolean;
    refetch: () => void;
};

export function useProductStatusCounts(enabled = true): ProductStatusCounts {
    const { tenantSlug } = useCurrentTenant();

    const queries = useQueries({
        queries: [
            {
                queryKey: adminProductsQueryKeys.list(tenantSlug, {
                    isActive: 'true',
                    page: 1,
                    pageSize: 1,
                }),
                queryFn: ({ signal }) =>
                    getAdminProductsList({ isActive: 'true', page: 1, pageSize: 1 }, undefined, signal),
                enabled,
                staleTime: 30_000,
            },
            {
                queryKey: adminProductsQueryKeys.list(tenantSlug, {
                    isActive: 'false',
                    page: 1,
                    pageSize: 1,
                }),
                queryFn: ({ signal }) =>
                    getAdminProductsList({ isActive: 'false', page: 1, pageSize: 1 }, undefined, signal),
                enabled,
                staleTime: 30_000,
            },
            {
                queryKey: adminProductsQueryKeys.list(tenantSlug, {
                    isActive: 'all',
                    page: 1,
                    pageSize: 1,
                }),
                queryFn: ({ signal }) =>
                    getAdminProductsList({ isActive: 'all', page: 1, pageSize: 1 }, undefined, signal),
                enabled,
                staleTime: 30_000,
            },
        ],
    });

    const refetch = useCallback(() => {
        void queries[0]?.refetch();
        void queries[1]?.refetch();
        void queries[2]?.refetch();
    }, [queries]);

    return useMemo(
        () => ({
            active: queries[0]?.data?.pagination?.totalCount ?? 0,
            inactive: queries[1]?.data?.pagination?.totalCount ?? 0,
            all: queries[2]?.data?.pagination?.totalCount ?? 0,
            isLoading: queries.some((q) => q.isLoading),
            isFetching: queries.some((q) => q.isFetching),
            refetch,
        }),
        [queries, refetch],
    );
}
