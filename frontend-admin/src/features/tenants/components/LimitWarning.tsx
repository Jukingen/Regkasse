'use client';

import { Alert, Space } from 'antd';

import { useTenantLimitUsage } from '@/features/tenants/hooks/useTenantLimits';
import { useI18n } from '@/i18n';

export type TenantLimitWarningKey =
  | 'maxProductsPerTenant'
  | 'maxUsersPerTenant'
  | 'dailyMaxTransactions'
  | 'dailyMaxRevenue'
  | 'maxBackupsPerTenant'
  | 'maxBackupSizeMB'
  | 'maxOfflineTransactions'
  | 'maxActiveRegistersPerUser';

const LABEL_KEYS = {
  maxProductsPerTenant: 'tenants.limits.maxProductsPerTenant',
  maxUsersPerTenant: 'tenants.limits.maxUsersPerTenant',
  dailyMaxTransactions: 'tenants.limits.dailyMaxTransactions',
  dailyMaxRevenue: 'tenants.limits.dailyMaxRevenue',
  maxBackupsPerTenant: 'tenants.limits.maxBackupsPerTenant',
  maxBackupSizeMB: 'tenants.limits.maxBackupSizeMB',
  maxOfflineTransactions: 'tenants.limits.maxOfflineTransactions',
  maxActiveRegistersPerUser: 'tenants.limits.maxActiveRegistersPerUser',
} as const;

type LimitWarningProps = {
  limitKey: TenantLimitWarningKey | TenantLimitWarningKey[];
  warningThreshold?: number;
};

function readUsage(
  key: TenantLimitWarningKey,
  usage: NonNullable<ReturnType<typeof useTenantLimitUsage>['data']>
): { current: number; limit: number } | null {
  const { limits } = usage;
  switch (key) {
    case 'maxProductsPerTenant':
      return { current: usage.currentProducts, limit: limits.maxProductsPerTenant };
    case 'maxUsersPerTenant':
      return { current: usage.currentUsers, limit: limits.maxUsersPerTenant };
    case 'dailyMaxTransactions':
      return { current: usage.currentDailyTransactions, limit: limits.dailyMaxTransactions };
    case 'dailyMaxRevenue':
      return { current: usage.currentDailyRevenue, limit: limits.dailyMaxRevenue };
    case 'maxBackupsPerTenant':
      return { current: usage.currentBackups ?? 0, limit: limits.maxBackupsPerTenant };
    case 'maxBackupSizeMB':
      return { current: usage.currentBackupSizeMb ?? 0, limit: limits.maxBackupSizeMB };
    case 'maxOfflineTransactions':
      return {
        current: usage.currentOfflineTransactions ?? 0,
        limit: limits.maxOfflineTransactions,
      };
    case 'maxActiveRegistersPerUser':
      return {
        current: usage.currentMaxAssignedRegistersPerUser ?? 0,
        limit: limits.maxActiveRegistersPerUser,
      };
    default:
      return null;
  }
}

export function LimitWarning({ limitKey, warningThreshold = 0.8 }: LimitWarningProps) {
  const { t } = useI18n();
  const { data } = useTenantLimitUsage();
  const keys = Array.isArray(limitKey) ? limitKey : [limitKey];

  if (!data?.limits) return null;

  const alerts = keys.flatMap((key) => {
    const pair = readUsage(key, data);
    if (!pair || pair.limit <= 0) return [];
    const percentage = pair.current / pair.limit;
    if (percentage < warningThreshold) return [];

    const reached = percentage >= 1;
    const remaining = Math.max(0, pair.limit - pair.current);
    return [
      <Alert
        key={key}
        type={reached ? 'error' : 'warning'}
        showIcon
        title={t('tenants.limits.warningUsage', {
          current: pair.current,
          limit: pair.limit,
          name: t(LABEL_KEYS[key]),
        })}
        description={
          reached
            ? t('tenants.limits.warningReached')
            : t('tenants.limits.warningRemaining', { remaining })
        }
      />,
    ];
  });

  if (alerts.length === 0) return null;
  return (
    <Space orientation="vertical" size={8} style={{ width: '100%', marginBottom: 16 }}>
      {alerts}
    </Space>
  );
}
