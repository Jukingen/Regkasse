import { useQueryClient } from '@tanstack/react-query';
import {
    useGetApiProduct,
    useGetApiProductSearch,
    useGetApiProductId,
    usePostApiProduct,
    usePutApiProductId,
    useDeleteApiProductId,
    usePutApiProductIdStock
} from '@/api/generated/product/product';
import { useURLFilters } from '@/hooks/useURLFilters';
import { Product } from '@/api/generated/model';

// 1. Key Factory
export const productKeys = {
    all: ['products'] as const,
    lists: () => [...productKeys.all, 'list'] as const,
    list: (filters: string) => [...productKeys.lists(), { filters }] as const,
    details: () => [...productKeys.all, 'detail'] as const,
    detail: (id: string) => [...productKeys.details(), id] as const,
};

// 2. Filter Hook
export function useProductFilters() {
    return useURLFilters<{
        page: number;
        pageSize: number;
        search: string;
        category: string;
    }>();
}

// 3. Main Hook
export function useProducts() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: productKeys.lists() });
        // Invalidate orval keys
        queryClient.invalidateQueries({ queryKey: ['/api/Product'] });
    };

    return {
        // Queries
        useList: (
            params?: { page?: number; pageSize?: number },
            options?: { query?: { enabled?: boolean } }
        ) =>
            useGetApiProduct(
                {
                    pageNumber: params?.page,
                    pageSize: params?.pageSize
                },
                {
                    query: {
                        queryKey: productKeys.list(JSON.stringify(params)),
                        ...options?.query
                    }
                }
            ),
        useSearch: useGetApiProductSearch,
        useDetail: useGetApiProductId,

        // Mutations
        useCreate: usePostApiProduct,
        useUpdate: usePutApiProductId,
        useDelete: useDeleteApiProductId,
        useUpdateStock: usePutApiProductIdStock,

        // Utils
        invalidateList,
        keys: productKeys,
    };
}
