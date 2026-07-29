'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Card, Input, Space, Switch, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import {
  fetchFeatureFlags,
  setFeatureFlag,
  type FeatureFlagStatusDto,
} from '@/api/manual/featureFlags';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

const QUERY_KEY = ['admin', 'feature-flags'] as const;

function normalizeTenantId(raw: string | undefined): string | undefined {
  const v = raw?.trim();
  if (!v) return undefined;
  return /^[0-9a-fA-F-]{36}$/.test(v) ? v : undefined;
}

export default function FeatureFlagsAdminPage() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();
  const [tenantInput, setTenantInput] = useState('');
  const tenantId = normalizeTenantId(tenantInput);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.administration'), href: '/admin' },
    { title: t('nav.featureFlags') },
  ];

  const listQuery = useQuery({
    queryKey: [...QUERY_KEY, tenantId ?? 'global'],
    queryFn: ({ signal }) => fetchFeatureFlags(tenantId, signal),
    enabled: canView,
  });

  const mutation = useMutation({
    mutationFn: setFeatureFlag,
    onSuccess: async () => {
      notify.successKey('featureFlags.saved');
      await queryClient.invalidateQueries({ queryKey: QUERY_KEY });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'FeatureFlags.set',
        fallbackKey: 'featureFlags.saveFailed',
      });
    },
  });

  const columns: ColumnsType<FeatureFlagStatusDto> = useMemo(
    () => [
      {
        title: t('featureFlags.columns.name'),
        dataIndex: 'name',
        key: 'name',
        render: (name: string) => <Typography.Text code>{name}</Typography.Text>,
      },
      {
        title: t('featureFlags.columns.enabled'),
        key: 'enabled',
        width: 120,
        render: (_, row) => (
          <Switch
            checked={row.enabled}
            loading={mutation.isPending}
            onChange={(checked) => {
              mutation.mutate({
                name: row.name,
                enabled: checked,
                tenantId: tenantId ?? null,
              });
            }}
          />
        ),
      },
      {
        title: t('featureFlags.columns.configDefault'),
        dataIndex: 'configDefault',
        key: 'configDefault',
        width: 140,
        render: (v: boolean) =>
          v ? t('featureFlags.bool.true') : t('featureFlags.bool.false'),
      },
      {
        title: t('featureFlags.columns.source'),
        dataIndex: 'source',
        key: 'source',
        width: 180,
        render: (source: string) => {
          const color =
            source === 'tenant_override'
              ? 'orange'
              : source === 'global_override'
                ? 'gold'
                : 'default';
          return (
            <Tag color={color}>
              {source === 'tenant_override'
                ? t('featureFlags.source.tenant_override')
                : source === 'global_override'
                  ? t('featureFlags.source.global_override')
                  : t('featureFlags.source.config')}
            </Tag>
          );
        },
      },
      {
        title: t('featureFlags.columns.actions'),
        key: 'actions',
        width: 160,
        render: (_, row) =>
          row.overrideValue != null ? (
            <Typography.Link
              onClick={() => {
                mutation.mutate({
                  name: row.name,
                  enabled: row.configDefault,
                  tenantId: tenantId ?? null,
                  clearOverride: true,
                });
              }}
            >
              {t('featureFlags.clearOverride')}
            </Typography.Link>
          ) : (
            <Typography.Text type="secondary">—</Typography.Text>
          ),
      },
    ],
    [mutation, t, tenantId],
  );

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('featureFlags.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('featureFlags.accessDenied')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('featureFlags.pageTitle')} breadcrumbs={breadcrumbs} />
      <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
        <Alert
          type="info"
          showIcon
          title={t('featureFlags.introTitle')}
          description={t('featureFlags.introBody')}
        />
        <Card size="small" title={t('featureFlags.tenantScope')}>
          <Input
            allowClear
            placeholder={t('featureFlags.tenantIdInputPlaceholder')}
            value={tenantInput}
            onChange={(e) => setTenantInput(e.target.value)}
            status={tenantInput && !tenantId ? 'error' : undefined}
          />
          <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
            {t('featureFlags.tenantIdPasteHint')}
          </Typography.Paragraph>
        </Card>
        <Table<FeatureFlagStatusDto>
          rowKey="name"
          loading={listQuery.isLoading}
          columns={columns}
          dataSource={listQuery.data ?? []}
          pagination={false}
        />
      </Space>
    </AdminPageShell>
  );
}
