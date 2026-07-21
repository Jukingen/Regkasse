'use client';

import { useMemo } from 'react';

import {
  TENANT_GRACE_PERIOD_DAYS,
  clampTenantGraceRemaining,
} from '@/features/license/constants/licenseGracePeriod';
import type { LicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import {
  getLicenseStatusMessage,
  resolveTenantLicenseFromPublicStatus,
} from '@/features/license/utils/licenseStatus';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenantLicense } from '@/hooks/useTenantLicense';
import { useI18n } from '@/i18n';

export type HeaderLicenseMode = 'hidden' | 'tenant';

/**
 * Mandant SaaS license for header badge (Manager on tenant context only).
 * Data source: {@link useTenantLicense} — never stale tenant-switcher row fallback.
 */
export function useHeaderTenantLicense() {
  const ctx = useCurrentTenant();
  const { t } = useI18n();
  const licenseQuery = useTenantLicense();

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

  const licenseValidUntilUtc = mode === 'tenant' ? (licenseQuery.data?.validUntil ?? null) : null;

  const isLoading = mode === 'tenant' && licenseQuery.isLoading;

  const resolvedStatus: LicenseStatus | null | undefined = useMemo(() => {
    if (mode !== 'tenant' || !ctx.isRealTenantSlug || licenseQuery.isLoading) {
      return null;
    }

    if (!licenseQuery.data) {
      return null;
    }

    const resolved = resolveTenantLicenseFromPublicStatus(licenseQuery.data);
    const status: LicenseStatus = {
      ...resolved,
      daysRemainingInGrace:
        resolved.kind === 'grace_write'
          ? clampTenantGraceRemaining(TENANT_GRACE_PERIOD_DAYS - resolved.daysExpired)
          : 0,
      isExpired:
        resolved.kind === 'grace_write' ||
        resolved.kind === 'grace_readonly' ||
        resolved.kind === 'lockdown' ||
        resolved.kind === 'expired',
      isLocked: resolved.kind === 'lockdown' || resolved.kind === 'expired',
      lockDate: licenseQuery.data.lockDate ?? null,
      message: getLicenseStatusMessage(resolved, 'tenant', t),
    };
    return status;
  }, [mode, ctx.isRealTenantSlug, licenseQuery.isLoading, licenseQuery.data, t]);

  const isUnavailable = useMemo(() => {
    if (mode !== 'tenant' || licenseQuery.isLoading) {
      return false;
    }
    if (resolvedStatus) {
      return false;
    }
    return licenseQuery.isError || !licenseQuery.isAuthorized;
  }, [
    mode,
    licenseQuery.isLoading,
    licenseQuery.isError,
    licenseQuery.isAuthorized,
    resolvedStatus,
  ]);

  return {
    mode,
    resolvedStatus,
    licenseValidUntilUtc,
    isLoading,
    isUnavailable,
    licenseQuery,
  };
}
