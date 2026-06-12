'use client';

/**
 * Product list, CRUD, stock: all calls use /api/admin/products (generated product hooks are not used).
 * Single list query supports pagination, optional name/categoryId, and isActive (all/true/false).
 */
import { useMemo } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { AdminProductsListParams } from '@/api/admin/products';
import {
    useAdminProductsList,
    useAdminProductById,
    useCreateAdminProduct,
    useUpdateAdminProduct,
    useDeleteAdminProduct,
    useBulkDeactivateAdminProducts,
    useDeactivateAllAdminProducts,
    useDevPurgeAdminCatalog,
    useUpdateAdminProductStock,
    useSetAdminProductModifierGroups,
    adminProductsQueryKeys,
} from '@/api/admin/products';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useURLFilters } from '@/hooks/useURLFilters';

export function createProductKeys(tenantSlug: string) {
    return {
        all: adminProductsQueryKeys.all(tenantSlug),
        lists: () => adminProductsQueryKeys.lists(tenantSlug),
        list: (params?: AdminProductsListParams) => adminProductsQueryKeys.list(tenantSlug, params),
        details: () => adminProductsQueryKeys.details(tenantSlug),
        detail: (id: string) => adminProductsQueryKeys.detail(tenantSlug, id),
    };
}

export function useProductFilters() {
    return useURLFilters<{
        page: string;
        pageSize: string;
        search: string;
        categoryId: string;
        /** List scope: active (default), inactive, all — mirrors UI Segmented */
        status: string;
    }>();
}

export function useProducts() {
    const queryClient = useQueryClient();
    const { tenantSlug } = useCurrentTenant();
    const keys = useMemo(() => createProductKeys(tenantSlug), [tenantSlug]);

    const invalidateList = () => {
        queryClient.invalidateQueries({ queryKey: adminProductsQueryKeys.lists(tenantSlug) });
    };

    return {
        tenantSlug,
        /** Single list query with optional pagination and filters (name, categoryId, isActive API param). */
        useList: (params?: AdminProductsListParams, options?: Parameters<typeof useAdminProductsList>[1]) =>
            useAdminProductsList(params, options),

        useDetail: (id: string, options?: Parameters<typeof useAdminProductById>[1]) =>
            useAdminProductById(id, options),

        useCreate: useCreateAdminProduct,
        useUpdate: useUpdateAdminProduct,
        useDelete: useDeleteAdminProduct,
        useBulkDeactivate: useBulkDeactivateAdminProducts,
        useDeactivateAll: useDeactivateAllAdminProducts,
        useDevPurgeCatalog: useDevPurgeAdminCatalog,
        useUpdateStock: useUpdateAdminProductStock,
        useSetModifierGroups: useSetAdminProductModifierGroups,

        invalidateList,
        keys,
    };
}
