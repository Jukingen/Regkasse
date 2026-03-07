/**
 * Category list, CRUD, and products-by-category: all calls use /api/admin/categories.
 * List and search are separate endpoints; page uses list when search is empty, search when term is present.
 */
import { useQueryClient } from '@tanstack/react-query';
import {
    useAdminCategoriesList,
    useAdminCategoriesSearch,
    useAdminCategoryById,
    useAdminCategoryProducts,
    useCreateAdminCategory,
    useUpdateAdminCategory,
    useDeleteAdminCategory,
    adminCategoriesQueryKeys,
} from '@/api/admin/categories';

export const categoryKeys = {
    all: adminCategoriesQueryKeys.all,
    lists: adminCategoriesQueryKeys.lists,
    details: adminCategoriesQueryKeys.details,
    detail: (id: string) => adminCategoriesQueryKeys.detail(id),
    products: (id: string) => adminCategoriesQueryKeys.products(id),
    search: (query: string) => adminCategoriesQueryKeys.search(query),
};

export function useCategories() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: adminCategoriesQueryKeys.all });
    };

    return {
        useList: (options?: Parameters<typeof useAdminCategoriesList>[0]) =>
            useAdminCategoriesList(options),

        useSearch: (query: string, options?: Parameters<typeof useAdminCategoriesSearch>[1]) =>
            useAdminCategoriesSearch(query, { enabled: !!query.trim(), ...options }),

        useDetail: (id: string, options?: Parameters<typeof useAdminCategoryById>[1]) =>
            useAdminCategoryById(id, options),

        useProductsByCategory: (id: string, options?: Parameters<typeof useAdminCategoryProducts>[1]) =>
            useAdminCategoryProducts(id, { enabled: !!id, ...options }),

        useCreate: useCreateAdminCategory,
        useUpdate: useUpdateAdminCategory,
        useDelete: useDeleteAdminCategory,

        invalidateList,
        keys: categoryKeys,
    };
}
