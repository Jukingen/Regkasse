/**
 * Ürün listesi, CRUD, stok: tüm çağrılar /api/admin/products (generated product hook'ları kullanılmıyor).
 */
import { useQueryClient } from '@tanstack/react-query';
import {
    useAdminProductsList,
    useAdminProductsSearch,
    useAdminProductById,
    useCreateAdminProduct,
    useUpdateAdminProduct,
    useDeleteAdminProduct,
    useUpdateAdminProductStock,
    adminProductsQueryKeys,
} from '@/api/admin/products';
import { useURLFilters } from '@/hooks/useURLFilters';

export const productKeys = {
    all: adminProductsQueryKeys.all,
    lists: adminProductsQueryKeys.lists,
    list: (filters: string) => [...adminProductsQueryKeys.lists(), { filters }] as const,
    details: adminProductsQueryKeys.details,
    detail: (id: string) => adminProductsQueryKeys.detail(id),
};

export function useProductFilters() {
    return useURLFilters<{
        page: number;
        pageSize: number;
        search: string;
        category: string;
    }>();
}

export function useProducts() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: productKeys.lists() });
        queryClient.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() });
    };

    return {
        useList: (
            params?: { page?: number; pageSize?: number },
            options?: { query?: { enabled?: boolean } }
        ) =>
            useAdminProductsList(
                {
                    pageNumber: params?.page,
                    pageSize: params?.pageSize,
                },
                {
                    queryKey: productKeys.list(JSON.stringify(params)),
                    ...options?.query,
                }
            ),
        useSearch: (params: { name?: string }, options?: { query?: { enabled?: boolean } }) =>
            useAdminProductsSearch(
                { name: params?.name, category: params?.category },
                { enabled: options?.query?.enabled ?? !!params?.name }
            ),
        useDetail: (id: string, options?: { query?: { enabled?: boolean } }) =>
            useAdminProductById(id, options?.query),

        useCreate: useCreateAdminProduct,
        useUpdate: useUpdateAdminProduct,
        useDelete: useDeleteAdminProduct,
        useUpdateStock: useUpdateAdminProductStock,

        invalidateList,
        keys: productKeys,
    };
}
