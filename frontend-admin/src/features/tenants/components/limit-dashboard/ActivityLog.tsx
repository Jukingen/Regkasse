'use client';

import { Card, Table, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';

import type { LimitActivityDto } from '@/features/tenants/api/tenantLimits';
import {
  healthStatusI18nKey,
  healthTagColor,
  limitDashboardLabelKey,
} from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { useI18n } from '@/i18n';

export function ActivityLog({
  rows,
  showTenant,
}: {
  rows: LimitActivityDto[];
  showTenant: boolean;
}) {
  const { t } = useI18n();

  const limitName = (key: string) => {
    const i18nKey = limitDashboardLabelKey(key);
    const label = t(i18nKey);
    return label === i18nKey ? key : label;
  };

  const columns: ColumnsType<LimitActivityDto> = [
    {
      title: t('tenants.limits.dashboard.activity.time'),
      dataIndex: 'timestampUtc',
      key: 'timestampUtc',
      width: 170,
      render: (value: string) => dayjs(value).format('DD.MM.YYYY HH:mm'),
    },
    ...(showTenant
      ? [
          {
            title: t('tenants.limits.dashboard.tenant'),
            dataIndex: 'tenantName',
            key: 'tenantName',
            render: (name: string | null | undefined, row: LimitActivityDto) =>
              name || row.tenantId,
          },
        ]
      : []),
    {
      title: t('tenants.limits.dashboard.activity.limit'),
      dataIndex: 'limitKey',
      key: 'limitKey',
      render: (key: string) => limitName(key),
    },
    {
      title: t('tenants.limits.dashboard.activity.status'),
      dataIndex: 'status',
      key: 'status',
      render: (status: string, row: LimitActivityDto) => {
        const eventKey = `activityNotifications.eventTypes.${row.eventType}`;
        const eventLabel = t(eventKey);
        return (
          <Tag color={healthTagColor(status)}>
            {eventLabel === eventKey ? t(healthStatusI18nKey(status)) : eventLabel}
          </Tag>
        );
      },
    },
    {
      title: t('tenants.limits.dashboard.activity.description'),
      dataIndex: 'description',
      key: 'description',
    },
  ];

  return (
    <Card variant="borderless" title={t('tenants.limits.dashboard.activity.title')}>
      <Table
        rowKey="id"
        columns={columns}
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('tenants.limits.dashboard.emptyLogs') }}
      />
    </Card>
  );
}
