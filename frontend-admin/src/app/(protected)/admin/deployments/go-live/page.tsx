'use client';

import { ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';

import {
  fetchGoLiveStatus,
  type GoLiveCheckDto,
  type GoLiveStatusDto,
} from '@/api/manual/deployments';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'deployments', 'go-live'] as const;

function useGoLiveStatus(enabled: boolean) {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: ({ signal }) => fetchGoLiveStatus(true, signal),
    enabled,
  });
}

function StatusBadge({
  status,
  t,
}: {
  status: GoLiveStatusDto['status'] | undefined;
  t: (key: string) => string;
}) {
  if (status === 'GO') {
    return (
      <Tag color="success" style={{ fontSize: 16, padding: '4px 12px' }}>
        {t('deployments.goLive.statusGo')}
      </Tag>
    );
  }
  if (status === 'NO-GO') {
    return (
      <Tag color="error" style={{ fontSize: 16, padding: '4px 12px' }}>
        {t('deployments.goLive.statusNoGo')}
      </Tag>
    );
  }
  return <Tag>{t('deployments.goLive.statusUnknown')}</Tag>;
}

function checkNameLabel(name: string, t: (key: string) => string): string {
  switch (name) {
    case 'Fiskaly':
      return t('deployments.goLive.checks.fiskaly');
    case 'Configuration':
      return t('deployments.goLive.checks.configuration');
    case 'FON':
      return t('deployments.goLive.checks.fon');
    case 'Backup':
      return t('deployments.goLive.checks.backup');
    case 'Monitoring':
      return t('deployments.goLive.checks.monitoring');
    case 'Sign-off':
      return t('deployments.goLive.checks.signOff');
    default:
      return name;
  }
}

export default function GoLivePage() {
  const { t } = useI18n();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const query = useGoLiveStatus(canView);

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'deploymentSystem', [
    { title: t('nav.deployments'), href: '/admin/deployments' },
    { title: t('nav.deploymentGoLive') },
  ]);

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('deployments.goLive.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('deployments.goLive.accessDenied')} />
      </AdminPageShell>
    );
  }

  const data = query.data;
  const isNoGo = data?.status === 'NO-GO';
  const failedCount = data?.checks.filter((c) => !c.passed).length ?? 0;

  const columns: ColumnsType<GoLiveCheckDto> = [
    {
      title: t('deployments.goLive.columns.name'),
      dataIndex: 'name',
      key: 'name',
      render: (name: string) => checkNameLabel(name, t),
    },
    {
      title: t('deployments.goLive.columns.category'),
      dataIndex: 'category',
      key: 'category',
      width: 140,
    },
    {
      title: t('deployments.goLive.columns.passed'),
      dataIndex: 'passed',
      key: 'passed',
      width: 120,
      render: (passed: boolean) =>
        passed ? (
          <Tag color="success">{t('deployments.goLive.passed')}</Tag>
        ) : (
          <Tag color="error">{t('deployments.goLive.failed')}</Tag>
        ),
    },
    {
      title: t('deployments.goLive.columns.details'),
      dataIndex: 'details',
      key: 'details',
      ellipsis: true,
    },
    {
      title: t('deployments.goLive.columns.remediation'),
      dataIndex: 'remediation',
      key: 'remediation',
      ellipsis: true,
      render: (value: string) => value || '—',
    },
  ];

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('deployments.goLive.pageTitle')}
        breadcrumbs={breadcrumbs}
        subtitle={t('deployments.goLive.introBody')}
        extra={
          <Button
            icon={<ReloadOutlined />}
            loading={query.isFetching}
            onClick={() => {
              void query.refetch();
            }}
          >
            {t('deployments.goLive.recheck')}
          </Button>
        }
      />

      {query.isError ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          title={t('deployments.goLive.loadFailed')}
        />
      ) : null}

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space align="center" wrap>
            <Typography.Text strong>{t('deployments.goLive.verdict')}</Typography.Text>
            <StatusBadge status={data?.status} t={t} />
            {data?.checkedAtUtc ? (
              <Typography.Text type="secondary">
                {t('deployments.goLive.checkedAt', { when: data.checkedAtUtc })}
              </Typography.Text>
            ) : null}
          </Space>

          {isNoGo ? (
            <Alert
              type="error"
              showIcon
              title={t('deployments.goLive.notReady')}
              description={data?.summary ?? t('deployments.goLive.missingCount', { count: failedCount })}
            />
          ) : null}

          {data?.status === 'GO' ? (
            <Alert type="success" showIcon title={t('deployments.goLive.ready')} description={data.summary} />
          ) : null}

          <Typography.Text type="secondary">{t('deployments.goLive.humanNote')}</Typography.Text>
          <Space wrap>
            <Link href="/admin/deployments">{t('deployments.goLive.linkDashboard')}</Link>
            <Link href="/admin/deployments/compliance">{t('nav.deploymentCompliance')}</Link>
          </Space>
        </Space>
      </Card>

      <Table<GoLiveCheckDto>
        rowKey={(row) => `${row.category}-${row.name}`}
        columns={columns}
        dataSource={data?.checks ?? []}
        loading={query.isLoading}
        pagination={false}
        locale={{ emptyText: t('deployments.goLive.noChecks') }}
      />
    </AdminPageShell>
  );
}
