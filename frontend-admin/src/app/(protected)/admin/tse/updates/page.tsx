'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Empty,
  List,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import { useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  applyTseUpdate,
  getTseUpdateHistory,
  getTseUpdateStatus,
} from '@/features/tse-updates/api/updates';
import type { TseAvailableUpdate, TseUpdateHistoryItem } from '@/features/tse-updates/types';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-updates'] as const;

function riskColor(risk: string): string {
  switch (risk) {
    case 'High':
      return 'red';
    case 'Medium':
      return 'orange';
    default:
      return 'green';
  }
}

function riskLabel(risk: string, t: (key: string) => string): string {
  switch (risk) {
    case 'High':
      return t('tseUpdates.riskHigh');
    case 'Medium':
      return t('tseUpdates.riskMedium');
    case 'Low':
      return t('tseUpdates.riskLow');
    default:
      return risk;
  }
}

function statusColor(status: string): string {
  switch (status) {
    case 'Succeeded':
      return 'success';
    case 'Blocked':
      return 'warning';
    case 'Failed':
      return 'error';
    default:
      return 'default';
  }
}

export default function TseUpdatesPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [tenantId, setTenantId] = useState<string | undefined>();

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'tse-updates'],
    queryFn: () => listAdminTenants(false),
    enabled: allowed,
    staleTime: 60_000,
  });

  const statusQuery = useQuery({
    queryKey: [...KEY, 'status', tenantId],
    queryFn: ({ signal }) => getTseUpdateStatus(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const historyQuery = useQuery({
    queryKey: [...KEY, 'history', tenantId],
    queryFn: ({ signal }) => getTseUpdateHistory(tenantId!, signal),
    enabled: allowed && !!tenantId,
  });

  const applyMutation = useMutation({
    mutationFn: (updateType: string) => applyTseUpdate(tenantId!, updateType),
    onSuccess: async (result) => {
      if (result.success) {
        notify.success(result.message || t('tseUpdates.applySuccess'));
      } else {
        notify.warning(result.message || t('tseUpdates.applyBlocked'), {
          mode: 'notification',
        });
      }
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseUpdates.apply',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const status = statusQuery.data;
  const hasUpdates = status?.hasUpdates ?? false;
  const updates = status?.availableUpdates ?? [];

  const historyColumns: ColumnsType<TseUpdateHistoryItem> = [
    {
      title: t('tseUpdates.colName'),
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: t('tseUpdates.colStatus'),
      dataIndex: 'status',
      key: 'status',
      width: 120,
      render: (v: string) => <Tag color={statusColor(v)}>{v}</Tag>,
    },
    {
      title: t('tseUpdates.colRisk'),
      dataIndex: 'riskLevel',
      key: 'riskLevel',
      width: 120,
      render: (v: string) => <Tag color={riskColor(v)}>{riskLabel(v, t)}</Tag>,
    },
    {
      title: t('tseUpdates.colVersions'),
      key: 'versions',
      render: (_, row) => `${row.fromVersion} → ${row.toVersion}`,
    },
    {
      title: t('tseUpdates.colDevices'),
      dataIndex: 'devicesTouched',
      key: 'devicesTouched',
      width: 90,
    },
    {
      title: t('tseUpdates.colWhen'),
      dataIndex: 'startedAt',
      key: 'startedAt',
      width: 170,
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm'),
    },
  ];

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseUpdates.forbidden')} />;
  }

  return (
    <>
      <AdminPageHeader
        title={t('tseUpdates.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseUpdates.title') }]}
        extra={
          <Select
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 260 }}
            placeholder={t('tseUpdates.tenantLabel')}
            loading={tenantsQuery.isLoading}
            value={tenantId}
            onChange={setTenantId}
            options={(tenantsQuery.data ?? []).map((tenant) => ({
              value: tenant.id,
              label: `${tenant.name} (${tenant.slug})`,
            }))}
          />
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('tseUpdates.subtitle')}
        </Typography.Paragraph>
      </AdminPageHeader>

      {!tenantId ? (
        <Alert type="info" showIcon message={t('tseUpdates.emptySelect')} />
      ) : statusQuery.isError ? (
        <Alert type="error" showIcon message={t('tseUpdates.loadError')} />
      ) : (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Card title={t('tseUpdates.cardTitle')} loading={statusQuery.isLoading}>
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
              message={t('tseUpdates.diagnosticNote')}
            />

            <Alert
              type={hasUpdates ? 'info' : 'success'}
              showIcon
              message={hasUpdates ? t('tseUpdates.updatesAvailable') : t('tseUpdates.upToDate')}
              description={
                hasUpdates ? t('tseUpdates.updatesAvailableDesc') : t('tseUpdates.upToDateDesc')
              }
              style={{ marginBottom: 16 }}
            />

            <Alert
              type={status?.zeroDowntimeCapable ? 'success' : 'warning'}
              showIcon
              style={{ marginBottom: 16 }}
              message={
                status?.zeroDowntimeCapable
                  ? t('tseUpdates.zeroDowntimeOk')
                  : t('tseUpdates.zeroDowntimeWarn')
              }
            />

            {hasUpdates ? (
              <List
                dataSource={updates}
                renderItem={(update: TseAvailableUpdate) => (
                  <List.Item
                    actions={[
                      <Tag key="risk" color={riskColor(update.risk)}>
                        {riskLabel(update.risk, t)}
                      </Tag>,
                      <Button
                        key="apply"
                        type="primary"
                        size="small"
                        loading={applyMutation.isPending}
                        onClick={() => applyMutation.mutate(update.updateType)}
                      >
                        {t('tseUpdates.apply')}
                      </Button>,
                    ]}
                  >
                    <List.Item.Meta
                      title={update.name}
                      description={
                        <>
                          <div>{update.description}</div>
                          <Typography.Text type="secondary">
                            {t('tseUpdates.version')}: {update.currentVersion} →{' '}
                            {update.targetVersion}
                          </Typography.Text>
                        </>
                      }
                    />
                  </List.Item>
                )}
              />
            ) : null}
          </Card>

          <Card title={t('tseUpdates.historyTitle')} loading={historyQuery.isLoading}>
            {(historyQuery.data?.items ?? []).length === 0 ? (
              <Empty description={t('tseUpdates.noHistory')} />
            ) : (
              <Table
                size="small"
                rowKey={(row) => row.id}
                columns={historyColumns}
                dataSource={historyQuery.data?.items ?? []}
                pagination={{ pageSize: 10 }}
              />
            )}
          </Card>
        </Space>
      )}
    </>
  );
}
