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

import {
  type LicenseActivity,
  useLicenseDashboardStats,
} from '@/features/license/api/licenseStats';
import {
  mapLicenseActivityFeedItem,
  type LicenseActivityFeedType,
} from '@/features/license/utils/licenseActivityFeed';
import { useI18n } from '@/i18n';
import dayjs from '@/lib/dayjs';

const FEED_MAX_HEIGHT = 384;

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
  activity: LicenseActivity;
};

function LicenseActivityFeedRow({ activity }: FeedRowProps) {
  const { t } = useI18n();
  const mapped = mapLicenseActivityFeedItem({
    action: activity.action,
    sourceCode: activity.sourceCode,
    licenseKeyMasked: activity.licenseKeyMasked,
    timestampUtc: activity.timestampUtc,
  });

  const when = dayjs(activity.timestampUtc);
  const absolute = when.isValid() ? when.format('DD.MM.YYYY HH:mm') : '—';
  const relative = when.isValid() ? when.fromNow() : '—';

  return (
    <Flex
      align="center"
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
        }}
      >
        {feedIcon(mapped.type)}
      </Flex>
      <div style={{ flex: 1, minWidth: 0 }}>
        <Typography.Text style={{ display: 'block' }}>
          {t(mapped.descriptionKey, mapped.descriptionParams)}
        </Typography.Text>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {relative}
        </Typography.Text>
      </div>
      <Typography.Text type="secondary" style={{ fontSize: 12, flexShrink: 0 }}>
        {absolute}
      </Typography.Text>
    </Flex>
  );
}

export type LicenseActivityLogCardProps = {
  /** When provided, skip the stats query (parent already loaded activities). */
  activities?: LicenseActivity[] | null;
  loading?: boolean;
  /** Compact card without outer title when embedded under a section heading. */
  embedded?: boolean;
};

/**
 * Scrollable Super Admin feed of recent license lifecycle / activation events.
 */
export function LicenseActivityLogCard({
  activities: activitiesProp,
  loading: loadingProp,
  embedded = false,
}: LicenseActivityLogCardProps = {}) {
  const { t } = useI18n();
  const statsQuery = useLicenseDashboardStats({
    enabled: activitiesProp === undefined,
  });

  const loading = loadingProp ?? (activitiesProp === undefined && statsQuery.isLoading);
  const activities = useMemo(
    () => activitiesProp ?? statsQuery.data?.recentActivities ?? [],
    [activitiesProp, statsQuery.data?.recentActivities]
  );

  const body = loading ? (
    <Skeleton active paragraph={{ rows: 5 }} title={false} />
  ) : activities.length === 0 ? (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={t('license.activityLog.empty')}
    />
  ) : (
    <div style={{ maxHeight: FEED_MAX_HEIGHT, overflowY: 'auto' }}>
      {activities.map((row, index) => (
        <LicenseActivityFeedRow
          key={`lic-feed-${row.timestampUtc}-${row.sourceCode}-${row.licenseKeyMasked}-${row.action}-${index}`}
          activity={row}
        />
      ))}
    </div>
  );

  if (embedded) {
    return <div>{body}</div>;
  }

  return <Card title={t('license.activityLog.title')}>{body}</Card>;
}
