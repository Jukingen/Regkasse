'use client';

import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  EditOutlined,
  ExportOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { Alert, Button, Card, Empty, Input, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { type ReactNode, useCallback, useMemo, useState } from 'react';

import { dateColumnRender } from '@/components/DateColumn';
import { adminTableScrollXy, shouldUseAdminTableVirtual } from '@/components/ui/adminTableVirtual';
import type { TenantLicenseOverviewItem } from '@/features/license/api/tenantLicenseOverview';
import { EditTenantLicenseModal } from '@/features/license/components/EditTenantLicenseModal';
import { useTenantLicenseOverview } from '@/features/license/hooks/useTenantLicenseOverview';
import {
  type MandantLicenseOverviewKind,
  mandantLicenseOverviewKindLabelKey,
  mandantLicenseOverviewTagColor,
} from '@/features/license/utils/mandantLicenseOverviewStatus';
import { maskTenantLicenseKey } from '@/features/license/utils/tenantLicenseExtend';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useDebounce } from '@/hooks/useDebounce';
import { formatGermanDateTime, useI18n } from '@/i18n';
import { formatExportFilenameDate } from '@/lib/dateUtils';

type LicenseFilterValue = 'all' | MandantLicenseOverviewKind;

type TenantLicenseRow = TenantLicenseOverviewItem & {
  searchBlob: string;
};

function toCsvCell(value: string): string {
  return `"${value.replace(/"/g, '""')}"`;
}

function downloadCsv(filename: string, content: string): void {
  if (typeof globalThis.window === 'undefined') {
    return;
  }

  const blob = new globalThis.Blob([`\uFEFF${content}`], {
    type: 'text/csv;charset=utf-8;',
  });
  const url = globalThis.URL.createObjectURL(blob);
  const link = globalThis.document.createElement('a');
  link.href = url;
  link.download = filename;
  globalThis.document.body.appendChild(link);
  link.click();
  link.remove();
  globalThis.URL.revokeObjectURL(url);
}

function statusIcon(kind: MandantLicenseOverviewKind): ReactNode {
  switch (kind) {
    case 'active':
      return <CheckCircleOutlined />;
    case 'expiring_soon':
      return <ClockCircleOutlined />;
    case 'expired':
      return <CloseCircleOutlined />;
    case 'trial':
      return <ClockCircleOutlined />;
    case 'none':
    default:
      return <CloseCircleOutlined />;
  }
}

