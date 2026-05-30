import { keepPreviousData, useQuery, type UseQueryOptions } from '@tanstack/react-query';
import type { AdminPaymentListItemDto } from '@/api/generated/model';
import { customInstance } from '@/lib/axios';
import type { AdminPaymentsListQueryParams } from '@/features/payments/utils/paymentFiltersToApiParams';
import type { PaymentFilterSummary } from '@/features/payments/types/paymentFilters';

type PaymentListApiResponse = {
    items?: AdminPaymentListItemDto[] | null;
    total?: number;
    totalCount?: number;
    pageNumber?: number;
    page?: number;
    pageSize?: number;
    activeFilters?: PaymentFilterSummary;
};

export type NormalizedAdminPaymentsList = {
    items: AdminPaymentListItemDto[];
    total: number;
    page: number;
    pageSize: number;
    activeFilters?: PaymentFilterSummary;
};

function normalizeResponse(raw: PaymentListApiResponse): NormalizedAdminPaymentsList {
    return {
        items: raw.items ?? [],
        total: raw.totalCount ?? raw.total ?? 0,
        page: raw.page ?? raw.pageNumber ?? 1,
        pageSize: raw.pageSize ?? 50,
        activeFilters: raw.activeFilters,
    };
}

export async function fetchAdminPaymentsList(
    params: AdminPaymentsListQueryParams,
    signal?: AbortSignal,
): Promise<NormalizedAdminPaymentsList> {
    const res = await customInstance<PaymentListApiResponse>({
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
