import { AXIOS_INSTANCE } from '@/lib/axios';

export type TenantLimitsDto = {
  id: string;
  tenantId: string;
  maxActiveRegistersPerUser: number;
  maxProductsPerTenant: number;
  maxUsersPerTenant: number;
  dailyMaxTransactions: number;
  maxTransactionAmount: number;
  dailyMaxRevenue: number;
  maxBackupsPerTenant: number;
  maxBackupSizeMB: number;
  maxOfflineTransactions: number;
  createdAt: string;
  updatedAt: string;
};

export type UpdateTenantLimitsRequest = {
  maxActiveRegistersPerUser: number;
  maxProductsPerTenant: number;
  maxUsersPerTenant: number;
  dailyMaxTransactions: number;
  maxTransactionAmount: number;
  dailyMaxRevenue: number;
  maxBackupsPerTenant: number;
  maxBackupSizeMB: number;
  maxOfflineTransactions: number;
};

export const tenantLimitsQueryKeys = {
  root: ['admin', 'tenant-limits'] as const,
  byTenant: (tenantId: string) => [...tenantLimitsQueryKeys.root, tenantId] as const,
  usage: ['admin', 'tenant-limits', 'usage'] as const,
  dashboard: (allTenants: boolean, tenantId?: string | null) =>
    ['admin', 'tenant-limits', 'dashboard', allTenants, tenantId ?? ''] as const,
};

export type TenantLimitUsageDto = {
  tenantId: string;
  limits: TenantLimitsDto;
  currentProducts: number;
  currentUsers: number;
  currentDailyTransactions: number;
  currentDailyRevenue: number;
  currentBackups: number;
  currentBackupSizeMb: number;
  currentOfflineTransactions: number;
  currentMaxAssignedRegistersPerUser: number;
};

export type LimitHealthStatus = 'Healthy' | 'Warning' | 'Critical';
export type LimitTrend = 'Increasing' | 'Stable' | 'Decreasing';
export type CriticalUserStatus = 'Approaching' | 'Full' | 'Exceeded';

export type DashboardSummaryDto = {
  total: number;
  healthy: number;
  warning: number;
  critical: number;
};

export type LimitStatusDto = {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  key: string;
  displayName: string;
  description: string;
  current: number;
  limit: number;
  percentage: number;
  status: LimitHealthStatus;
  trend: LimitTrend;
  changeCount: number;
  changeUnit: string;
};

export type CriticalUserDto = {
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  userId: string;
  userName: string;
  displayName: string;
  role: string;
  limitKey: string;
  current: number;
  limit: number;
  percentage: number;
  status: CriticalUserStatus;
  recommendedAction: string;
};

export type LimitActivityDto = {
  id: string;
  timestampUtc: string;
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  limitKey: string;
  eventType: string;
  status: string;
  description: string;
  userName?: string | null;
  isRead: boolean;
};

export type LimitDashboardDto = {
  lastUpdated: string;
  summary: DashboardSummaryDto;
  limits: LimitStatusDto[];
  criticalUsers: CriticalUserDto[];
  recentActivity: LimitActivityDto[];
  totalViolations: number;
  approachingLimits: number;
  unreadAlertCount: number;
  allTenants: boolean;
};

/** GET /api/admin/limits (ambient tenant usage) */
export async function getTenantLimitUsage(): Promise<TenantLimitUsageDto> {
  const { data } = await AXIOS_INSTANCE.get<TenantLimitUsageDto>('/api/admin/limits');
  return data;
}

/** GET /api/admin/limits/dashboard */
export async function getLimitDashboard(options?: {
  allTenants?: boolean;
  tenantId?: string | null;
}): Promise<LimitDashboardDto> {
  const allTenants = options?.allTenants === true;
  const tenantId = options?.tenantId?.trim();
  const { data } = await AXIOS_INSTANCE.get<LimitDashboardDto>('/api/admin/limits/dashboard', {
    params: {
      ...(allTenants ? { allTenants: true } : {}),
      ...(!allTenants && tenantId ? { tenantId } : {}),
    },
  });
  return data;
}

/** GET /api/admin/tenants/{tenantId}/limits */
export async function getTenantLimits(tenantId: string): Promise<TenantLimitsDto> {
  const { data } = await AXIOS_INSTANCE.get<TenantLimitsDto>(
    `/api/admin/tenants/${tenantId}/limits`
  );
  return data;
}

/** PUT /api/admin/tenants/{tenantId}/limits */
export async function updateTenantLimits(
  tenantId: string,
  body: UpdateTenantLimitsRequest
): Promise<TenantLimitsDto> {
  const { data } = await AXIOS_INSTANCE.put<TenantLimitsDto>(
    `/api/admin/tenants/${tenantId}/limits`,
    body
  );
  return data;
}

/** POST /api/admin/tenants/{tenantId}/limits/reset */
export async function resetTenantLimits(tenantId: string): Promise<TenantLimitsDto> {
  const { data } = await AXIOS_INSTANCE.post<TenantLimitsDto>(
    `/api/admin/tenants/${tenantId}/limits/reset`
  );
  return data;
}
