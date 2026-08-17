'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Col, Row, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import Link from 'next/link';
import { useMemo, useState } from 'react';

import {
  fetchDeployments,
  requestDeploymentRollback,
  type DeploymentRunDto,
  type DeploymentStage,
} from '@/api/manual/deployments';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'deployments'] as const;

const STAGES: Array<DeploymentStage | 'all'> = ['all', 'staging', 'canary', 'production'];

function statusColor(status: string): string {
  switch (status) {
    case 'succeeded':
      return 'success';
    case 'failed':
      return 'error';
    case 'rolled_back':
      return 'warning';
    case 'deploying':
    case 'smoke_running':
      return 'processing';
    default:
      return 'default';
  }
}

function stageColor(stage: string): string {
  switch (stage) {
    case 'staging':
      return 'gold';
    case 'canary':
      return 'orange';
    case 'production':
      return 'green';
    default:
      return 'default';
  }
}

function smokeTag(passed: boolean | null | undefined, t: (k: string) => string) {
  if (passed === true) return <Tag color="success">{t('deployments.smoke.passed')}</Tag>;
  if (passed === false) return <Tag color="error">{t('deployments.smoke.failed')}</Tag>;
  return <Tag>{t('deployments.smoke.unknown')}</Tag>;
}

export default function DeploymentsAdminPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { modal } = useAntdApp();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();
  const [stageFilter, setStageFilter] = useState<DeploymentStage | 'all'>('all');

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'deploymentSystem', {
    title: t('nav.deployments'),
  });

  const listQuery = useQuery({
    queryKey: [...QUERY_KEY, stageFilter],
    queryFn: ({ signal }) =>
      fetchDeployments(
        {
          stage: stageFilter === 'all' ? undefined : stageFilter,
          take: 50,
        },
        signal,
      ),
    enabled: canView,
    refetchInterval: 30_000,
  });

  const rollbackMutation = useMutation({
    mutationFn: requestDeploymentRollback,
    onSuccess: async (res) => {
      notify.success(res.message || t('deployments.rollback.success'));
      await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'Deployments.rollback',
        fallbackKey: 'deployments.rollback.failed',
      });
    },
  });

  const latest = listQuery.data?.latestByStage ?? {};

  const confirmRollback = (stage: DeploymentStage, row: DeploymentRunDto | null | undefined) => {
    const previous = row?.previousImageTag;
    modal.confirm({
      title: t('deployments.rollback.confirmTitle'),
      content: (
        <Space orientation="vertical">
          <Typography.Text>
            {t('deployments.rollback.confirmBody', { stage })}
          </Typography.Text>
          <Typography.Text type="secondary">
            {previous
              ? t('deployments.rollback.previousImage', { image: previous })
              : t('deployments.rollback.noPrevious')}
          </Typography.Text>
        </Space>
      ),
      okText: t('deployments.rollback.confirmOk'),
      okButtonProps: { danger: true },
      onOk: () =>
        rollbackMutation.mutateAsync({
          stage,
          confirm: 'rollback',
          previousImageTag: previous || undefined,
        }),
    });
  };

  const columns: ColumnsType<DeploymentRunDto> = useMemo(
    () => [
      {
        title: t('deployments.columns.stage'),
        dataIndex: 'stage',
        key: 'stage',
        width: 110,
        render: (stage: string) => <Tag color={stageColor(stage)}>{stage}</Tag>,
      },
      {
        title: t('deployments.columns.status'),
        dataIndex: 'status',
        key: 'status',
        width: 130,
        render: (status: string) => (
          <Tag color={statusColor(status)}>{t(`deployments.status.${status}`)}</Tag>
        ),
      },
      {
        title: t('deployments.columns.smoke'),
        key: 'smoke',
        width: 110,
        render: (_, row) => smokeTag(row.smokePassed, t),
      },
      {
        title: t('deployments.columns.image'),
        dataIndex: 'imageTag',
        key: 'imageTag',
        ellipsis: true,
        render: (v?: string | null) =>
          v ? <Typography.Text code>{v}</Typography.Text> : '—',
      },
      {
        title: t('deployments.columns.git'),
        key: 'git',
        width: 150,
        render: (_, row) => (
          <Typography.Text type="secondary">
            {(row.gitRef || '—') + (row.gitSha ? ` @ ${row.gitSha.slice(0, 7)}` : '')}
          </Typography.Text>
        ),
      },
      {
        title: t('deployments.columns.tenants'),
        dataIndex: 'tenantIds',
        key: 'tenantIds',
        width: 160,
        render: (ids: string[]) =>
          ids?.length ? ids.map((id) => <Tag key={id}>{id}</Tag>) : '—',
      },
      {
        title: t('deployments.columns.updated'),
        dataIndex: 'updatedAtUtc',
        key: 'updatedAtUtc',
        width: 170,
        render: (v: string) => new Date(v).toLocaleString(),
      },
      {
        title: t('deployments.columns.run'),
        key: 'runUrl',
        width: 90,
        render: (_, row) =>
          row.runUrl ? (
            <Typography.Link href={row.runUrl} target="_blank" rel="noreferrer">
              {t('deployments.openRun')}
            </Typography.Link>
          ) : (
            '—'
          ),
      },
    ],
    [t],
  );

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('deployments.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('deployments.accessDenied')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('deployments.pageTitle')}
        breadcrumbs={breadcrumbs}
        subtitle={t('deployments.introBody')}
      />

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        title={t('deployments.introTitle')}
        description={
          <Space orientation="vertical">
            <Typography.Text>{t('deployments.pipelineHint')}</Typography.Text>
            <Link href="/admin/deployments/tenants">{t('nav.deploymentTenants')}</Link>
            <Link href="/admin/deployments/go-live">{t('nav.deploymentGoLive')}</Link>
            <Link href="/admin/deployments/compliance">{t('nav.deploymentCompliance')}</Link>
          </Space>
        }
      />

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        {(['staging', 'canary', 'production'] as const).map((stage) => {
          const row = latest[stage];
          return (
            <Col xs={24} md={8} key={stage}>
              <Card
                size="small"
                title={<Tag color={stageColor(stage)}>{stage}</Tag>}
                extra={
                  <Button
                    size="small"
                    danger
                    loading={rollbackMutation.isPending}
                    disabled={!row}
                    onClick={() => confirmRollback(stage, row)}
                  >
                    {t('deployments.rollback.button')}
                  </Button>
                }
              >
                {row ? (
                  <Space orientation="vertical" size={4}>
                    <Tag color={statusColor(row.status)}>{row.status}</Tag>
                    {smokeTag(row.smokePassed, t)}
                    <Typography.Text type="secondary" ellipsis>
                      {row.imageTag || '—'}
                    </Typography.Text>
                    {row.smokeSummary ? (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {row.smokeSummary}
                      </Typography.Text>
                    ) : null}
                    <Typography.Text type="secondary">
                      {new Date(row.updatedAtUtc).toLocaleString()}
                    </Typography.Text>
                  </Space>
                ) : (
                  <Typography.Text type="secondary">{t('deployments.noRuns')}</Typography.Text>
                )}
              </Card>
            </Col>
          );
        })}
      </Row>

      <Space style={{ marginBottom: 12 }}>
        <Typography.Text>{t('deployments.filterStage')}</Typography.Text>
        <Select
          value={stageFilter}
          style={{ width: 160 }}
          options={STAGES.map((s) => ({
            value: s,
            label: s === 'all' ? t('deployments.allStages') : s,
          }))}
          onChange={(v) => setStageFilter(v)}
        />
      </Space>

      <Table<DeploymentRunDto>
        rowKey="id"
        loading={listQuery.isLoading}
        columns={columns}
        dataSource={listQuery.data?.items ?? []}
        pagination={{ pageSize: 20, total: listQuery.data?.total }}
        expandable={{
          expandedRowRender: (row) => (
            <Space orientation="vertical">
              {row.smokeSummary ? (
                <Typography.Text>
                  {t('deployments.smoke.summary')}: {row.smokeSummary}
                </Typography.Text>
              ) : null}
              {row.errorMessage ? (
                <Typography.Text type="danger">{row.errorMessage}</Typography.Text>
              ) : (
                <Typography.Text type="secondary">{t('deployments.noError')}</Typography.Text>
              )}
            </Space>
          ),
        }}
      />
    </AdminPageShell>
  );
}
