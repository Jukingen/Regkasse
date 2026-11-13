import type { TenantDataManagementOverview } from '@/features/data-management/api/adminDataManagement';
import type { TenantDataManagementSummary } from '@/features/data-management/api/tenantDataManagement';

/** Unified stats for SuperAdmin platform overview or Manager own-tenant summary. */
export type DataRetentionStats = {
  totalTenants: number;
  inGraceCount: number;
  lockedCount: number;
  pendingDeletionRequestCount: number;
  activeCount: number;
  oldestRksvData?: string | null;
  scope: 'platform' | 'tenant';
};

const PENDING_DELETION_STATUSES = new Set(['pending', 'export_ready', 'confirmed']);

export function mapPlatformOverviewToStats(
  overview: TenantDataManagementOverview
): DataRetentionStats {
  const activeCount = overview.items.filter((i) => i.lifecycleState === 'Active').length;
  let oldest: string | null = null;
  for (const item of overview.items) {
    if (!item.oldestRksvPaymentDate) continue;
    if (!oldest || item.oldestRksvPaymentDate < oldest) {
      oldest = item.oldestRksvPaymentDate;
    }
  }

  return {
    totalTenants: overview.totalTenants,
    inGraceCount: overview.inGraceCount,
    lockedCount: overview.lockedCount,
    pendingDeletionRequestCount: overview.pendingDeletionRequestCount,
    activeCount,
    oldestRksvData: oldest,
    scope: 'platform',
  };
}

export function mapTenantSummaryToStats(summary: TenantDataManagementSummary): DataRetentionStats {
  const status = summary.latestDeletionRequest?.status?.toLowerCase() ?? '';
  const pending =
    Boolean(summary.latestDeletionRequest) && PENDING_DELETION_STATUSES.has(status) ? 1 : 0;

  return {
    totalTenants: 1,
    inGraceCount: summary.isInGracePeriod ? 1 : 0,
    lockedCount: summary.isLocked ? 1 : 0,
    pendingDeletionRequestCount: pending,
    activeCount: summary.lifecycleState === 'Active' ? 1 : 0,
    oldestRksvData: summary.retention?.rksvData.oldestPaymentDate ?? null,
    scope: 'tenant',
  };
}
