'use client';

import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { getDevTenant, isDevelopment } from '@/features/auth/services/devTenant';
import { readTokenTenantClaims } from '@/features/auth/services/tokenTenantClaims';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
  isTenantSuspendedOrInactive,
  resolveActiveTenantFromSwitcherList,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import {
  resolveActiveTenantId,
  tenantSlugsMatch,
} from '@/features/tenancy/utils/resolveActiveTenantIdentity';

/** Minimal tenant fields from GET /api/tenants/current. */
export type TenantSnapshot = {
  id: string;
  slug: string;
  name: string;
  licenseValidUntilUtc: string | null;
};

/**
 * Prefer switcher / dev-header identity when GET /api/tenants/current still reflects a
 * mismatched JWT mandant (e.g. Super Admin `default` while header is `dev`).
 */
export function resolveTenantIdentityFromApiAndSwitcher(input: {
  apiTenant: TenantSnapshot | null | undefined;
  resolvedRow: Pick<
    AdminTenantListItem,
    'id' | 'slug' | 'name' | 'licenseValidUntilUtc' | 'licenseKey' | 'licenseDaysRemaining'
  > | null;
  ctxSlug: string | null | undefined;
  ctxName: string | null | undefined;
  jwtTenantId: string | null;
  jwtTenantSlug: string | null | undefined;
}): {
  tenantId: string | null;
  tenantSlug: string | null | undefined;
  tenantName: string | null;
  licenseValidUntilUtc: string | null;
  licenseKey: string | null;
  licenseDaysRemaining: number | null;
} {
  const switcherSlug = input.resolvedRow?.slug ?? input.ctxSlug;
  const switcherId = resolveActiveTenantId({
    resolvedRowId: input.resolvedRow?.id,
    jwtTenantId: input.jwtTenantId,
    jwtTenantSlug: input.jwtTenantSlug,
    activeTenantSlug: switcherSlug,
  });

  const apiMatchesSwitcher = Boolean(
    input.apiTenant &&
    ((switcherId != null && input.apiTenant.id === switcherId) ||
      (switcherSlug != null &&
        switcherSlug !== 'admin' &&
        tenantSlugsMatch(input.apiTenant.slug, switcherSlug)))
  );

  const preferSwitcherIdentity =
    Boolean(switcherId && switcherSlug && switcherSlug !== 'admin') && !apiMatchesSwitcher;

  const tenantSlug = preferSwitcherIdentity
    ? switcherSlug
    : (input.apiTenant?.slug ?? switcherSlug);
  const tenantId = preferSwitcherIdentity ? switcherId : (input.apiTenant?.id ?? switcherId);
  const tenantName = preferSwitcherIdentity
    ? (input.resolvedRow?.name ?? input.ctxName ?? null)
    : (input.apiTenant?.name ?? input.resolvedRow?.name ?? input.ctxName ?? null);

  const licenseValidUntilUtc =
    input.apiTenant && tenantId != null && input.apiTenant.id === tenantId
      ? (input.apiTenant.licenseValidUntilUtc ?? input.resolvedRow?.licenseValidUntilUtc ?? null)
      : (input.resolvedRow?.licenseValidUntilUtc ?? input.apiTenant?.licenseValidUntilUtc ?? null);

  return {
    tenantId,
    tenantSlug,
    tenantName,
    licenseValidUntilUtc,
    licenseKey: input.resolvedRow?.licenseKey ?? null,
    licenseDaysRemaining: input.resolvedRow?.licenseDaysRemaining ?? null,
  };
}

export type CurrentTenant = {
  tenantSlug: string | null | undefined;
  tenantId: string | null;
  tenantName: string | null;
  tenantStatus: string | null;
  isActive: boolean;
  isTenantSuspended: boolean;
  licenseValidUntilUtc: string | null;
  licenseKey: string | null;
  licenseDaysRemaining: number | null;
  resolvedTenant: AdminTenantListItem | null;
  displayLabel: string | null;
  hasAuthToken: boolean;
  isImpersonating: boolean;
  isDevTenantOverride: boolean;
  isPlatformAdminHost: boolean;
  hostSlug: string;
  requiresTenantSelection: boolean;
  isSuperAdminPlatformMode: boolean;
  isSuperAdminUser: boolean;
  isRealTenantSlug: boolean;
  showTenantLicenseInHeader: boolean;
  suppressLicenseWarnings: boolean;
  isTenantRecordLoading: boolean;
};

