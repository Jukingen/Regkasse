'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import { useGetApiCompanySettings } from '@/api/generated/company-settings/company-settings';
import { getLicenseStatus, licenseQueryKeys } from '@/api/manual/adminLicense';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';

export type TenantLicenseDisplay = 'active' | 'expired' | 'days' | 'unknown';

/**
 * Mandant info for Verwaltung card: context slug/id, company or admin API metadata, license status.
 */
export function useTenantInfo() {
    const { user } = useAuth();
    const ctx = useCurrentTenant();
    const canFetchAdminTenant = isSuperAdmin(user?.role) && Boolean(ctx.tenantId);

    const licenseQuery = useQuery({
        queryKey: licenseQueryKeys.status,
        queryFn: getLicenseStatus,
        enabled: ctx.hasAuthToken,
        retry: false,
        staleTime: 60_000,
    });

    const companyQuery = useGetApiCompanySettings({
        query: { enabled: ctx.hasAuthToken, retry: false },
    });

    const adminTenantQuery = useQuery({
        queryKey: ['admin', 'tenant', ctx.tenantId] as const,
        queryFn: () => getAdminTenantById(ctx.tenantId!),
        enabled: ctx.hasAuthToken && canFetchAdminTenant,
        retry: false,
        staleTime: 60_000,
    });

    return useMemo(() => {
        const adminTenant = adminTenantQuery.data;
        const companyTenant = companyQuery.data?.tenant;
        const tenantId =
            ctx.tenantId ?? companyTenant?.id ?? companyQuery.data?.tenantId ?? null;

        const name =
            ctx.tenantName?.trim() ||
            adminTenant?.name?.trim() ||
            companyTenant?.name?.trim() ||
            null;

        const registeredAt = adminTenant?.createdAt ?? companyTenant?.createdAt ?? null;

        const licenseFromStatus = licenseQuery.data;
        const licenseValidUntilUtc = adminTenant?.licenseValidUntilUtc ?? null;

        let licenseDisplay: TenantLicenseDisplay = 'unknown';
        let daysRemaining: number | null = null;

        if (licenseFromStatus) {
            if (licenseFromStatus.isExpired) {
                licenseDisplay = 'expired';
            } else if (licenseFromStatus.daysRemaining > 0) {
                licenseDisplay = 'days';
                daysRemaining = licenseFromStatus.daysRemaining;
            } else if (licenseFromStatus.isValid) {
                licenseDisplay = 'active';
            }
        } else if (licenseValidUntilUtc) {
            const until = new Date(licenseValidUntilUtc);
            const diffMs = until.getTime() - Date.now();
            const diffDays = Math.ceil(diffMs / (24 * 60 * 60 * 1000));
            if (diffDays < 0) {
                licenseDisplay = 'expired';
            } else if (diffDays > 0) {
                licenseDisplay = 'days';
                daysRemaining = diffDays;
            } else {
                licenseDisplay = 'active';
            }
        }

        return {
            tenantSlug: companyTenant?.slug ?? ctx.tenantSlug,
            tenantId,
            tenantName: name,
            registeredAt,
            licenseDisplay,
            daysRemaining,
            hasAuthToken: ctx.hasAuthToken,
            isLoading:
                (licenseQuery.isLoading && licenseQuery.fetchStatus !== 'idle') ||
                (companyQuery.isLoading && companyQuery.fetchStatus !== 'idle') ||
                (adminTenantQuery.isLoading && adminTenantQuery.fetchStatus !== 'idle'),
        };
    }, [
        ctx.hasAuthToken,
        ctx.tenantName,
        ctx.tenantSlug,
        adminTenantQuery.data,
        adminTenantQuery.fetchStatus,
        adminTenantQuery.isLoading,
        companyQuery.data,
        companyQuery.fetchStatus,
        companyQuery.isLoading,
        licenseQuery.data,
        licenseQuery.fetchStatus,
        licenseQuery.isLoading,
    ]);
}
