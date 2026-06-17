import { keepPreviousData, useQuery, type UseQueryOptions } from '@tanstack/react-query';
import type { AdminPaymentListItemDto, FilterSummaryDto, PaymentListResponse } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import type { AdminPaymentsListQueryParams } from '@/features/payments/utils/paymentFiltersToApiParams';

export type NormalizedAdminPaymentsList = {
    items: AdminPaymentListItemDto[];
    total: number | null;
    page: number;
    pageSize: number;
    activeFilters?: FilterSummaryDto;
    nextCursor?: string | null;
    hasMore: boolean;
};

function normalizeResponse(raw: PaymentListResponse): NormalizedAdminPaymentsList {
    return {
        items: raw.items ?? [],
        total: raw.totalCount != null ? raw.totalCount : null,
        page: raw.page ?? 1,
        pageSize: raw.pageSize ?? 50,
        activeFilters: raw.activeFilters,
        nextCursor: raw.nextCursor,
        hasMore: raw.hasMore ?? false,
    };
}

export async function fetchAdminPaymentsList(
    params: AdminPaymentsListQueryParams,
    signal?: AbortSignal,
): Promise<NormalizedAdminPaymentsList> {
    const res = await customInstance<PaymentListResponse>({
        url: '/api/admin/payments',
        method: 'GET',
        params,
        paramsSerializer: { indexes: null },
        signal,
    });
    return normalizeResponse(res);
}

export function adminPaymentsListQueryKey(params: AdminPaymentsListQueryParams) {
    return ['/api/admin/payments', 'list', params] as const;
}

export function useAdminPaymentsList<TData = NormalizedAdminPaymentsList>(
    params: AdminPaymentsListQueryParams,
    options?: {
        query?: Partial<UseQueryOptions<NormalizedAdminPaymentsList, unknown, TData>>;
    },
) {
    const { query: queryOptions } = options ?? {};
    return useQuery({
        queryKey: adminPaymentsListQueryKey(params),
        queryFn: ({ signal }) => fetchAdminPaymentsList(params, signal),
        placeholderData: keepPreviousData,
        ...queryOptions,
    });
}
