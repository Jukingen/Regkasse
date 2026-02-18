import { useQueryClient } from '@tanstack/react-query';
import {
    useGetApiCustomer,
    useGetApiCustomerSearch,
    useGetApiCustomerId,
    usePostApiCustomer,
    usePutApiCustomerId,
    useDeleteApiCustomerId,
    useGetApiCustomerCount
} from '@/api/generated/customer/customer';
import { useURLFilters } from '@/hooks/useURLFilters';
import { Customer } from '@/api/generated/model';

// 1. Key Factory
export const customerKeys = {
    all: ['customers'] as const,
    lists: () => [...customerKeys.all, 'list'] as const,
    list: (filters: string) => [...customerKeys.lists(), { filters }] as const,
    details: () => [...customerKeys.all, 'detail'] as const,
    detail: (id: string) => [...customerKeys.details(), id] as const,
    count: () => [...customerKeys.all, 'count'] as const,
};

// 2. Filter Hook
export function useCustomerFilters() {
    return useURLFilters<{
        page: number;
        pageSize: number;
        search: string;
    }>();
}

// 3. Main Hook
export function useCustomers() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: customerKeys.lists() });
        queryClient.invalidateQueries({ queryKey: customerKeys.count() });
        queryClient.invalidateQueries({ queryKey: ['/api/Customer'] });
    };

    return {
        // Queries
        useList: (params?: { page?: number; pageSize?: number }) =>
            useGetApiCustomer(params, {
                query: {
                    queryKey: customerKeys.list(JSON.stringify(params))
                }
            }),
        useSearch: useGetApiCustomerSearch,
        useDetail: useGetApiCustomerId,
        useCount: useGetApiCustomerCount,

        // Mutations
        useCreate: usePostApiCustomer,
        useUpdate: usePutApiCustomerId,
        useDelete: useDeleteApiCustomerId,

        // Utils
        invalidateList,
        keys: customerKeys,
    };
}
