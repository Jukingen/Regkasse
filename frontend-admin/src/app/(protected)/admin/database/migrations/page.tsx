'use client';

import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Col, Row, Statistic, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo } from 'react';

import {
  fetchDatabaseMigrations,
  type MigrationEntryDto,
} from '@/api/manual/databaseMigrations';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'database', 'migrations'] as const;

function statusColor(status: string): string {
  switch (status) {
    case 'Healthy':
      return 'success';
    case 'Degraded':
      return 'warning';
    case 'Unhealthy':
      return 'error';
    default:
      return 'default';
  }
}

export default function DatabaseMigrationsAdminPage() {
  const { t } = useI18n();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.administration'), href: '/admin' },
    { title: t('nav.databaseMigrations') },
  ];

  const query = useQuery({
    queryKey: QUERY_KEY,
    queryFn: ({ signal }) => fetchDatabaseMigrations(50, signal),
    enabled: canView,
    refetchInterval: 30_000,
  });

  const appliedColumns: ColumnsType<MigrationEntryDto> = useMemo(
    () => [
      {
        title: t('databaseMigrations.columns.id'),
        dataIndex: 'id',
        key: 'id',
        render: (id: string) => <Typography.Text code>{id}</Typography.Text>,
      },
    ],
    [t],
  );

  const pendingColumns: ColumnsType<{ id: string }> = useMemo(
    () => [
      {
        title: t('databaseMigrations.columns.id'),
        dataIndex: 'id',
        key: 'id',
        render: (id: string) => <Typography.Text code type="danger">{id}</Typography.Text>,
      },
    ],
    [t],
  );

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('databaseMigrations.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('databaseMigrations.accessDenied')} />
      </AdminPageShell>
    );
  }

  const data = query.data;

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('databaseMigrations.pageTitle')}
        breadcrumbs={breadcrumbs}
        subtitle={t('databaseMigrations.introBody')}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('databaseMigrations.introTitle')}
        description={t('databaseMigrations.strategyHint')}
      />

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={6}>
          <Card size="small" title={t('databaseMigrations.status')}>
            <Tag color={statusColor(String(data?.status ?? ''))}>{data?.status ?? '—'}</Tag>
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card size="small">
            <Statistic title={t('databaseMigrations.appliedCount')} value={data?.appliedCount ?? 0} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card size="small">
            <Statistic
              title={t('databaseMigrations.pendingCount')}
              value={data?.pendingCount ?? 0}
              valueStyle={{ color: (data?.pendingCount ?? 0) > 0 ? '#cf1322' : undefined }}
            />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card size="small">
            <Typography.Text type="secondary">{t('databaseMigrations.latestApplied')}</Typography.Text>
            <div>
              <Typography.Text code ellipsis style={{ maxWidth: '100%' }}>
                {data?.latestApplied || '—'}
              </Typography.Text>
            </div>
          </Card>
        </Col>
      </Row>

      {(data?.pendingCount ?? 0) > 0 && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('databaseMigrations.pendingWarning')}
        />
      )}

      <Typography.Title level={4}>{t('databaseMigrations.pendingTitle')}</Typography.Title>
      <Table
        rowKey="id"
        loading={query.isLoading}
        columns={pendingColumns}
        dataSource={(data?.pending ?? []).map((id) => ({ id }))}
        pagination={false}
        locale={{ emptyText: t('databaseMigrations.noPending') }}
        style={{ marginBottom: 24 }}
      />

      <Typography.Title level={4}>{t('databaseMigrations.recentTitle')}</Typography.Title>
      <Table
        rowKey="id"
        loading={query.isLoading}
        columns={appliedColumns}
        dataSource={data?.recentApplied ?? []}
        pagination={{ pageSize: 20 }}
      />
    </AdminPageShell>
  );
}
