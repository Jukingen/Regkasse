'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Card, Select, Space, Switch, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';

import {
  fetchFeatureFlags,
  setFeatureFlag,
  type FeatureFlagStatusDto,
} from '@/api/manual/featureFlags';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { buildTenantSelectorLabel } from '@/features/super-admin/utils/tenantSelectorLabel';
import { useNotify } from '@/hooks/useNotify';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

import { describeFeatureFlag, isReservedFeatureFlag } from './flagDescription';

const QUERY_KEY = ['admin', 'feature-flags'] as const;

export function FeatureFlagsScreen() {
  const { t } = useI18n();
  const notify = useNotify();
  const { isSuperAdmin, hasPermission } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const queryClient = useQueryClient();
  const [overrideTenantId, setOverrideTenantId] = useState<string | undefined>();

  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'deploymentSystem', {
    title: t('nav.featureFlags'),
  });

  const listQuery = useQuery({
    queryKey: QUERY_KEY,
    queryFn: ({ signal }) => fetchFeatureFlags(undefined, signal),
    enabled: canView,
  });

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', 'feature-flags'],
    queryFn: () => listAdminTenants(false),
    enabled: canView,
  });

  const tenantName = useMemo(() => {
    const map = new Map<string, string>();
    for (const tenant of tenantsQuery.data ?? []) {
      map.set(tenant.id, tenant.name);
    }
    return map;
  }, [tenantsQuery.data]);

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
        width: 240,
        render: (name: string) => <Typography.Text code>{name}</Typography.Text>,
      },
      {
        title: t('featureFlags.columns.description'),
        key: 'description',
        render: (_, row) => describeFeatureFlag(t, row.name),
      },
      {
        title: t('featureFlags.columns.value'),
        key: 'enabled',
        width: 140,
        render: (_, row) => {
          const reserved = isReservedFeatureFlag(row.source, row.name);
          return (
            <Switch
              checked={row.enabled}
              disabled={reserved}
              loading={mutation.isPending}
              aria-label={t('featureFlags.globalSwitch', { name: row.name })}
              onChange={(checked) => {
                mutation.mutate({
                  name: row.name,
                  enabled: checked,
                  tenantId: null,
                });
              }}
            />
          );
        },
      },
      {
        title: t('featureFlags.columns.tenantOverrides'),
        key: 'tenantOverrides',
        render: (_, row) => {
          const overrides = row.tenantOverrides ?? [];
          const reserved = isReservedFeatureFlag(row.source, row.name);
          const selected = overrides.find((item) => item.tenantId === overrideTenantId);
          return (
            <Space orientation="vertical" size={4}>
              {overrides.length === 0 ? (
                <Typography.Text type="secondary">{t('featureFlags.noOverrides')}</Typography.Text>
              ) : (
                <Space size={[4, 4]} wrap>
                  {overrides.map((item) => (
                    <Tag key={item.tenantId} color={item.enabled ? 'green' : 'default'}>
                      {tenantName.get(item.tenantId) ?? item.tenantId}:{' '}
                      {item.enabled ? t('featureFlags.on') : t('featureFlags.off')}
                    </Tag>
                  ))}
                </Space>
              )}
              <Space>
                <Switch
                  checked={selected?.enabled ?? false}
                  disabled={!overrideTenantId || reserved}
                  aria-label={t('featureFlags.tenantSwitch', { name: row.name })}
                  onChange={(checked) => {
                    if (!overrideTenantId) return;
                    mutation.mutate({
                      name: row.name,
                      enabled: checked,
                      tenantId: overrideTenantId,
                    });
                  }}
                />
                {selected ? (
                  <Typography.Link
                    onClick={() => {
                      if (!overrideTenantId) return;
                      mutation.mutate({
                        name: row.name,
                        enabled: row.configDefault,
                        tenantId: overrideTenantId,
                        clearOverride: true,
                      });
                    }}
                  >
                    {t('featureFlags.clearOverride')}
                  </Typography.Link>
                ) : null}
              </Space>
            </Space>
          );
        },
      },
    ],
    [mutation, overrideTenantId, t, tenantName],
  );

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('featureFlags.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="warning" showIcon title={t('featureFlags.accessDenied')} />
      </AdminPageShell>
    );
  }

  const tenantOptions = (tenantsQuery.data ?? [])
    .filter((tenant) => tenant.isActive)
    .map((tenant) => ({
      value: tenant.id,
      label: buildTenantSelectorLabel(tenant, t),
    }));

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
        <Card size="small" title={t('featureFlags.overrideForTenant')}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 320 }}
            placeholder={t('featureFlags.overridePlaceholder')}
            value={overrideTenantId}
            options={tenantOptions}
            data-testid="feature-flags-tenant-select"
            onChange={(value) => setOverrideTenantId(value)}
          />
        </Card>
        <Table<FeatureFlagStatusDto>
          rowKey="name"
          data-testid="feature-flags-table"
          loading={listQuery.isLoading}
          columns={columns}
          dataSource={listQuery.data ?? []}
          pagination={false}
        />
      </Space>
    </AdminPageShell>
  );
}
