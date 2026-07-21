'use client';

import { BugOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Collapse,
  Descriptions,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { useMemo, useState } from 'react';

import {
  getDeploymentLicenseStatus,
  getLicenseStatus,
  getTenantLicensePublicStatus,
  licenseQueryKeys,
  tenantLicenseUnifiedQueryKeyFor,
} from '@/api/manual/adminLicense';
import { TableSkeleton } from '@/components/Skeleton';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  getMandantLicenseHistory,
  licenseHistoryQueryKeys,
} from '@/features/license/api/licenseHistory';
import { getTenantLicense, tenantLicenseQueryKeys } from '@/features/license/api/tenantLicense';
import { LicenseHistory } from '@/features/license/components/LicenseHistory';
import { useTenantLicenseStatus } from '@/features/license/hooks/useLicenseStatus';
import { resolveTenantLicenseStatus } from '@/features/license/utils/licenseStatus';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useHeaderTenantLicense } from '@/features/tenant/hooks/useHeaderTenantLicense';
import { useTenantLicense } from '@/hooks/useTenantLicense';
import { formatGermanDateTime, useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { NotFoundAccessView } from '@/shared/auth/NotFoundAccessView';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

function JsonPanel({ value }: { value: unknown }) {
  return (
    <Typography.Paragraph
      copyable
      code
      style={{ whiteSpace: 'pre-wrap', marginBottom: 0, maxHeight: 360, overflow: 'auto' }}
    >
      {JSON.stringify(value, null, 2)}
    </Typography.Paragraph>
  );
}

export default function LicenseDebugPage() {
  const { t, formatLocale } = useI18n();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const currentTenant = useCurrentTenant();
  const headerLicense = useHeaderTenantLicense();

  const isSuperAdmin = hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);

  const effectiveTenantId = selectedTenantId ?? currentTenant.tenantId ?? null;

  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants', false],
    queryFn: () => listAdminTenants(false),
    enabled: isSuperAdmin,
  });

  const unifiedQuery = useTenantLicense(effectiveTenantId ?? undefined);
  const resolvedStatusQuery = useTenantLicenseStatus(effectiveTenantId ?? undefined);

  const publicStatusQuery = useQuery({
    queryKey: [...tenantLicenseUnifiedQueryKeyFor(effectiveTenantId), 'debug-raw'],
    queryFn: () => getTenantLicensePublicStatus(effectiveTenantId),
    enabled: isSuperAdmin && Boolean(effectiveTenantId),
  });

  const adminDetailQuery = useQuery({
    queryKey: [...tenantLicenseQueryKeys.detail(effectiveTenantId ?? ''), 'debug'],
    queryFn: () => getTenantLicense(effectiveTenantId!),
    enabled: isSuperAdmin && Boolean(effectiveTenantId),
  });

  const deploymentQuery = useQuery({
    queryKey: licenseQueryKeys.deploymentStatus,
    queryFn: getDeploymentLicenseStatus,
    enabled: isSuperAdmin,
  });

  const adminStatusQuery = useQuery({
    queryKey: licenseQueryKeys.status,
    queryFn: getLicenseStatus,
    enabled: isSuperAdmin,
  });

  const historyQuery = useQuery({
    queryKey: licenseHistoryQueryKeys.detail(effectiveTenantId ?? ''),
    queryFn: () => getMandantLicenseHistory(effectiveTenantId!),
    enabled: isSuperAdmin && Boolean(effectiveTenantId),
  });

  const tenantOptions = useMemo(
    () =>
      (tenantsQuery.data ?? []).map((row) => ({
        value: row.id,
        label: `${row.name} (${row.slug})`,
      })),
    [tenantsQuery.data]
  );

  const comparisonRows = useMemo(() => {
    const unified = unifiedQuery.data;
    const detail = adminDetailQuery.data?.status;
    const resolved = resolvedStatusQuery.data;
    const header = headerLicense.resolvedStatus;

    return [
      {
        key: 'unified',
        source: t('license.debug.sources.unified'),
        endpoint: 'GET /api/license/status?tenantId=…',
        daysRemaining: unified?.daysRemaining ?? '—',
        validUntil: unified?.validUntil ?? '—',
        kind: resolved?.kind ?? '—',
        canAccess: unified?.canAccess == null ? '—' : unified.canAccess ? 'yes' : 'no',
      },
      {
        key: 'adminDetail',
        source: t('license.debug.sources.adminDetail'),
        endpoint: 'GET /api/admin/tenants/{id}/license',
        daysRemaining: detail ? resolveTenantLicenseStatus(detail).daysRemaining : '—',
        validUntil: detail?.validUntilUtc ?? '—',
        kind: detail ? resolveTenantLicenseStatus(detail).kind : '—',
        canAccess: '—',
      },
      {
        key: 'header',
        source: t('license.debug.sources.header'),
        endpoint: 'useHeaderTenantLicense → unified',
        daysRemaining: header?.daysRemaining ?? '—',
        validUntil: headerLicense.licenseValidUntilUtc ?? '—',
        kind: header?.kind ?? '—',
        canAccess: '—',
      },
    ];
  }, [
    adminDetailQuery.data,
    headerLicense.resolvedStatus,
    headerLicense.licenseValidUntilUtc,
    resolvedStatusQuery.data,
    t,
    unifiedQuery.data,
  ]);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.licenseManagement'), href: '/admin/license' },
    { title: t('license.debug.title'), href: '/admin/license/debug' },
  ];

  const refreshAll = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: tenantLicenseUnifiedQueryKeyFor(effectiveTenantId),
      }),
      queryClient.invalidateQueries({
        queryKey: tenantLicenseQueryKeys.detail(effectiveTenantId ?? ''),
      }),
      queryClient.invalidateQueries({
        queryKey: licenseHistoryQueryKeys.detail(effectiveTenantId ?? ''),
      }),
      queryClient.invalidateQueries({ queryKey: licenseQueryKeys.deploymentStatus }),
      queryClient.invalidateQueries({ queryKey: licenseQueryKeys.status }),
    ]);
  };

  if (!isSuperAdmin) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <AdminPageHeader title={t('license.debug.title')} breadcrumbs={breadcrumbs} />
        <NotFoundAccessView compact />
      </div>
    );
  }

  const isLoading =
    unifiedQuery.isLoading ||
    publicStatusQuery.isLoading ||
    adminDetailQuery.isLoading ||
    deploymentQuery.isLoading;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader
        title={t('license.debug.title')}
        breadcrumbs={breadcrumbs}
        actions={
          <Button icon={<ReloadOutlined />} onClick={() => void refreshAll()}>
            {t('license.debug.refresh')}
          </Button>
        }
      />

      <Alert
        type="info"
        showIcon
        icon={<BugOutlined />}
        message={t('license.debug.noticeTitle')}
        description={t('license.debug.noticeDescription')}
      />

      <Card>
        <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap>
            <Typography.Text strong>{t('license.debug.tenantContext')}</Typography.Text>
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder={t('license.debug.tenantPlaceholder')}
              style={{ minWidth: 320 }}
              loading={tenantsQuery.isLoading}
              options={tenantOptions}
              value={effectiveTenantId}
              onChange={(value) => setSelectedTenantId(value ?? null)}
            />
          </Space>
          <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
            <Descriptions.Item label={t('license.debug.currentSlug')}>
              {currentTenant.tenantSlug ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.debug.platformMode')}>
              {currentTenant.isSuperAdminPlatformMode ? 'yes' : 'no'}
            </Descriptions.Item>
            <Descriptions.Item label={t('license.debug.queryKey')}>
              <Typography.Text code>
                {JSON.stringify(tenantLicenseUnifiedQueryKeyFor(effectiveTenantId))}
              </Typography.Text>
            </Descriptions.Item>
            <Descriptions.Item label={t('license.debug.fetchedAt')}>
              {unifiedQuery.dataUpdatedAt
                ? formatGermanDateTime(new Date(unifiedQuery.dataUpdatedAt).toISOString())
                : '—'}
            </Descriptions.Item>
          </Descriptions>
        </Space>
      </Card>

      {isLoading && !unifiedQuery.data ? (
        <TableSkeleton rows={5} cols={6} />
      ) : (
        <>
          <Card title={t('license.debug.comparisonTitle')}>
            <Table
              size="small"
              pagination={false}
              rowKey="key"
              dataSource={comparisonRows}
              columns={[
                { title: t('license.debug.columns.source'), dataIndex: 'source', key: 'source' },
                {
                  title: t('license.debug.columns.endpoint'),
                  dataIndex: 'endpoint',
                  key: 'endpoint',
                  ellipsis: true,
                },
                {
                  title: t('license.debug.columns.kind'),
                  dataIndex: 'kind',
                  key: 'kind',
                  render: (v) => <Tag>{String(v)}</Tag>,
                },
                {
                  title: t('license.debug.columns.daysRemaining'),
                  dataIndex: 'daysRemaining',
                  key: 'daysRemaining',
                },
                {
                  title: t('license.debug.columns.validUntil'),
                  dataIndex: 'validUntil',
                  key: 'validUntil',
                  ellipsis: true,
                },
                {
                  title: t('license.debug.columns.canAccess'),
                  dataIndex: 'canAccess',
                  key: 'canAccess',
                },
              ]}
            />
          </Card>

          <Collapse
            items={[
              {
                key: 'unified',
                label: t('license.debug.sources.unified'),
                children: <JsonPanel value={publicStatusQuery.data ?? unifiedQuery.data} />,
              },
              {
                key: 'adminDetail',
                label: t('license.debug.sources.adminDetail'),
                children: <JsonPanel value={adminDetailQuery.data} />,
              },
              {
                key: 'resolved',
                label: t('license.debug.sources.resolved'),
                children: <JsonPanel value={resolvedStatusQuery.data} />,
              },
              {
                key: 'deployment',
                label: t('license.debug.sources.deployment'),
                children: <JsonPanel value={deploymentQuery.data} />,
              },
              {
                key: 'adminStatus',
                label: t('license.debug.sources.adminStatus'),
                children: <JsonPanel value={adminStatusQuery.data} />,
              },
              {
                key: 'historyMeta',
                label: t('license.debug.sources.history'),
                children: (
                  <JsonPanel
                    value={{
                      itemCount: historyQuery.data?.items?.length ?? 0,
                      items: historyQuery.data?.items ?? [],
                    }}
                  />
                ),
              },
            ]}
          />

          {effectiveTenantId ? <LicenseHistory tenantId={effectiveTenantId} /> : null}
        </>
      )}
    </div>
  );
}
