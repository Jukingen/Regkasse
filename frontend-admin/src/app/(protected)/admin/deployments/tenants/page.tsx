'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo } from 'react';

import {
  fetchDeploymentOverallStatus,
  requestTenantDeploymentRollback,
  type TenantDeploymentHistoryDto,
} from '@/api/manual/deployments';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'deployments', 'tenants'] as const;

function statusColor(status: string): string {
  switch (status) {
    case 'succeeded':
    case 'promoted':
      return 'success';
    case 'failed':
      return 'error';
    case 'rolled_back':
      return 'warning';
    case 'canary_soak':
    case 'deploying':
      return 'processing';
    default:
      return 'default';
  }
}

export default function DeploymentTenantsPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.administration'), href: '/admin' },
    { title: t('nav.deployments'), href: '/admin/deployments' },
    { title: t('nav.deploymentTenants') },
  ];

  const statusQuery = useQuery({
    queryKey: QUERY_KEY,
    queryFn: ({ signal }) => fetchDeploymentOverallStatus(signal),
    enabled: canView,
    refetchInterval: 30_000,
  });

  const rollbackMutation = useMutation({
    mutationFn: ({
      tenantId,
      previousVersion,
    }: {
      tenantId: string;
      previousVersion?: string | null;
    }) =>
      requestTenantDeploymentRollback(tenantId, {
        confirm: 'rollback',
        previousVersion: previousVersion ?? undefined,
      }),
    onSuccess: async (res) => {
      notify.success(res.message || t('deployments.tenants.rollback.success'));
      await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'Deployments.tenantRollback',
        fallbackKey: 'deployments.tenants.rollback.failed',
      });
    },
  });

  const confirmRollback = (row: TenantDeploymentHistoryDto) => {
    modal.confirm({
      title: t('deployments.tenants.rollback.confirmTitle'),
      content: (
        <Space orientation="vertical">
          <Typography.Text>
            {t('deployments.tenants.rollback.confirmBody', {
              tenant: row.tenantSlug || row.tenantName || row.tenantId,
            })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {row.previousVersion
              ? t('deployments.tenants.rollback.previousVersion', {
                  version: row.previousVersion,
                })
              : t('deployments.tenants.rollback.noPrevious')}
          </Typography.Text>
        </Space>
      ),
      okText: t('deployments.tenants.rollback.confirmOk'),
      okButtonProps: { danger: true },
      onOk: () =>
        rollbackMutation.mutateAsync({
          tenantId: row.tenantId,
          previousVersion: row.previousVersion,
        }),
    });
  };

  const columns: ColumnsType<TenantDeploymentHistoryDto> = useMemo(
    () => [
      {
        title: t('deployments.tenants.columns.tenant'),
        key: 'tenant',
        render: (_, row) => (
          <Space orientation="vertical" size={0}>
            <Typography.Text strong>{row.tenantSlug || row.tenantId}</Typography.Text>
            {row.tenantName ? (
              <Typography.Text type="secondary">{row.tenantName}</Typography.Text>
            ) : null}
          </Space>
        ),
      },
      {
        title: t('deployments.tenants.columns.version'),
        dataIndex: 'version',
        ellipsis: true,
      },
      {
        title: t('deployments.tenants.columns.stage'),
        dataIndex: 'stage',
        render: (stage: string) => <Tag>{stage}</Tag>,
      },
      {
        title: t('deployments.tenants.columns.status'),
        dataIndex: 'status',
        render: (status: string, row) => (
          <Space>
            <Tag color={statusColor(status)}>
              {t(`deployments.tenants.status.${status}`)}
            </Tag>
            {row.isCanarySoaking ? (
              <Tag color="orange">{t('deployments.tenants.soaking')}</Tag>
            ) : null}
          </Space>
        ),
      },
      {
        title: t('deployments.tenants.columns.deployed'),
        dataIndex: 'deployedAtUtc',
        render: (v: string) => new Date(v).toLocaleString(),
      },
      {
        title: t('deployments.tenants.columns.soakUntil'),
        dataIndex: 'soakUntilUtc',
        render: (v?: string | null) => (v ? new Date(v).toLocaleString() : '—'),
      },
      {
        title: t('deployments.tenants.columns.actions'),
        key: 'actions',
        render: (_, row) => (
          <Button
            danger
            size="small"
            disabled={!row.previousVersion || rollbackMutation.isPending}
            onClick={() => confirmRollback(row)}
          >
            {t('deployments.tenants.rollback.button')}
          </Button>
        ),
      },
    ],
    [t, rollbackMutation.isPending],
  );

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('deployments.tenants.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('deployments.accessDenied')} />
      </AdminPageShell>
    );
  }

  const overall = statusQuery.data;

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('deployments.tenants.pageTitle')}
        breadcrumbs={breadcrumbs}
        subtitle={t('deployments.tenants.introBody')}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('deployments.tenants.introTitle')}
        description={
          <Space orientation="vertical">
            <Typography.Text>{t('deployments.tenants.pipelineHint')}</Typography.Text>
            <Link href="/admin/deployments">{t('deployments.tenants.linkStageDashboard')}</Link>
          </Space>
        }
      />

      <Card size="small" style={{ marginBottom: 16 }}>
        <Space wrap size="large">
          <Typography.Text>
            {t('deployments.tenants.soakingCount')}: {overall?.canarySoakingCount ?? '—'}
          </Typography.Text>
          <Typography.Text>
            {t('deployments.tenants.failedCount')}: {overall?.failedCount ?? '—'}
          </Typography.Text>
          <Typography.Text>
            {t('deployments.tenants.nextCanary')}:{' '}
            {overall?.recommendedNextCanaryTenantSlug || t('deployments.tenants.nextCanaryNone')}
          </Typography.Text>
        </Space>
      </Card>

      <Table
        rowKey="id"
        loading={statusQuery.isLoading}
        columns={columns}
        dataSource={overall?.tenants ?? []}
        pagination={{ pageSize: 25 }}
      />
    </AdminPageShell>
  );
}