/** Resolves active mandant (header switcher / JWT / dev override / API current). Used by {@link TenantProvider}. */
export function useCurrentTenantState(
  apiTenant: TenantSnapshot | null = null,
  apiTenantLoading = false
): CurrentTenant {
  const { user } = useAuth();
  const ctx = useTenantContext();
  const mode = useSuperAdminTenantMode();

  const switcherQuery = useGetApiAdminTenants(
    { includeDeleted: false },
    {
      enabled: ctx.hasAuthToken,
      staleTime: 60_000,
      refetchOnMount: true,
      refetchOnWindowFocus: true,
    }
  );

  return useMemo(() => {
    const tokenSnapshot = readTokenTenantClaims();
    const jwtTenantId = user?.tenantId ?? tokenSnapshot.tenantId ?? ctx.tenantId ?? null;
    const jwtTenantSlug = user?.tenantSlug ?? tokenSnapshot.tenantSlug ?? ctx.jwtTenantSlug ?? null;
    const rawDevSlug = isDevelopment() ? (ctx.devSelectedSlug ?? getDevTenant()) : null;
    const devTenantSlug = rawDevSlug && rawDevSlug !== 'admin' ? rawDevSlug : null;

    const resolvedRow = resolveActiveTenantFromSwitcherList(switcherQuery.data ?? [], {
      jwtTenantId,
      jwtTenantSlug,
      isImpersonating: ctx.isImpersonating,
      isDevTenantOverride: ctx.isDevTenantOverride,
      devTenantSlug,
      hostSlug: ctx.hostSlug,
    });

    const {
      tenantId,
      tenantSlug,
      tenantName,
      licenseValidUntilUtc,
      licenseKey,
      licenseDaysRemaining,
    } = resolveTenantIdentityFromApiAndSwitcher({
      apiTenant,
      resolvedRow,
      ctxSlug: ctx.tenantSlug,
      ctxName: ctx.tenantName,
      jwtTenantId,
      jwtTenantSlug,
    });
    const tenantStatus = resolvedRow?.status ?? null;
    const isActive = resolvedRow?.isActive ?? true;
    const isTenantSuspended = resolvedRow ? isTenantSuspendedOrInactive(resolvedRow) : false;

    const isSuperAdminUser = isSuperAdmin(user?.role);
    const isRealTenantSlug = Boolean(tenantSlug && tenantSlug !== 'admin');
    const isManager = user?.role === 'Manager';

    const showTenantLicenseInHeader =
      ctx.hasAuthToken && !isSuperAdminUser && isManager && isRealTenantSlug;

    const suppressLicenseWarnings = isSuperAdminUser;

    const awaitingTenantId = Boolean(tenantSlug && tenantSlug !== 'admin' && !tenantId);

    const isTenantRecordLoading =
      apiTenantLoading ||
      (ctx.hasAuthToken &&
        awaitingTenantId &&
        (switcherQuery.isLoading || switcherQuery.isFetching));

    return {
      tenantSlug,
      tenantId,
      tenantName,
      tenantStatus,
      isActive,
      isTenantSuspended,
      licenseValidUntilUtc,
      licenseKey,
      licenseDaysRemaining,
      resolvedTenant: resolvedRow,
      displayLabel: tenantName ?? (tenantSlug && tenantSlug !== 'admin' ? tenantSlug : null),
      hasAuthToken: ctx.hasAuthToken,
      isImpersonating: ctx.isImpersonating,
      isDevTenantOverride: ctx.isDevTenantOverride,
      isPlatformAdminHost: ctx.isPlatformAdminHost,
      hostSlug: ctx.hostSlug,
      requiresTenantSelection: mode.requiresTenantSelection,
      isSuperAdminPlatformMode: mode.isSuperAdminPlatformMode,
      isSuperAdminUser,
      isRealTenantSlug,
      showTenantLicenseInHeader,
      suppressLicenseWarnings,
      isTenantRecordLoading,
    };
  }, [
    user?.role,
    user?.tenantId,
    user?.tenantSlug,
    ctx.tenantSlug,
    ctx.tenantId,
    ctx.tenantName,
    ctx.jwtTenantSlug,
    ctx.hasAuthToken,
    ctx.isImpersonating,
    ctx.isDevTenantOverride,
    ctx.isPlatformAdminHost,
    ctx.hostSlug,
    mode.requiresTenantSelection,
    mode.isSuperAdminPlatformMode,
    switcherQuery.data,
    switcherQuery.isLoading,
    switcherQuery.fetchStatus,
    apiTenant?.id,
    apiTenant?.slug,
    apiTenant?.name,
    apiTenant?.licenseValidUntilUtc,
    apiTenantLoading,
  ]);
}
