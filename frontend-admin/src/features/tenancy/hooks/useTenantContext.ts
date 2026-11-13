'use client';

import { useMemo, useSyncExternalStore } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { authStorage } from '@/features/auth/services/authStorage';
import {
  DEV_TENANT_LOCAL_STORAGE_KEY,
  getDevTenant,
  getEffectiveTenantSlug,
  getTenantSlugFromSubdomain,
  isDevelopment,
} from '@/features/auth/services/devTenant';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { readTokenTenantClaims } from '@/features/auth/services/tokenTenantClaims';

const emptySubscribe = () => () => {};

/** Stable primitive for `useSyncExternalStore` — object snapshots would re-render forever. */
function readTokenTenantClaimsSnapshotKey(): string {
  const { tenantId, tenantSlug, isImpersonating } = readTokenTenantClaims();
  return `${tenantId ?? ''}|${tenantSlug ?? ''}|${isImpersonating ? '1' : '0'}`;
}

function readDevTenantOverrideActive(): boolean {
  if (!isDevelopment() || typeof window === 'undefined') return false;
  const stored = window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)?.trim();
  if (!stored) return false;
  const hostSlug = getTenantSlugFromSubdomain();
  if (hostSlug !== 'admin' && stored === hostSlug) return false;
  return true;
}

/**
 * Read-only resolved tenant context (JWT, host subdomain, dev `dev_tenant_id`, `tenantStorage`).
 *
 * Used for header badge, API `X-Tenant-Id` (via {@link getEffectiveTenantSlug}), and mandant-scoped pages.
 * Does **not** own a `switchTenant` mutator — dev switching is {@link DEV_TENANT_CHANGED_EVENT} /
 * `HeaderDevTenantSwitch` → `localStorage` `dev_tenant_id` (slug).
 *
 * **User creation** uses a separate mandant source: `CreateUserModal` form `tenantId`, optional
 * `fixedTenantId` from the users list URL filter (`?tenantId=`), or tenant-detail `useCreateUser({ fixedTenantId })`.
 * Do not wire `useTenantContext().tenantId` into create-user payloads.
 */
export function useTenantContext() {
  const { user } = useAuth();

  const tokenClaimsKey = useSyncExternalStore(
    emptySubscribe,
    readTokenTenantClaimsSnapshotKey,
    () => '|0'
  );

  const hostSlug = useSyncExternalStore(
    emptySubscribe,
    () => (typeof window !== 'undefined' ? getTenantSlugFromSubdomain() : 'admin'),
    () => 'admin'
  );

  const effectiveSlug = useSyncExternalStore(
    emptySubscribe,
    () => getEffectiveTenantSlug(),
    () => 'dev'
  );

  return useMemo(() => {
    const tokenSnapshot = readTokenTenantClaims();
    const jwtTenantSlug = user?.tenantSlug ?? tokenSnapshot.tenantSlug;
    const tenantId = user?.tenantId ?? tokenSnapshot.tenantId ?? tenantStorage.getTenantId();
    const tenantName = user?.tenantDisplayName?.trim() || null;
    const isPlatformAdminHost = hostSlug === 'admin' && effectiveSlug === 'admin';
    const isDevTenantOverride = readDevTenantOverrideActive();
    const devSelectedSlug = isDevelopment() ? getDevTenant() : null;

    const displayLabel =
      tenantName ||
      (effectiveSlug !== 'admin' ? effectiveSlug : null) ||
      (isPlatformAdminHost ? null : effectiveSlug);

    return {
      tenantSlug: effectiveSlug,
      tenantName,
      tenantId,
      jwtTenantSlug,
      displayLabel,
      hostSlug,
      isPlatformAdminHost,
      isImpersonating: tokenSnapshot.isImpersonating,
      isDevTenantOverride,
      devSelectedSlug,
      hasAuthToken: authStorage.hasToken(),
    };
  }, [user, tokenClaimsKey, hostSlug, effectiveSlug]);
}
