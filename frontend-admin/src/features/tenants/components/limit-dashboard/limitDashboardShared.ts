import type { LimitHealthStatus } from '@/features/tenants/api/tenantLimits';

export const LIMIT_DASHBOARD_LABEL_KEYS: Record<string, string> = {
  maxActiveRegistersPerUser: 'tenants.limits.dashboard.limits.maxActiveRegistersPerUser',
  maxProductsPerTenant: 'tenants.limits.dashboard.limits.maxProductsPerTenant',
  maxUsersPerTenant: 'tenants.limits.dashboard.limits.maxUsersPerTenant',
  dailyMaxTransactions: 'tenants.limits.dashboard.limits.dailyMaxTransactions',
  maxTransactionAmount: 'tenants.limits.dashboard.limits.maxTransactionAmount',
  dailyMaxRevenue: 'tenants.limits.dashboard.limits.dailyMaxRevenue',
  maxBackupsPerTenant: 'tenants.limits.dashboard.limits.maxBackupsPerTenant',
  maxBackupSizeMB: 'tenants.limits.dashboard.limits.maxBackupSizeMB',
  maxOfflineTransactions: 'tenants.limits.dashboard.limits.maxOfflineTransactions',
};

export type LimitStatusFilter = 'all' | LimitHealthStatus;

export function limitDashboardLabelKey(key: string): string {
  return LIMIT_DASHBOARD_LABEL_KEYS[key] ?? `tenants.limits.${key}`;
}

export function limitDashboardDetailHref(
  key: string,
  tenantId: string,
  isSuperAdmin: boolean
): string {
  if (isSuperAdmin && tenantId) {
    return `/admin/tenants/${tenantId}?tab=limits`;
  }

  switch (key) {
    case 'maxProductsPerTenant':
      return '/products';
    case 'maxUsersPerTenant':
      return '/admin/users';
    case 'maxActiveRegistersPerUser':
      return '/kassenverwaltung';
    case 'dailyMaxTransactions':
    case 'maxTransactionAmount':
    case 'dailyMaxRevenue':
      return '/payments';
    case 'maxBackupsPerTenant':
    case 'maxBackupSizeMB':
      return '/backup';
    case 'maxOfflineTransactions':
      return '/admin/tse/offline-transactions';
    default:
      return '/admin/limits/dashboard';
  }
}

export function healthTagColor(status: string): string {
  if (status === 'Critical' || status === 'Exceeded') return 'red';
  if (status === 'Warning' || status === 'Approaching' || status === 'Full') return 'gold';
  return 'green';
}

export function healthProgressStatus(status: string): 'success' | 'normal' | 'exception' {
  if (status === 'Critical') return 'exception';
  if (status === 'Warning') return 'normal';
  return 'success';
}

export function healthStatusI18nKey(status: string): string {
  return `tenants.limits.dashboard.status.${status}`;
}

export function trendI18nKey(trend: string): string {
  return `tenants.limits.dashboard.trend.${trend}`;
}
