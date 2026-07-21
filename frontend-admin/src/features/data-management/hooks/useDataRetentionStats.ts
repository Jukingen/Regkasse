'use client';

import { useQuery } from '@tanstack/react-query';

import { DASHBOARD_AUTO_REFRESH_MS } from '@/features/dashboard/types';
import { listDataManagementOverview } from '@/features/data-management/api/adminDataManagement';
import { getTenantDataManagementSummary } from '@/features/data-management/api/tenantDataManagement';
import {
  type DataRetentionStats,
  mapPlatformOverviewToStats,
  mapTenantSummaryToStats,
} from '@/features/data-management/logic/dataRetentionStats';
import { useCurrentTenant } from '@/hooks/useCurrentTenant';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

export const dataRetentionStatsQueryKey = (scope: 'platform' | 'tenant', tenantId?: string) =>
  ['data-retention-stats', scope, tenantId ?? null] as const;

/**
 * SuperAdmin → platform overview; Manager → ambient tenant summary.
 * Gated by backup.manage (Managers) or system.critical / SuperAdmin role.
 */
export function useDataRetentionStats() {
  const { isSuperAdmin, user } = usePermissions();
  const { tenantId, isRealTenantSlug } = useCurrentTenant();

  const canBackup = hasPermission(
    user ? { permissions: user.permissions } : null,
    PERMISSIONS.BACKUP_MANAGE
  );
  const canSystem = hasPermission(
    user ? { permissions: user.permissions } : null,
    PERMISSIONS.SYSTEM_CRITICAL
  );
  const allowed = isSuperAdmin || canSystem || canBackup;

  const platformEnabled = allowed && isSuperAdmin;
  const tenantEnabled = allowed && !isSuperAdmin && Boolean(tenantId) && isRealTenantSlug;

  return useQuery({
    queryKey: dataRetentionStatsQueryKey(
      platformEnabled ? 'platform' : 'tenant',
      platformEnabled ? undefined : (tenantId ?? undefined)
    ),
    queryFn: async (): Promise<DataRetentionStats> => {
      if (platformEnabled) {
        const overview = await listDataManagementOverview();
        return mapPlatformOverviewToStats(overview);
      }
      if (!tenantId) {
        throw new Error('Tenant context required');
      }
      const summary = await getTenantDataManagementSummary(tenantId);
      return mapTenantSummaryToStats(summary);
    },
    enabled: platformEnabled || tenantEnabled,
    staleTime: DASHBOARD_AUTO_REFRESH_MS / 2,
    refetchInterval: DASHBOARD_AUTO_REFRESH_MS,
  });
}
