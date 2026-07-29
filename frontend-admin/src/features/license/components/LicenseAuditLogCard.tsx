'use client';

import { Card, Empty, Table, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import React, { useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import type { LicenseAuditLogItem } from '@/api/manual/adminLicense';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useLicenseAuditLog } from '@/features/license/hooks/useLicenseAuditLog';
import { useI18n } from '@/i18n';

function formatStatusLabel(
  t: (key: string) => string,
  status: string | null | undefined
): string {
  if (!status) return '—';
  const key = `license.auditLog.statuses.${status}`;
  const label = t(key);
  return label === key ? status : label;
}

function statusTagColor(status: string | null | undefined): string {
  switch (status) {
    case 'Active':
      return 'green';
    case 'Grace':
      return 'gold';
    case 'Expired':
    case 'Locked':
    case 'Archived':
      return 'red';
    default:
      return 'default';
  }
}

function actionTagColor(action: string): string {
  if (action.includes('CANCEL') || action.includes('REFUND')) return 'red';
  if (action.includes('REMINDER')) return 'purple';
  if (action.includes('ACTIVAT') || action.includes('RENEW') || action.includes('EXTEND'))
    return 'blue';
  if (action.includes('SALE')) return 'cyan';
  return 'default';
}

/**
 * Super Admin unified license audit table (billing_audit_log + LICENSE_* audit_logs).
 */
export function LicenseAuditLogCard() {
  const { t } = useI18n();
  const canAccess = useBillingAccess();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const auditQuery = useLicenseAuditLog({ page, pageSize }, canAccess);

  const columns = useMemo<ColumnsType<LicenseAuditLogItem>>(
    () => [
      {
        title: t('license.auditLog.columns.time'),
        dataIndex: 'createdAtUtc',
        key: 'createdAtUtc',
        width: 170,
        render: dateColumnRender('datetime'),
      },
      {
        title: t('license.auditLog.columns.tenant'),
        dataIndex: 'tenantName',
        key: 'tenantName',
        width: 160,
        render: (value: string | null | undefined) => value ?? '—',
      },
      {
        title: t('license.auditLog.columns.action'),
        dataIndex: 'action',
        key: 'action',
        width: 180,
        render: (action: string) => {
          const key = `license.auditLog.actions.${action}`;
          const label = t(key);
          return (
            <Tag color={actionTagColor(action)}>{label === key ? action : label}</Tag>
          );
        },
      },
      {
        title: t('license.auditLog.columns.fromStatus'),
        dataIndex: 'fromStatus',
        key: 'fromStatus',
        width: 110,
        render: (status: string | null | undefined) =>
          status ? (
            <Tag color={statusTagColor(status)}>{formatStatusLabel(t, status)}</Tag>
          ) : (
            '—'
          ),
      },
      {
        title: t('license.auditLog.columns.toStatus'),
        dataIndex: 'toStatus',
        key: 'toStatus',
        width: 110,
        render: (status: string | null | undefined) =>
          status ? (
            <Tag color={statusTagColor(status)}>{formatStatusLabel(t, status)}</Tag>
          ) : (
            '—'
          ),
      },
      {
        title: t('license.auditLog.columns.performedBy'),
        dataIndex: 'performedBy',
        key: 'performedBy',
        width: 140,
        render: (value: string | null | undefined) => value ?? '—',
      },
      {
        title: t('license.auditLog.columns.reason'),
        dataIndex: 'reason',
        key: 'reason',
        ellipsis: true,
        render: (value: string | null | undefined) => value ?? '—',
      },
    ],
    [t]
  );

  if (!canAccess) {
    return null;
  }

  const onTableChange = (pagination: TablePaginationConfig) => {
    setPage(pagination.current ?? 1);
    setPageSize(pagination.pageSize ?? 10);
  };

  return (
    <Card title={t('license.auditLog.title')} style={{ marginTop: 16 }}>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('license.auditLog.subtitle')}
      </Typography.Paragraph>

      <Table<LicenseAuditLogItem>
        rowKey="id"
        size="small"
        loading={auditQuery.isLoading}
        columns={columns}
        dataSource={auditQuery.data?.items ?? []}
        onChange={onTableChange}
        pagination={{
          current: page,
          pageSize,
          total: auditQuery.data?.totalCount ?? 0,
          showSizeChanger: true,
          pageSizeOptions: [10, 20, 50],
        }}
        locale={{
          emptyText: (
            <Empty
              image={Empty.PRESENTED_IMAGE_SIMPLE}
              description={t('license.auditLog.empty')}
            />
          ),
        }}
      />
    </Card>
  );
}
