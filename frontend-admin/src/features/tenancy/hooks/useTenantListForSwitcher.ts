'use client';

import { useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import {
  filterTenantsForHeaderSearch,
  getTenantStatusIcon,
  sortTenantsForHeaderSwitcher,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { resolveTenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';

export type TenantListItemForSwitcher = {
  id: string;
  name: string;
  slug: string;
  status: string;
  isActive: boolean;
  adminEmail: string | null;
  licenseDaysLeft: number | null;
  statusIcon: string;
  /** Raw API row for shared header formatters. */
  source: AdminTenantListItem;
};

/** Seeded local DX tenants shown in the development header switcher. */
export const DEVELOPMENT_TENANTS = ['dev', 'development'] as const;

/** Sentinel / leftover demo tenants that must stay hidden in the development switcher. */
export const TEST_TENANTS = ['platform', 'test-bar', 'test-cafe'] as const;

const TEST_TENANT_SLUGS = new Set<string>([...TEST_TENANTS, 'bar', 'cafe']);

function normalizeSwitcherSlug(slug: string): string {
  return slug.trim().toLowerCase().replace(/_/g, '-');
}

export function isTestTenantSlug(slug: string): boolean {
  return TEST_TENANT_SLUGS.has(normalizeSwitcherSlug(slug));
}

/**
 * Development switcher visibility: only active `dev` / `development` seeds.
 * Production builds keep all non-test tenants (switcher itself is still local-only).
 */
export function isDevelopmentTenant(
  slug: string,
  nodeEnv: string | undefined = process.env.NODE_ENV
): boolean {
  const normalized = normalizeSwitcherSlug(slug);
  if (isTestTenantSlug(normalized)) {
    return false;
  }
  if (nodeEnv === 'development') {
    return (DEVELOPMENT_TENANTS as readonly string[]).includes(normalized);
  }
  return true;
}

function mapTenantForSwitcher(row: AdminTenantListItem): TenantListItemForSwitcher {
  const license = resolveTenantLicenseLabel(
    row.licenseValidUntilUtc,
    row.licenseKey,
    Date.now(),
    row.licenseDaysRemaining
  );
  return {
    id: row.id,
    name: row.name,
    slug: row.slug,
    status: row.status,
    isActive: row.isActive,
    adminEmail: row.ownerAdminEmail?.trim() ?? null,
    licenseDaysLeft: license.daysRemaining,
    statusIcon: getTenantStatusIcon(row),
    source: row,
  };
}

/**
 * Tenants for the dev header switcher.
 * Super Admin: seeded development tenants (`dev`); platform / Test Bar / Test Cafe are hidden.
 * Other users: tenants with an active membership (backend-filtered), then the same DX filter.
 */
export function useTenantListForSwitcher(options?: { includeDeleted?: boolean }) {
  const { user } = useAuth();
  const enabled = process.env.NODE_ENV === 'development' && Boolean(user?.id);
  const includeDeleted = options?.includeDeleted ?? false;

  const query = useGetApiAdminTenants(
    { includeDeleted },
    {
      enabled,
      staleTime: 60_000,
    }
  );

  const tenants = useMemo(() => {
    return (query.data ?? [])
      .filter((row) => includeDeleted || row.isActive)
      .filter((row) => isDevelopmentTenant(row.slug))
      .map(mapTenantForSwitcher);
  }, [includeDeleted, query.data]);

  return {
    tenants,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
    tenantCount: tenants.length,
  };
}

/** Client-side filter by name, slug, or admin email; preserves switcher sort order. */
export function filterTenantSwitcherItems(
  items: TenantListItemForSwitcher[],
  query: string
): TenantListItemForSwitcher[] {
  const sortedSources = sortTenantsForHeaderSwitcher(items.map((row) => row.source));
  const filteredSources = filterTenantsForHeaderSearch(sortedSources, query);
  const byId = new Map(items.map((row) => [row.id, row]));
  return filteredSources
    .map((row) => byId.get(row.id))
    .filter((row): row is TenantListItemForSwitcher => row != null);
}

/** Active tenant without an owner admin — Super Admin should confirm before switching. */
export function tenantNeedsNoAdminWarning(tenant: TenantListItemForSwitcher): boolean {
  return tenant.status === 'active' && tenant.isActive && !tenant.adminEmail;
}
