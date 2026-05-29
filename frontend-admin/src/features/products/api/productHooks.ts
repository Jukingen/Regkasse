'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
    getDemoImportCatalog,
    importDemoProducts,
    type DemoImportCatalog,
    type DemoProductImportResult,
} from '@/api/admin/products';
import { importDemoProductsForTenant } from '@/features/super-admin/api/adminTenants';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

export type ImportDemoProductsVariables = {
    /** Super-admin: target tenant. Omit to import into current tenant context. */
    tenantId?: string;
    selectedCategories: string[];
    overwriteExisting: boolean;
};

export const demoImportCatalogQueryKey = ['admin', 'demo-import-catalog'] as const;

export function useDemoImportCatalog(enabled = true) {
    return useQuery<DemoImportCatalog>({
        queryKey: demoImportCatalogQueryKey,
        queryFn: ({ signal }) => getDemoImportCatalog(undefined, signal),
        enabled,
        staleTime: 5 * 60_000,
    });
}

export function useImportDemoProducts() {
    const queryClient = useQueryClient();
    const { tenantId: currentTenantId } = useCurrentTenant();

    return useMutation<DemoProductImportResult, Error, ImportDemoProductsVariables>({
        mutationFn: ({ tenantId, selectedCategories, overwriteExisting }) => {
            const request = { selectedCategories, overwriteExisting };
            if (tenantId) {
                return importDemoProductsForTenant(tenantId, request);
            }
            return importDemoProducts(request);
        },
        onSuccess: (_result, { tenantId }) => {
            const effectiveTenantId = tenantId ?? currentTenantId;
            if (!effectiveTenantId) return;

            queryClient.invalidateQueries({ queryKey: ['products', effectiveTenantId] });
            queryClient.invalidateQueries({ queryKey: ['categories', effectiveTenantId] });
        },
    });
}
