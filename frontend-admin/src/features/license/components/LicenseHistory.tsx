'use client';

import { Card, Empty, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';

import type { TenantLicenseHistoryItem } from '@/features/license/api/tenantLicense';
import { useLicenseHistory } from '@/features/license/hooks/useLicenseHistory';
import {
  getLicenseHistoryEventLabel,
  getLicenseHistoryEventTagColor,
} from '@/features/license/utils/licenseHistoryLabels';
import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import { dateColumnRender } from '@/components/DateColumn';
import { useI18n } from '@/i18n';

export type LicenseHistoryProps = {
  tenantId?: string;
};

export function LicenseHistory({ tenantId }: LicenseHistoryProps) {
  const { t } = useI18n();
  const historyQuery = useLicenseHistory(tenantId);
  const items = historyQuery.data ?? [];

  const columns: ColumnsType<TenantLicenseHistoryItem> = [
    {
      title: t('license.history.columns.date'),
      dataIndex: 'atUtc',
      key: 'atUtc',
      width: 170,
      render: dateColumnRender('datetime'),
    },
    {
      title: t('license.history.columns.event'),
      dataIndex: 'eventType',
      key: 'eventType',
      width: 140,
      render: (value: string) => (
        <Tag color={getLicenseHistoryEventTagColor(value)}>
          {getLicenseHistoryEventLabel(value, t)}
        </Tag>
      ),
    },
    {
      title: t('license.history.columns.summary'),
      dataIndex: 'summary',
      key: 'summary',
      ellipsis: true,
    },
    {
      title: t('license.history.columns.actor'),
      dataIndex: 'actorDisplayName',
      key: 'actorDisplayName',
      width: 160,
      ellipsis: true,
      render: (value: string | null | undefined) => value?.trim() || '—',
    },
    {
      title: t('license.history.columns.licenseKey'),
      dataIndex: 'licenseKey',
      key: 'licenseKey',
      width: 180,
      ellipsis: true,
      render: (value: string | null | undefined) =>
        value ? <Typography.Text code>{maskTenantLicenseKey(value)}</Typography.Text> : '—',
    },
  ];

  return (
    <Card title={t('license.history.title')} loading={historyQuery.isLoading}>
      {!historyQuery.isLoading && items.length === 0 ? (
        <Empty description={t('license.history.empty')} />
      ) : (
        <Table<TenantLicenseHistoryItem>
          rowKey={(row) =>
            `${row.eventType}-${row.atUtc}-${row.summary}-${row.licenseKey ?? ''}-${row.actorUserId ?? ''}`
          }
          size="small"
          pagination={{ pageSize: 10, hideOnSinglePage: true }}
          columns={columns}
          dataSource={items}
          scroll={{ x: 860 }}
        />
      )}
    </Card>
  );
}
