'use client';

import { Space, Tag, Typography } from 'antd';

import type { LimitStatusDto } from '@/features/tenants/api/tenantLimits';
import { limitDashboardLabelKey } from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { useI18n } from '@/i18n';

export function LimitCard({
  limit,
  tenantSlug,
  registerLabel,
}: {
  limit: LimitStatusDto;
  tenantSlug?: string | null;
  registerLabel?: string | null;
}) {
  const { t } = useI18n();
  const i18nKey = limitDashboardLabelKey(limit.key);
  const title = t(i18nKey);
  const displayTitle = title === i18nKey ? limit.displayName : title;

  return (
    <Space orientation="vertical" size={4}>
      <Space wrap size={8}>
        <Typography.Text strong>{displayTitle}</Typography.Text>
        {tenantSlug ? <Tag>{tenantSlug}</Tag> : null}
        {registerLabel ? <Tag>{registerLabel}</Tag> : null}
      </Space>
      {limit.description ? (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {limit.description}
        </Typography.Text>
      ) : null}
    </Space>
  );
}
