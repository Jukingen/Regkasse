/**
 * Product list, CRUD, stock: all calls use /api/admin/products (generated product hooks are not used).
 * Single list query supports pagination and optional name/categoryId filter.
 */
import { useQueryClient } from '@tanstack/react-query';
import type { AdminProductsListParams } from '@/api/admin/products';
import {
    useAdminProductsList,
    useAdminProductById,
    useCreateAdminProduct,
    useUpdateAdminProduct,
    useDeleteAdminProduct,
    useUpdateAdminProductStock,
    useSetAdminProductModifierGroups,
    adminProductsQueryKeys,
} from '@/api/admin/products';
import { useURLFilters } from '@/hooks/useURLFilters';

export const productKeys = {
    all: adminProductsQueryKeys.all,
    lists: adminProductsQueryKeys.lists,
    list: (params?: AdminProductsListParams) => [...adminProductsQueryKeys.lists(), params] as const,
    details: adminProductsQueryKeys.details,
    detail: (id: string) => adminProductsQueryKeys.detail(id),
};

export function useProductFilters() {
    return useURLFilters<{
        page: string;
        pageSize: string;
        search: string;
        categoryId: string;
    }>();
}

export function useProducts() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: adminProductsQueryKeys.lists() });
    };

    return {
        /** Single list query with optional pagination and filters (name, categoryId). */
        useList: (params?: AdminProductsListParams, options?: Parameters<typeof useAdminProductsList>[1]) =>
            useAdminProductsList(params, options),

        useDetail: (id: string, options?: Parameters<typeof useAdminProductById>[1]) =>
            useAdminProductById(id, options),

        useCreate: useCreateAdminProduct,
        useUpdate: useUpdateAdminProduct,
        useDelete: useDeleteAdminProduct,
        useUpdateStock: useUpdateAdminProductStock,
        useSetModifierGroups: useSetAdminProductModifierGroups,

        invalidateList,
        keys: productKeys,
    };
}