export function TenantLicenseOverview() {
  const { message } = useAntdApp();
  const { t, formatLocale } = useI18n();
  const [searchInput, setSearchInput] = useState('');
  const debouncedSearch = useDebounce(searchInput, 300);
  const [statusFilter, setStatusFilter] = useState<LicenseFilterValue>('all');
  const [editTenant, setEditTenant] = useState<TenantLicenseOverviewItem | null>(null);
  const overviewQuery = useTenantLicenseOverview();

  const rows = useMemo<TenantLicenseRow[]>(() => {
    return (overviewQuery.data ?? []).map((tenant) => ({
      ...tenant,
      searchBlob: [tenant.tenantName, tenant.tenantSlug, tenant.licenseKey ?? '']
        .join(' ')
        .toLowerCase(),
    }));
  }, [overviewQuery.data]);

  const filteredRows = useMemo(() => {
    const normalizedSearch = debouncedSearch.trim().toLowerCase();

    return rows.filter((row) => {
      if (statusFilter !== 'all' && row.status !== statusFilter) {
        return false;
      }

      if (normalizedSearch && !row.searchBlob.includes(normalizedSearch)) {
        return false;
      }

      return true;
    });
  }, [debouncedSearch, rows, statusFilter]);

  const exportCsv = useCallback(() => {
    if (filteredRows.length === 0) {
      message.info(t('license.superAdmin.noRowsToExport'));
      return;
    }

    const header = [
      t('license.superAdmin.table.tenant'),
      t('license.superAdmin.table.slug'),
      t('license.superAdmin.table.licenseKey'),
      t('license.superAdmin.table.validUntil'),
      t('license.superAdmin.table.status'),
    ];

    const lines = filteredRows.map((row) => {
      return [
        row.tenantName,
        row.tenantSlug,
        row.licenseKey ? maskTenantLicenseKey(row.licenseKey) : '—',
        row.validUntilUtc ? formatGermanDateTime(row.validUntilUtc) : '—',
        t(mandantLicenseOverviewKindLabelKey(row.status)),
      ]
        .map((value) => toCsvCell(String(value)))
        .join(';');
    });

    downloadCsv(
      `mandantenlizenzen_${formatExportFilenameDate()}.csv`,
      [header.map((value) => toCsvCell(String(value))).join(';'), ...lines].join('\n')
    );

    message.success(t('license.superAdmin.exported', { count: filteredRows.length }));
  }, [filteredRows, formatLocale, message, t]);

  const columns = useMemo<ColumnsType<TenantLicenseRow>>(
    () => [
      {
        title: t('license.superAdmin.table.tenant'),
        dataIndex: 'tenantName',
        key: 'tenantName',
        width: 200,
        sorter: (a, b) => a.tenantName.localeCompare(b.tenantName, 'de'),
        render: (name: string) => <span style={{ fontWeight: 500 }}>{name}</span>,
      },
      {
        title: t('license.superAdmin.table.slug'),
        dataIndex: 'tenantSlug',
        key: 'tenantSlug',
        width: 140,
        sorter: (a, b) => a.tenantSlug.localeCompare(b.tenantSlug, 'de'),
        render: (slug: string) => (
          <Typography.Text type="secondary" code>
            {slug}
          </Typography.Text>
        ),
      },
      {
        title: t('license.superAdmin.table.licenseKey'),
        dataIndex: 'licenseKey',
        key: 'licenseKey',
        width: 200,
        render: (key: string | null) =>
          key?.trim() ? (
            <Typography.Text code style={{ fontSize: 12 }}>
              {maskTenantLicenseKey(key)}
            </Typography.Text>
          ) : (
            '—'
          ),
      },
      {
        title: t('license.superAdmin.table.validUntil'),
        dataIndex: 'validUntilUtc',
        key: 'validUntilUtc',
        width: 130,
        sorter: (a, b) => {
          const left = a.validUntilUtc ? dayjs(a.validUntilUtc).unix() : 0;
          const right = b.validUntilUtc ? dayjs(b.validUntilUtc).unix() : 0;
          return left - right;
        },
        render: dateColumnRender('datetime'),
      },
      {
        title: t('license.superAdmin.table.status'),
        dataIndex: 'status',
        key: 'status',
        width: 170,
        sorter: (a, b) => a.status.localeCompare(b.status, 'de'),
        render: (kind: MandantLicenseOverviewKind) => (
          <Tag color={mandantLicenseOverviewTagColor(kind)} icon={statusIcon(kind)}>
            {t(mandantLicenseOverviewKindLabelKey(kind))}
          </Tag>
        ),
      },
      {
        title: t('license.superAdmin.table.actions'),
        key: 'actions',
        width: 120,
        fixed: 'right',
        render: (_, row) => {
          const isAdd = row.status === 'none';
          return (
            <Button
              type={isAdd ? 'default' : 'link'}
              size="small"
              icon={isAdd ? <PlusOutlined /> : <EditOutlined />}
              onClick={() => setEditTenant(row)}
            >
              {isAdd ? t('license.superAdmin.table.add') : t('license.superAdmin.table.edit')}
            </Button>
          );
        },
      },
    ],
    [formatLocale, t]
  );

  return (
    <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
        <Input.Search
          allowClear
          value={searchInput}
          placeholder={t('license.superAdmin.searchPlaceholder')}
          style={{ width: 280 }}
          onSearch={setSearchInput}
          onChange={(event) => setSearchInput(event.target.value)}
        />

        <Select<LicenseFilterValue>
          placeholder={t('license.superAdmin.statusFilter')}
          style={{ width: 200 }}
          value={statusFilter}
          onChange={setStatusFilter}
          options={[
            { label: t('license.superAdmin.filters.all'), value: 'all' },
            { label: t('license.superAdmin.filters.active'), value: 'active' },
            { label: t('license.superAdmin.filters.expiresSoon'), value: 'expiring_soon' },
            { label: t('license.superAdmin.filters.expired'), value: 'expired' },
            { label: t('license.superAdmin.filters.noLicense'), value: 'none' },
            { label: t('license.superAdmin.filters.trial'), value: 'trial' },
          ]}
        />

        <Button icon={<ReloadOutlined />} onClick={() => void overviewQuery.refetch()}>
          {t('common.buttons.refresh')}
        </Button>

        <Button icon={<ExportOutlined />} onClick={exportCsv} disabled={filteredRows.length === 0}>
          {t('license.superAdmin.exportButton')}
        </Button>
      </div>

      {overviewQuery.isError ? (
        <Alert type="error" showIcon title={t('common.messages.unknownError')} />
      ) : null}

      <Card>
        <Table<TenantLicenseRow>
          rowKey="tenantId"
          loading={overviewQuery.isLoading}
          dataSource={filteredRows}
          columns={columns}
          locale={{
            emptyText: (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={t('license.superAdmin.table.noResults')}
              />
            ),
          }}
          virtual={shouldUseAdminTableVirtual(filteredRows.length)}
          scroll={adminTableScrollXy(960, filteredRows.length)}
          pagination={{
            pageSize: 20,
            showSizeChanger: true,
            pageSizeOptions: [10, 20, 50, 100],
          }}
        />
      </Card>

      <EditTenantLicenseModal
        open={editTenant !== null}
        tenant={editTenant}
        onClose={() => setEditTenant(null)}
      />
    </Space>
  );
}
