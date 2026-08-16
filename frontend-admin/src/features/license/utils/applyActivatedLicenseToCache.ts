import type { QueryClient, QueryKey } from '@tanstack/react-query';

import {
  type LicensePublicStatusDto,
  tenantLicenseUnifiedQueryKey,
} from '@/api/manual/adminLicense';
import type { TenantLicenseOverview } from '@/features/license/api/tenantLicense';
import { tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import type { CurrentTenantDto } from '@/features/tenancy/api/getCurrentTenant';
import { currentTenantQueryKey } from '@/features/tenancy/api/getCurrentTenant';
import { calculateLicenseDaysRemainingUnsigned } from '@/features/license/utils/licenseValidUntil';
import type { OptimisticQuerySnapshot } from '@/lib/query/optimisticUpdateHelpers';

export type ActivatedLicenseCachePatch = {
  tenantId?: string | null;
  validUntilUtc?: string | null;
  licenseKey?: string | null;
  licenseType?: string | null;
};

function trimOrNull(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function daysRemainingFromValidUntil(validUntilUtc: string | null, nowMs: number): number {
  return calculateLicenseDaysRemainingUnsigned(validUntilUtc, nowMs);
}

function shouldPatchUnifiedKey(queryKey: QueryKey, tenantId: string | null): boolean {
  if (queryKey[0] !== 'tenant' || queryKey[1] !== 'license') {
    return false;
  }
  const keyTenant = queryKey[2];
  if (!tenantId) {
    return true;
  }
  return keyTenant === tenantId || keyTenant === 'current';
}

/** Active public-status snapshot so FA immediately drops lockdown / stale expiry. */
export function toActiveLicensePublicStatus(
  old: LicensePublicStatusDto | undefined,
  patch: ActivatedLicenseCachePatch,
  nowMs = Date.now()
): LicensePublicStatusDto {
  const validUntil = trimOrNull(patch.validUntilUtc) ?? old?.validUntil ?? null;
  return {
    licenseType: trimOrNull(patch.licenseType) || old?.licenseType || 'Licensed',
    validUntil,
    daysRemaining: daysRemainingFromValidUntil(validUntil, nowMs),
    features: old?.features ?? [],
    isExpired: false,
    isValid: true,
    mode: old?.mode ?? 'Production',
    isDevelopmentBypass: old?.isDevelopmentBypass,
    canAccess: true,
    canTransact: true,
    statusMessage: old?.statusMessage ?? null,
    statusMessageKey: 'license.status.active',
    isInGracePeriod: false,
    isLocked: false,
    daysOverdue: 0,
    gracePeriodRemaining: 0,
    lockDate: null,
    restrictions: [],
    requiresRenewal: false,
  };
}

function toActiveOverview(
  old: TenantLicenseOverview | undefined,
  patch: ActivatedLicenseCachePatch,
  nowMs: number
): TenantLicenseOverview {
  const validUntilUtc = trimOrNull(patch.validUntilUtc) ?? old?.status.validUntilUtc ?? null;
  return {
    status: {
      kind: 'active',
      licenseKey: trimOrNull(patch.licenseKey) ?? old?.status.licenseKey ?? null,
      validUntilUtc,
      daysRemaining: daysRemainingFromValidUntil(validUntilUtc, nowMs),
      tier: old?.status.tier ?? null,
      features: old?.status.features ?? [],
      trialStatus: old?.status.trialStatus,
      trialEndsAtUtc: old?.status.trialEndsAtUtc,
      trialDaysRemaining: old?.status.trialDaysRemaining,
      trialGracePeriodEndsAtUtc: old?.status.trialGracePeriodEndsAtUtc,
    },
    history: old?.history ?? [],
  };
}

/**
 * Writes an active mandant license into React Query caches used by the dashboard,
 * header badge, expiry banner, and tenant license page.
 */
export function applyActivatedLicenseToCache(
  queryClient: QueryClient,
  patch: ActivatedLicenseCachePatch,
  nowMs = Date.now()
): void {
  const tenantId = trimOrNull(patch.tenantId);

  const unifiedEntries = queryClient.getQueriesData<LicensePublicStatusDto>({
    queryKey: tenantLicenseUnifiedQueryKey,
  });
  for (const [queryKey, data] of unifiedEntries) {
    if (!shouldPatchUnifiedKey(queryKey, tenantId)) {
      continue;
    }
    queryClient.setQueryData(queryKey, toActiveLicensePublicStatus(data, patch, nowMs));
  }

  if (tenantId) {
    for (const source of ['admin', 'public', 'auto'] as const) {
      const key = [...tenantLicenseUnifiedQueryKey, tenantId, source] as const;
      const existing = queryClient.getQueryData<LicensePublicStatusDto>(key);
      queryClient.setQueryData(key, toActiveLicensePublicStatus(existing, patch, nowMs));
    }

    const detailKey = tenantLicenseQueryKeys.detail(tenantId);
    const overview = queryClient.getQueryData<TenantLicenseOverview>(detailKey);
    queryClient.setQueryData(detailKey, toActiveOverview(overview, patch, nowMs));
  }

  const current = queryClient.getQueryData<CurrentTenantDto>(currentTenantQueryKey);
  if (current && (!tenantId || current.id === tenantId)) {
    queryClient.setQueryData<CurrentTenantDto>(currentTenantQueryKey, {
      ...current,
      licenseValid: true,
      licenseValidUntilUtc: trimOrNull(patch.validUntilUtc) ?? current.licenseValidUntilUtc,
    });
  }
}

const OPTIMISTIC_LICENSE_KEYS: QueryKey[] = [
  tenantLicenseUnifiedQueryKey,
  tenantLicenseQueryKeys.root,
  currentTenantQueryKey,
];

/** Cancel in-flight reads, snapshot, then show the new expiry immediately. */
export async function beginActivatedLicenseOptimisticUpdate(
  queryClient: QueryClient,
  patch: ActivatedLicenseCachePatch
): Promise<OptimisticQuerySnapshot> {
  const keys: QueryKey[] = [...OPTIMISTIC_LICENSE_KEYS];
  const tenantId = trimOrNull(patch.tenantId);
  if (tenantId) {
    keys.push(tenantLicenseQueryKeys.detail(tenantId));
  }

  const previous: OptimisticQuerySnapshot['previous'] = [];
  for (const queryKey of keys) {
    await queryClient.cancelQueries({ queryKey });
    previous.push(...queryClient.getQueriesData({ queryKey }));
  }

  applyActivatedLicenseToCache(queryClient, patch);

  const snapSet = new Set(previous.map(([key]) => JSON.stringify(key)));
  for (const queryKey of keys) {
    for (const [key] of queryClient.getQueriesData({ queryKey })) {
      if (!snapSet.has(JSON.stringify(key))) {
        previous.push([key, undefined]);
      }
    }
  }

  return { previous };
}

/** Restore snapshots; drop cache entries that the optimistic write created. */
export function rollbackActivatedLicenseOptimisticUpdate(
  queryClient: QueryClient,
  context: OptimisticQuerySnapshot | undefined
): void {
  if (!context?.previous.length) return;
  for (const [key, data] of context.previous) {
    if (data === undefined) {
      queryClient.removeQueries({ queryKey: key, exact: true });
    } else {
      queryClient.setQueryData(key, data);
    }
  }
}
