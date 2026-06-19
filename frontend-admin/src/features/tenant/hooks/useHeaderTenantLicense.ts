'use client';

import { useMemo } from 'react';

import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import {
    resolveTenantLicenseLabel,
    type TenantLicenseLabel,
} from '@/features/super-admin/utils/tenantLicenseLabel';

export type HeaderLicenseMode = 'hidden' | 'tenant';

function resolveHeaderTenantLicenseLabel(
    licenseValidUntilUtc: string | null | undefined,
    licenseKey: string | null | undefined,
    licenseDaysRemaining: number | null | undefined,
    now = Date.now(),
): TenantLicenseLabel {
    if (!licenseValidUntilUtc?.trim()) {
        return { kind: 'none', label: '—', daysRemaining: null };
    }

    return resolveTenantLicenseLabel(
        licenseValidUntilUtc,
        licenseKey,
        now,
        licenseDaysRemaining,
    );
}

/**
 * Mandant SaaS license for header badge (Manager on tenant context only).
 * Data source: `useCurrentTenant` → `GET /api/tenants/switcher` resolved row — not deployment license.
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

    const license: TenantLicenseLabel | null = useMemo(() => {
        if (mode !== 'tenant' || !ctx.isRealTenantSlug) {
            return null;
        }
        if (ctx.isTenantRecordLoading) {
            return null;
        }
        return resolveHeaderTenantLicenseLabel(
            ctx.licenseValidUntilUtc,
            ctx.licenseKey,
            ctx.licenseDaysRemaining,
        );
    }, [
        mode,
        ctx.isRealTenantSlug,
        ctx.isTenantRecordLoading,
        ctx.licenseValidUntilUtc,
        ctx.licenseKey,
        ctx.licenseDaysRemaining,
    ]);

    const licenseValidUntilUtc = mode === 'tenant' ? ctx.licenseValidUntilUtc : null;

    return {
        mode,
        license,
        licenseValidUntilUtc,
        isLoading: mode === 'tenant' && ctx.isTenantRecordLoading,
    };
}
