'use client';

import { useCallback } from 'react';
import { useIsFetching, useQueryClient } from '@tanstack/react-query';

import { licenseQueryKeys } from '@/api/manual/adminLicense';
import { billingQueryKeys } from '@/features/billing/constants/billingQueryKeys';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';

type UseLicensePageRefreshOptions = {
    tenantId?: string | null;
    showAllTenantLicensesSection?: boolean;
    canActivate?: boolean;
};

function isLicensePageQueryKey(queryKey: readonly unknown[]): boolean {
    const root = queryKey[0];
    if (root === 'tenant' && queryKey[1] === 'license') {
        return true;
    }
    if (root === 'admin' && queryKey[1] === 'tenant-license') {
        return true;
    }
    if (root === 'admin' && queryKey[1] === 'tenants' && queryKey[2] === 'license-overview') {
        return true;
    }
    if (root === 'admin' && queryKey[1] === 'license') {
        return true;
    }
    if (root === 'api' && queryKey[1] === 'admin' && queryKey[2] === 'tenants') {
        return true;
    }
    return false;
}

/** Manual refresh for `/admin/license` — invalidates and refetches all visible license data. */
export function useLicensePageRefresh(options: UseLicensePageRefreshOptions) {
    const queryClient = useQueryClient();
    const { tenantId, showAllTenantLicensesSection = false, canActivate = false } = options;

    const isFetching =
        useIsFetching({
            predicate: (query) => isLicensePageQueryKey(query.queryKey),
        }) > 0;

    const refresh = useCallback(async () => {
        await invalidateTenantLicenseQueries(queryClient, tenantId);
        if (showAllTenantLicensesSection) {
            await queryClient.invalidateQueries({ queryKey: billingQueryKeys.all, refetchType: 'all' });
        }
        if (canActivate) {
            await Promise.all([
                queryClient.invalidateQueries({
                    queryKey: licenseQueryKeys.listRoot,
                    refetchType: 'all',
                }),
                queryClient.invalidateQueries({
                    queryKey: licenseQueryKeys.activationAttemptsRoot,
                    refetchType: 'all',
                }),
                queryClient.invalidateQueries({ queryKey: ['admin', 'licenses'], refetchType: 'all' }),
            ]);
        }
    }, [queryClient, tenantId, showAllTenantLicensesSection, canActivate]);

    return { refresh, isFetching, refetch: refresh };
}
