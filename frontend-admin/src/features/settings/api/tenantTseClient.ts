import { customInstance } from '@/lib/axios';

export interface TenantTseDeviceStatus {
  deviceId: string;
  serialNumber?: string | null;
  isPrimary: boolean;
  isBackup: boolean;
  healthStatus: string;
  healthScore: number;
  expiresAt?: string | null;
  daysUntilExpiry?: number | null;
  lastHealthCheck?: string | null;
}

export interface TenantTseStatus {
  tenantId: string;
  devices: TenantTseDeviceStatus[];
  overallHealth: 'Healthy' | 'Degraded' | 'Unknown' | string;
  overallHealthScore: number;
  nearestDaysUntilExpiry?: number | null;
  generatedAt: string;
}

export interface TenantTseHealthHistoryPoint {
  checkedAtUtc: string;
  deviceId?: string | null;
  serialNumber?: string | null;
  healthScore: number;
  healthStatus: string;
  responseTimeMs?: number | null;
}

export interface TenantTseHealthHistory {
  tenantId: string;
  days: number;
  points: TenantTseHealthHistoryPoint[];
}

export async function getTenantTseStatus(signal?: AbortSignal): Promise<TenantTseStatus> {
  return customInstance<TenantTseStatus>({
    url: '/api/tenant/tse/status',
    method: 'GET',
    signal,
  });
}

export async function getTenantTseHealthHistory(
  days = 30,
  signal?: AbortSignal
): Promise<TenantTseHealthHistory> {
  return customInstance<TenantTseHealthHistory>({
    url: '/api/tenant/tse/health-history',
    method: 'GET',
    params: { days },
    signal,
  });
}
