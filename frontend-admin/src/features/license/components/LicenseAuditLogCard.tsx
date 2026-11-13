'use client';

import { DownloadOutlined } from '@ant-design/icons';
import { Button, Card, DatePicker, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import React, { useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import {
  type LicenseAuditLogItem,
  downloadLicenseAuditLogCsv,
} from '@/api/manual/adminLicense';
import { useBillingAccess } from '@/features/billing/hooks/useBillingAccess';
import { useLicenseAuditLog } from '@/features/license/hooks/useLicenseAuditLog';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n';

dayjs.extend(utc);

const AUDIT_ACTIONS = [
  'SALE_CREATED',
  'SALE_CANCELLED',
  'SALE_REFUNDED',
  'LICENSE_ACTIVATED',
  'LICENSE_EXTENDED',
  'LICENSE_RENEWED',
  'LICENSE_UPDATED',
  'LICENSE_REMINDER_SENT',
  'LICENSE_RENEWAL_PAGE_VIEWED',
] as const;

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
      return 'red';
    case 'Locked':
    case 'Archived':
      return 'default';
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
  const notify = useNotify();
  const canAccess = useBillingAccess();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [action, setAction] = useState<string | undefined>();
  const [userSearch, setUserSearch] = useState('');
  const [range, setRange] = useState<[Dayjs, Dayjs] | null>(null);
  const [exporting, setExporting] = useState(false);

  const fromUtc = range?.[0]?.utc().startOf('day').toISOString();
  const toUtc = range?.[1]?.utc().endOf('day').toISOString();

  const auditQuery = useLicenseAuditLog(
    {
      page,
      pageSize,
      action,
      userSearch: userSearch.trim() || undefined,
      fromUtc,
      toUtc,
    },
    canAccess
  );

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
        render: (rowAction: string) => {
          const key = `license.auditLog.actions.${rowAction}`;
          const label = t(key);
          return (
            <Tag color={actionTagColor(rowAction)}>{label === key ? rowAction : label}</Tag>
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

  const handleExport = async () => {
    setExporting(true);
    try {
      await downloadLicenseAuditLogCsv({
        action,
        userSearch: userSearch.trim() || undefined,
        fromUtc,
        toUtc,
      });
    } catch (err) {
      notify.apiError(err, {
        logContext: 'LicenseAuditLogCard.export',
        fallbackKey: 'license.auditLog.empty',
      });
    } finally {
      setExporting(false);
    }
  };

  return (
    <Card
      title={t('license.auditLog.title')}
      style={{ marginTop: 16 }}
      extra={
        <Button
          icon={<DownloadOutlined />}
          loading={exporting}
          onClick={() => void handleExport()}
        >
          {t('license.auditLog.exportCsv')}
        </Button>
      }
    >
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
        {t('license.auditLog.subtitle')}
      </Typography.Paragraph>

      <Space wrap style={{ marginBottom: 12 }}>
        <Select
          allowClear
          placeholder={t('license.auditLog.filters.actionAll')}
          aria-label={t('license.auditLog.filters.action')}
          style={{ minWidth: 220 }}
          value={action}
          onChange={(value) => {
            setAction(value);
            setPage(1);
          }}
          options={AUDIT_ACTIONS.map((item) => ({
            value: item,
            label: t(`license.auditLog.actions.${item}`),
          }))}
        />
        <Input.Search
          allowClear
          placeholder={t('license.auditLog.filters.userPlaceholder')}
          aria-label={t('license.auditLog.filters.user')}
          style={{ width: 240 }}
          onSearch={(value) => {
            setUserSearch(value);
            setPage(1);
          }}
        />
        <DatePicker.RangePicker
          placeholder={[
            t('license.auditLog.filters.dateRange'),
            t('license.auditLog.filters.dateRange'),
          ]}
          onChange={(dates) => {
            setRange(dates && dates[0] && dates[1] ? [dates[0], dates[1]] : null);
            setPage(1);
          }}
        />
      </Space>

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
