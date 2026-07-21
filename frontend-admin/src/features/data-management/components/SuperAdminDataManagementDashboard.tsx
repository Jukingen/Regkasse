'use client';

import { Alert, Card, Col, Row, Space, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo } from 'react';

import type { TenantDataManagementOverviewItem } from '@/features/data-management/api/adminDataManagement';
import { useDataManagementOverview } from '@/features/data-management/hooks/useDataManagementOverview';
import { formatDate, useI18n } from '@/i18n';

function lifecycleColor(state: string): string {
  switch (state) {
    case 'Active':
      return 'success';
    case 'Grace':
      return 'warning';
    case 'Locked':
      return 'orange';
    case 'Archived':
      return 'default';
    case 'ExportRequest':
      return 'processing';
    case 'Deleted':
      return 'error';
    default:
      return 'default';
  }
}

function lifecycleLabel(state: string, t: (key: string) => string): string {
  const key = `dataManagement.states.${state}`;
  const translated = t(key);
  return translated === key ? state : translated;
}

export function SuperAdminDataManagementDashboard() {
  const { t, formatLocale } = useI18n();
  const overviewQuery = useDataManagementOverview();
  const overview = overviewQuery.data;

  const columns: ColumnsType<TenantDataManagementOverviewItem> = useMemo(
    () => [
      {
        title: t('dataManagement.admin.colTenant'),
        key: 'tenant',
        render: (_, row) => (
          <Space orientation="vertical" size={0}>
            <Link href={`/tenant/${row.tenantId}/data-management`}>{row.tenantName}</Link>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {row.tenantSlug}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: t('dataManagement.lifecycle'),
        dataIndex: 'lifecycleState',
        key: 'lifecycle',
        width: 160,
        render: (state: string) => (
          <Tag color={lifecycleColor(state)}>{lifecycleLabel(state, t)}</Tag>
        ),
      },
      {
        title: t('dataManagement.admin.colGrace'),
        key: 'grace',
        width: 140,
        render: (_, row) =>
          row.isInGracePeriod ? (
            <Tag color="warning">
              {t('dataManagement.admin.graceDays', {
                days: row.gracePeriodRemainingDays,
              })}
            </Tag>
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          ),
      },
      {
        title: t('dataManagement.admin.colLock'),
        key: 'lock',
        width: 120,
        render: (_, row) => {
          if (row.isArchived) {
            return <Tag>{t('dataManagement.states.Archived')}</Tag>;
          }
          if (row.isLocked) {
            return <Tag color="orange">{t('dataManagement.states.Locked')}</Tag>;
          }
          return <Typography.Text type="secondary">—</Typography.Text>;
        },
      },
      {
        title: t('dataManagement.admin.colDeletion'),
        key: 'deletion',
        width: 160,
        render: (_, row) =>
          row.hasPendingDeletionRequest ? (
            <Tag color="processing">
              {row.deletionRequestStatus ?? t('dataManagement.admin.pending')}
            </Tag>
          ) : row.customerDataPurgedAtUtc ? (
            <Tag color="error">{t('dataManagement.states.Deleted')}</Tag>
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          ),
      },
      {
        title: t('dataManagement.admin.colRksvUntil'),
        key: 'rksv',
        width: 160,
        render: (_, row) =>
          row.rksvRetentionUntil
            ? formatDate(row.rksvRetentionUntil, formatLocale)
            : t('dataManagement.admin.noFiscalData'),
      },
      {
        title: t('dataManagement.admin.colPayments'),
        dataIndex: 'rksvPaymentCount',
        key: 'payments',
        width: 100,
      },
      {
        title: t('dataManagement.admin.colActions'),
        key: 'actions',
        width: 120,
        render: (_, row) => (
          <Link href={`/tenant/${row.tenantId}/data-management`}>
            {t('dataManagement.openAction')}
          </Link>
        ),
      },
    ],
    [t, formatLocale]
  );

  if (overviewQuery.isError) {
    return <Alert type="error" title={t('dataManagement.loadFailed')} />;
  }

  return (
    <Space orientation="vertical" size="large" style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        title={t('dataManagement.rksvTitle')}
        description={t('dataManagement.rksvNote')}
      />

      <Row gutter={[16, 16]}>
        <Col xs={12} sm={6}>
          <Card loading={overviewQuery.isLoading}>
            <Statistic
              title={t('dataManagement.admin.statTenants')}
              value={overview?.totalTenants ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card loading={overviewQuery.isLoading}>
            <Statistic
              title={t('dataManagement.admin.statGrace')}
              value={overview?.inGraceCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card loading={overviewQuery.isLoading}>
            <Statistic
              title={t('dataManagement.admin.statLocked')}
              value={overview?.lockedCount ?? 0}
            />
          </Card>
        </Col>
        <Col xs={12} sm={6}>
          <Card loading={overviewQuery.isLoading}>
            <Statistic
              title={t('dataManagement.admin.statDeletion')}
              value={overview?.pendingDeletionRequestCount ?? 0}
            />
          </Card>
        </Col>
      </Row>

      <Card title={t('dataManagement.admin.tableTitle')} loading={overviewQuery.isLoading}>
        <Table
          rowKey="tenantId"
          size="middle"
          columns={columns}
          dataSource={overview?.items ?? []}
          pagination={{ pageSize: 25, showSizeChanger: true }}
        />
      </Card>
    </Space>
  );
}
