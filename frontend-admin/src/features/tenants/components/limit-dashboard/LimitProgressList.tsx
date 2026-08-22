'use client';

import { ArrowDownOutlined, ArrowUpOutlined, MinusOutlined } from '@ant-design/icons';
import { Card, Progress, Segmented, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';

import type { LimitHealthStatus, LimitStatusDto } from '@/features/tenants/api/tenantLimits';
import { LimitCard } from '@/features/tenants/components/limit-dashboard/LimitCard';
import {
  healthProgressStatus,
  healthStatusI18nKey,
  healthTagColor,
  limitDashboardDetailHref,
  trendI18nKey,
  type LimitStatusFilter,
} from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { useI18n } from '@/i18n';

function TrendMark({ trend }: { trend: string }) {
  if (trend === 'Increasing') return <ArrowUpOutlined style={{ color: '#cf1322' }} />;
  if (trend === 'Decreasing') return <ArrowDownOutlined style={{ color: '#389e0d' }} />;
  return <MinusOutlined style={{ color: '#8c8c8c' }} />;
}

export function LimitProgressList({
  limits,
  loading,
  showTenant,
  isSuperAdmin,
  filter,
  onFilterChange,
  onOpenDetail,
  registerLabel,
}: {
  limits: LimitStatusDto[];
  loading?: boolean;
  showTenant: boolean;
  isSuperAdmin: boolean;
  filter: LimitStatusFilter;
  onFilterChange: (value: LimitStatusFilter) => void;
  onOpenDetail: (href: string) => void;
  registerLabel?: string | null;
}) {
  const { t } = useI18n();

  const filtered = useMemo(
    () => (filter === 'all' ? limits : limits.filter((row) => row.status === filter)),
    [filter, limits]
  );

  const columns: ColumnsType<LimitStatusDto> = [
    ...(showTenant
      ? [
          {
            title: t('tenants.limits.dashboard.tenant'),
            dataIndex: 'tenantName',
            key: 'tenantName',
            render: (name: string | null | undefined, row: LimitStatusDto) => name || row.tenantId,
          },
        ]
      : []),
    {
      title: t('tenants.limits.dashboard.limit'),
      dataIndex: 'key',
      key: 'key',
      render: (_: string, row: LimitStatusDto) => (
        <LimitCard
          limit={row}
          tenantSlug={row.tenantSlug || row.tenantName}
          registerLabel={row.key === 'maxActiveRegistersPerUser' ? registerLabel : null}
        />
      ),
    },
    {
      title: t('tenants.limits.dashboard.usage'),
      key: 'usage',
      width: 280,
      render: (_: unknown, row: LimitStatusDto) => (
        <Progress
          percent={Math.min(100, Math.max(0, Number(row.percentage) || 0))}
          status={healthProgressStatus(row.status)}
          format={() => `${row.current} / ${row.limit}`}
        />
      ),
    },
    {
      title: t('tenants.limits.dashboard.users.status'),
      dataIndex: 'status',
      key: 'status',
      width: 140,
      render: (status: LimitHealthStatus) => (
        <Tag color={healthTagColor(status)}>{t(healthStatusI18nKey(status))}</Tag>
      ),
    },
    {
      title: t('tenants.limits.dashboard.limits.trend'),
      key: 'trend',
      width: 160,
      render: (_: unknown, row: LimitStatusDto) => (
        <Space>
          <TrendMark trend={row.trend} />
          <span>{t(trendI18nKey(row.trend))}</span>
          <Typography.Text type="secondary">
            {row.changeCount > 0 ? '+' : ''}
            {row.changeCount} {row.changeUnit}
          </Typography.Text>
        </Space>
      ),
    },
  ];

  return (
    <Card
      variant="borderless"
      title={t('tenants.limits.dashboard.limits.title')}
      extra={
        <Segmented
          value={filter}
          onChange={(value) => onFilterChange(value as LimitStatusFilter)}
          options={[
            { label: t('tenants.limits.dashboard.filter.all'), value: 'all' },
            { label: t('tenants.limits.dashboard.filter.healthy'), value: 'Healthy' },
            { label: t('tenants.limits.dashboard.filter.warning'), value: 'Warning' },
            { label: t('tenants.limits.dashboard.filter.critical'), value: 'Critical' },
          ]}
        />
      }
      loading={loading}
    >
      <Table
        rowKey={(row) => `${row.tenantId}-${row.key}`}
        columns={columns}
        dataSource={filtered}
        pagination={false}
        onRow={(row) => ({
          style: { cursor: 'pointer' },
          onClick: () => onOpenDetail(limitDashboardDetailHref(row.key, row.tenantId, isSuperAdmin)),
        })}
        locale={{ emptyText: t('tenants.limits.dashboard.emptyLimits') }}
      />
    </Card>
  );
}
