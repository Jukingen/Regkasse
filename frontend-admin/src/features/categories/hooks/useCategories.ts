import { useQueryClient } from '@tanstack/react-query';
import {
    useAdminCategoriesList,
    useAdminCategoriesSearch,
    useAdminCategoryProducts,
    useCreateAdminCategory,
    useUpdateAdminCategory,
    useDeleteAdminCategory,
    adminCategoriesQueryKeys,
} from '@/api/admin/categories';
import type { CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';

export const categoryKeys = {
    all: adminCategoriesQueryKeys.all,
    lists: adminCategoriesQueryKeys.lists,
    details: adminCategoriesQueryKeys.details,
    detail: (id: string) => adminCategoriesQueryKeys.detail(id),
    products: (id: string) => adminCategoriesQueryKeys.products(id),
};

export function useCategories() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: categoryKeys.lists() });
        queryClient.invalidateQueries({ queryKey: adminCategoriesQueryKeys.lists() });
    };

    return {
        useList: () =>
            useAdminCategoriesList({
                queryKey: categoryKeys.lists(),
            }),
        useSearch: (query: string) =>
            useAdminCategoriesSearch(query, {
                queryKey: [...categoryKeys.lists(), 'search', query],
                enabled: !!query,
            }),
        useProductsByCategory: (id: string) =>
            useAdminCategoryProducts(id, {
                queryKey: categoryKeys.products(id),
                enabled: !!id,
            }),

        useCreate: useCreateAdminCategory,
        useUpdate: useUpdateAdminCategory,
        useDelete: useDeleteAdminCategory,

        invalidateList,
        keys: categoryKeys,
    };
}
