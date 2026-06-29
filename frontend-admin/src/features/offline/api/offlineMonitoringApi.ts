import { customInstance } from '@/lib/axios';

export type OfflineSyncHealth = {
  isHealthy: boolean;
  avgSyncTimeMs: number;
  successRate: number;
  totalAttempts: number;
  failedAttempts: number;
};

export type OfflineMonitoringStatus = {
  totalPendingOrders: number;
  totalPendingTransactions: number;
  totalExpiredOrders: number;
  totalFailedSyncs: number;
  oldestPendingOrder: string | null;
  lastSyncAt: string | null;
  hasCriticalIssues: boolean;
  syncHealth: OfflineSyncHealth;
};

type OfflineSystemStatusApiDto = {
  totalPendingOrders?: number;
  TotalPendingOrders?: number;
  totalPendingTransactions?: number;
  TotalPendingTransactions?: number;
  totalExpiredOrders?: number;
  TotalExpiredOrders?: number;
  totalFailedSyncs?: number;
  TotalFailedSyncs?: number;
  oldestPendingOrder?: string | null;
  OldestPendingOrder?: string | null;
  lastSyncAt?: string | null;
  LastSyncAt?: string | null;
  hasCriticalIssues?: boolean;
  HasCriticalIssues?: boolean;
};

type SyncHealthApiDto = {
  isHealthy?: boolean;
  IsHealthy?: boolean;
  avgSyncTimeMs?: number;
  AvgSyncTimeMs?: number;
  successRate?: number;
  SuccessRate?: number;
  totalAttempts?: number;
  TotalAttempts?: number;
  failedAttempts?: number;
  FailedAttempts?: number;
};

export function mapSyncHealth(dto: SyncHealthApiDto): OfflineSyncHealth {
  return {
    isHealthy: dto.isHealthy ?? dto.IsHealthy ?? true,
    avgSyncTimeMs: dto.avgSyncTimeMs ?? dto.AvgSyncTimeMs ?? 0,
    successRate: dto.successRate ?? dto.SuccessRate ?? 100,
    totalAttempts: dto.totalAttempts ?? dto.TotalAttempts ?? 0,
    failedAttempts: dto.failedAttempts ?? dto.FailedAttempts ?? 0,
  };
}

export function mapStatus(
  status: OfflineSystemStatusApiDto,
  syncHealth: OfflineSyncHealth,
): OfflineMonitoringStatus {
  return {
    totalPendingOrders: status.totalPendingOrders ?? status.TotalPendingOrders ?? 0,
    totalPendingTransactions:
      status.totalPendingTransactions ?? status.TotalPendingTransactions ?? 0,
    totalExpiredOrders: status.totalExpiredOrders ?? status.TotalExpiredOrders ?? 0,
    totalFailedSyncs: status.totalFailedSyncs ?? status.TotalFailedSyncs ?? 0,
    oldestPendingOrder: status.oldestPendingOrder ?? status.OldestPendingOrder ?? null,
    lastSyncAt: status.lastSyncAt ?? status.LastSyncAt ?? null,
    hasCriticalIssues: status.hasCriticalIssues ?? status.HasCriticalIssues ?? false,
    syncHealth,
  };
}

export async function fetchOfflineMonitoringStatus(
  signal?: AbortSignal,
): Promise<OfflineMonitoringStatus> {
  const [statusRes, syncRes] = await Promise.all([
    customInstance<OfflineSystemStatusApiDto>({
      url: '/api/admin/offline-monitoring/status',
      method: 'GET',
      signal,
    }),
    customInstance<SyncHealthApiDto>({
      url: '/api/admin/offline-monitoring/sync-health',
      method: 'GET',
      signal,
    }),
  ]);

  const syncHealth = mapSyncHealth(syncRes ?? {});
  return mapStatus(statusRes ?? {}, syncHealth);
}

export const OFFLINE_MONITORING_QUERY_KEY = ['offline-monitoring', 'status'] as const;

/** Default tenant-wide pending order cap (matches backend OfflineAlertRules). */
export const OFFLINE_PENDING_ORDERS_CAP = 50;
