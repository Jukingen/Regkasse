'use client';

import { useMemo } from 'react';

import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    resolveTenantLicenseLabel,
    type TenantLicenseLabel,
} from '@/features/super-admin/utils/tenantLicenseLabel';

export type HeaderLicenseMode = 'hidden' | 'tenant';

/**
 * Mandant SaaS license for header/banner (Manager on tenant context only).
 * Super Admin platform mode is shown on TenantBadge only — not here.
 */
export function useHeaderTenantLicense() {
    const ctx = useCurrentTenant();

    const mode: HeaderLicenseMode = useMemo(() => {
        if (
            !ctx.hasAuthToken ||
            ctx.isSuperAdminPlatformMode ||
            ctx.suppressLicenseWarnings ||
            !ctx.showTenantLicenseInHeader
        ) {
            return 'hidden';
        }
        return 'tenant';
    }, [
        ctx.hasAuthToken,
        ctx.isSuperAdminPlatformMode,
        ctx.showTenantLicenseInHeader,
        ctx.suppressLicenseWarnings,
    ]);

    const tenantsQuery = useGetApiAdminTenants(
        { includeDeleted: false },
        { enabled: mode === 'tenant', staleTime: 60_000 },
    );

    const license: TenantLicenseLabel | null = useMemo(() => {
        if (mode !== 'tenant' || !ctx.tenantSlug) {
            return null;
        }
        const row = tenantsQuery.data?.find(
            (t) => t.slug.toLowerCase() === ctx.tenantSlug!.toLowerCase(),
        );
        if (!row) {
            return null;
        }
        return resolveTenantLicenseLabel(row.licenseValidUntilUtc, row.licenseKey);
    }, [mode, ctx.tenantSlug, tenantsQuery.data]);

    const licenseValidUntilUtc = useMemo(() => {
        if (mode !== 'tenant' || !ctx.tenantSlug) {
            return null;
        }
        return (
            tenantsQuery.data?.find((t) => t.slug.toLowerCase() === ctx.tenantSlug!.toLowerCase())
                ?.licenseValidUntilUtc ?? null
        );
    }, [mode, ctx.tenantSlug, tenantsQuery.data]);

    return {
        mode,
        license,
        licenseValidUntilUtc,
        isLoading: mode === 'tenant' && tenantsQuery.isLoading && !tenantsQuery.data,
    };
}
