'use client';

import type { QueryClient } from '@tanstack/react-query';
import { type ReactNode, createContext, useContext, useMemo, useSyncExternalStore } from 'react';

import { ambientVerticalProfileQueryKeys } from '@/api/admin/vertical-profile';
import {
  getGetApiAdminVerticalProfileQueryKey,
  useGetApiAdminVerticalProfile,
} from '@/api/generated/admin/admin';
import type { EffectiveVerticalProfileDto } from '@/api/generated/model';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import {
  type AdminVerticalProfileSnapshot,
  readSuperAdminProfileSimulation,
  resolveEffectiveAdminVerticalProfile,
  subscribeSuperAdminProfileSimulation,
} from '@/features/vertical-profiles/superAdminProfileSimulation';

/** Matches backend tenant-settings cache TTL (`CacheSettings:TenantSettingsCacheMinutes`). */
export const ADMIN_VERTICAL_PROFILE_STALE_MS = 5 * 60 * 1000;

/**
 * Sidebar and route guard read `GET /api/admin/vertical-profile` with a tenant
 * scope suffix. Invalidate the prefix so a profile save refreshes that cache
 * before the five-minute stale time.
 */
export function invalidateAdminVerticalProfileQueries(
  queryClient: QueryClient,
  tenantId?: string
): void {
  void queryClient.invalidateQueries({
    queryKey: getGetApiAdminVerticalProfileQueryKey(),
  });
  void queryClient.invalidateQueries({
    queryKey: ambientVerticalProfileQueryKeys.all,
  });
  if (tenantId) {
    void queryClient.invalidateQueries({
      queryKey: ['admin', 'tenants', tenantId, 'vertical-profile'],
    });
  }
}

export type AdminVerticalProfileValue = AdminVerticalProfileSnapshot;

const AdminVerticalProfileContext = createContext<AdminVerticalProfileValue | null>(null);

function tenantScopeKey(
  tenantId: string | null | undefined,
  tenantSlug: string | null | undefined
): string {
  return `${tenantId?.trim() ?? ''}|${tenantSlug?.trim() ?? ''}`;
}

function hasAmbientTenant(
  tenantId: string | null | undefined,
  tenantSlug: string | null | undefined
): boolean {
  if (tenantId?.trim()) {
    return true;
  }
  const slug = tenantSlug?.trim();
  return Boolean(slug && slug !== 'admin');
}

export function normalizeAdminPosFeatures(value: unknown): Record<string, boolean> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(value as Record<string, unknown>).filter(
      (entry): entry is [string, boolean] => typeof entry[1] === 'boolean'
    )
  );
}

export function toAdminVerticalProfile(
  data: EffectiveVerticalProfileDto | undefined,
  isLoading: boolean
): Omit<AdminVerticalProfileSnapshot, 'viewAllProfiles'> {
  const profileId = data?.profileId?.trim() ?? '';
  const posLayout = data?.posLayout?.trim() || 'standard';

  return {
    profileId,
    posFeatures: normalizeAdminPosFeatures(data?.posFeatures),
    posLayout,
    isLoading,
  };
}

export function AdminVerticalProfileProvider({ children }: { children: ReactNode }) {
  const { tenantId, tenantSlug, isImpersonating } = useTenantContext();
  const { user } = useAuth();
  const simulation = useSyncExternalStore(
    subscribeSuperAdminProfileSimulation,
    readSuperAdminProfileSimulation,
    () => 'all'
  );
  const scope = tenantScopeKey(tenantId, tenantSlug);
  const enabled = hasAmbientTenant(tenantId, tenantSlug);

  const query = useGetApiAdminVerticalProfile({
    query: {
      queryKey: [...getGetApiAdminVerticalProfileQueryKey(), scope],
      staleTime: ADMIN_VERTICAL_PROFILE_STALE_MS,
      enabled,
    },
  });

  const value = useMemo(
    () =>
      resolveEffectiveAdminVerticalProfile({
        role: user?.role,
        isImpersonating,
        simulation,
        actual: toAdminVerticalProfile(query.data, !enabled || query.isLoading),
      }),
    [enabled, isImpersonating, query.data, query.isLoading, simulation, user?.role]
  );

  return (
    <AdminVerticalProfileContext.Provider value={value}>
      {children}
    </AdminVerticalProfileContext.Provider>
  );
}

export function useAdminVerticalProfile(): AdminVerticalProfileValue {
  const value = useContext(AdminVerticalProfileContext);
  if (!value) {
    throw new Error('useAdminVerticalProfile must be used within AdminVerticalProfileProvider');
  }
  return value;
}
