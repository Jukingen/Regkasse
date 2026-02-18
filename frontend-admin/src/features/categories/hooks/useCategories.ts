import { useQueryClient } from '@tanstack/react-query';
import {
    useGetApiCategories,
    usePostApiCategories,
    usePutApiCategoriesId,
    useDeleteApiCategoriesId,
    useGetApiCategoriesIdProducts
} from '@/api/generated/categories/categories';
import { CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';

// Key Factory for consistent cache management
export const categoryKeys = {
    all: ['categories'] as const,
    lists: () => [...categoryKeys.all, 'list'] as const,
    details: () => [...categoryKeys.all, 'detail'] as const,
    detail: (id: string) => [...categoryKeys.details(), id] as const,
    products: (id: string) => [...categoryKeys.detail(id), 'products'] as const,
};

export function useCategories() {
    const queryClient = useQueryClient();

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: categoryKeys.lists() });
        // Invalidate orval keys as well if they differ
        queryClient.invalidateQueries({ queryKey: ['/api/Categories'] });
    };

    return {
        // Queries
        useList: () =>
            useGetApiCategories({
                query: {
                    queryKey: categoryKeys.lists(),
                }
            }),
        useProductsByCategory: (id: string) =>
            useGetApiCategoriesIdProducts(id, {
                query: {
                    queryKey: categoryKeys.products(id),
                    enabled: !!id
                }
            }),

        // Mutations
        useCreate: usePostApiCategories,
        useUpdate: usePutApiCategoriesId,
        useDelete: useDeleteApiCategoriesId,

        // Utils
        invalidateList,
        keys: categoryKeys,
    };
}
