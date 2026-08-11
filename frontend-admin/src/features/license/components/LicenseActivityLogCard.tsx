'use client';

import {
  EyeOutlined,
  KeyOutlined,
  MailOutlined,
  MoreOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { Card, Empty, Flex, Skeleton, Typography } from 'antd';
import React, { useMemo } from 'react';

import type { LicenseAuditLogItem } from '@/api/manual/adminLicense';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useLicenseAuditLog } from '@/features/license/hooks/useLicenseAuditLog';
import {
  mapLicenseAuditFeedItem,
  type LicenseActivityFeedType,
} from '@/features/license/utils/licenseActivityFeed';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';

const FEED_MAX_HEIGHT = 384;
const FEED_PAGE_SIZE = 15;
const LOOKBACK_DAYS = 30;

function feedIcon(type: LicenseActivityFeedType) {
  switch (type) {
    case 'renewal':
      return <KeyOutlined style={{ color: '#1890ff' }} aria-hidden />;
    case 'reminder':
      return <MailOutlined style={{ color: '#722ed1' }} aria-hidden />;
    case 'expiry':
      return <WarningOutlined style={{ color: '#faad14' }} aria-hidden />;
    case 'view':
      return <EyeOutlined style={{ color: '#8c8c8c' }} aria-hidden />;
    default:
      return <MoreOutlined style={{ color: '#8c8c8c' }} aria-hidden />;
  }
}

function feedIconBackground(type: LicenseActivityFeedType): string {
  switch (type) {
    case 'renewal':
      return '#e6f4ff';
    case 'reminder':
      return '#f9f0ff';
    case 'expiry':
      return '#fff7e6';
    case 'view':
      return '#f5f5f5';
    default:
      return '#fafafa';
  }
}

type FeedRowProps = {
  item: LicenseAuditLogItem;
};

function LicenseActivityFeedRow({ item }: FeedRowProps) {
  const { t } = useI18n();
  const mapped = mapLicenseAuditFeedItem({
    action: item.action,
    tenantName: item.tenantName,
    performedBy: item.performedBy,
    createdAtUtc: item.createdAtUtc,
  });

  const when = dayjs(mapped.timestampUtc);
  const absolute = when.isValid() ? when.format('DD.MM.YYYY HH:mm') : '—';

  const translatedAction = t(mapped.actionLabelKey, { key: mapped.actionCode });
  const actionLabel =
    translatedAction === mapped.actionLabelKey ? mapped.actionCode : translatedAction;
  const tenantLabel = mapped.tenantName ?? t('license.activityLog.unknownTenant');
  const userLabel = mapped.performedBy ?? t('license.activityLog.unknownUser');

  return (
    <Flex
      align="flex-start"
      gap={12}
      style={{
        padding: '8px 4px',
        borderRadius: 6,
      }}
    >
      <Flex
        align="center"
        justify="center"
        style={{
          width: 32,
          height: 32,
          borderRadius: 8,
          background: feedIconBackground(mapped.type),
          flexShrink: 0,
          marginTop: 2,
        }}
      >
        {feedIcon(mapped.type)}
      </Flex>
      <div style={{ flex: 1, minWidth: 0 }}>
        <Typography.Text style={{ display: 'block' }}>{actionLabel}</Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
          {t('license.activityLog.meta', { tenant: tenantLabel, user: userLabel })}
        </Typography.Text>
      </div>
      <Typography.Text type="secondary" style={{ fontSize: 12, flexShrink: 0 }}>
        {absolute}
      </Typography.Text>
    </Flex>
  );
}

export type LicenseActivityLogCardProps = {
  /** Compact card without outer title when embedded under a section heading. */
  embedded?: boolean;
};

/**
 * Scrollable Super Admin feed of recent mandant license lifecycle events
 * (GET /api/admin/license/audit, last 30 days).
 */
export function LicenseActivityLogCard({ embedded = false }: LicenseActivityLogCardProps = {}) {
  const { t } = useI18n();
  const canAccess = useBillingAccess();
  const fromUtc = useMemo(
    () => dayjs.utc().subtract(LOOKBACK_DAYS, 'day').startOf('day').toISOString(),
    []
  );

  const auditQuery = useLicenseAuditLog(
    {
      page: 1,
      pageSize: FEED_PAGE_SIZE,
      fromUtc,
    },
    canAccess
  );

  // Disabled queries stay pending without fetching — avoid empty-state flash during auth.
  const loading = !canAccess || auditQuery.isLoading || auditQuery.isPending;
  const activities = auditQuery.data?.items ?? [];

  const body = loading ? (
    <Skeleton active paragraph={{ rows: 5 }} title={false} />
  ) : activities.length === 0 ? (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={t('license.activityLog.empty')}
    />
  ) : (
    <div style={{ maxHeight: FEED_MAX_HEIGHT, overflowY: 'auto' }}>
      {activities.map((row) => (
        <LicenseActivityFeedRow key={row.id || `${row.createdAtUtc}-${row.action}`} item={row} />
      ))}
    </div>
  );

  if (embedded) {
    return <div>{body}</div>;
  }

  return <Card title={t('license.activityLog.title')}>{body}</Card>;
}
