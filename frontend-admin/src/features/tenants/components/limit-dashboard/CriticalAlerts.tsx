'use client';

import { Alert, Space } from 'antd';

import type { CriticalUserDto, LimitStatusDto } from '@/features/tenants/api/tenantLimits';
import { limitDashboardLabelKey } from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { useI18n } from '@/i18n';

export function CriticalAlerts({
  limits,
  users,
}: {
  limits: LimitStatusDto[];
  users: CriticalUserDto[];
}) {
  const { t } = useI18n();
  const criticalLimits = limits.filter((row) => row.status === 'Critical');
  const warningLimits = limits.filter((row) => row.status === 'Warning');
  const hotUsers = users.filter((row) => row.status === 'Exceeded' || row.status === 'Full');

  if (criticalLimits.length === 0 && warningLimits.length === 0 && hotUsers.length === 0) {
    return null;
  }

  const limitName = (key: string, fallback: string) => {
    const i18nKey = limitDashboardLabelKey(key);
    const label = t(i18nKey);
    return label === i18nKey ? fallback : label;
  };

  return (
    <Space orientation="vertical" size={8} style={{ width: '100%' }}>
      {criticalLimits.map((row) => (
        <Alert
          key={`c-${row.tenantId}-${row.key}`}
          type="error"
          showIcon
          title={t('tenants.limits.dashboard.alerts.criticalLimit', {
            name: limitName(row.key, row.displayName),
            percent: String(row.percentage),
          })}
        />
      ))}
      {hotUsers.map((row) => (
        <Alert
          key={`u-${row.tenantId}-${row.userId}`}
          type="error"
          showIcon
          title={t('tenants.limits.dashboard.alerts.criticalUser', {
            user: row.displayName || row.userName,
            name: limitName(row.limitKey, row.limitKey),
            percent: String(row.percentage),
          })}
        />
      ))}
      {warningLimits.map((row) => (
        <Alert
          key={`w-${row.tenantId}-${row.key}`}
          type="warning"
          showIcon
          title={t('tenants.limits.dashboard.alerts.warningLimit', {
            name: limitName(row.key, row.displayName),
            percent: String(row.percentage),
          })}
        />
      ))}
    </Space>
  );
}
