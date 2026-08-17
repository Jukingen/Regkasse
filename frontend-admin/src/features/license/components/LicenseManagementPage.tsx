'use client';

import { EyeOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Space, Table, Tabs, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';

import { LicenseGenerationCard } from '@/app/(protected)/admin/license/LicenseGenerationCard';
import {
  getIssuedLicensesList,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useBillingSalesList } from '@/features/billing/hooks';
import { DEFAULT_BILLING_SALES_FILTERS } from '@/features/billing/utils/billingSalesFilters';
import { UnifiedLicenseActivateCard } from '@/features/license/components/UnifiedLicenseActivateCard';
import { UnifiedLicenseDetailDrawer } from '@/features/license/components/UnifiedLicenseDetailDrawer';
import { TenantLicenseSection } from '@/features/license/components/TenantLicenseSection';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { resolveLicensePageSectionVisibility } from '@/features/license/utils/licensePageVisibility';
import {
  type UnifiedLicenseKind,
  type UnifiedLicenseRow,
  mergeUnifiedLicenseRows,
} from '@/features/license/utils/unifiedLicenseRows';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

type KindTab = 'all' | UnifiedLicenseKind;

export function LicenseManagementPage() {
  const { t } = useI18n();
  const router = useRouter();
  const { user } = useAuth();
  const currentTenant = useCurrentTenant();
  const [kindTab, setKindTab] = useState<KindTab>('all');
  const [search, setSearch] = useState('');
  const [detailKey, setDetailKey] = useState<string | null>(null);

  const visibility = resolveLicensePageSectionVisibility(user, {
    hasTenantContext: Boolean(
      currentTenant.isRealTenantSlug &&
        (currentTenant.tenantId || currentTenant.isTenantRecordLoading)
    ),
    role: user?.role,
    isSuperAdminPlatformMode: currentTenant.isSuperAdminPlatformMode,
  });

  const canGenerate = hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
  const canSeeBillingSales = hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const showCombinedList = visibility.showAllTenantLicensesSection || visibility.showDeploymentSection;

  const issuedQuery = useQuery({
    queryKey: licenseQueryKeys.list({ pageNumber: 1, pageSize: 50 }),
    queryFn: () => getIssuedLicensesList({ pageNumber: 1, pageSize: 50 }),
    enabled: showCombinedList,
  });

  const salesQuery = useBillingSalesList({
    ...DEFAULT_BILLING_SALES_FILTERS,
    pageSize: 50,
  });

  const rows = useMemo(
    () =>
      mergeUnifiedLicenseRows({
        issued: issuedQuery.data?.items ?? [],
        sales: salesQuery.data?.items ?? [],
        kindFilter: kindTab,
        search,
      }),
    [issuedQuery.data?.items, salesQuery.data?.items, kindTab, search]
  );

  const columns: ColumnsType<UnifiedLicenseRow> = [
    {
      title: t('license.management.kind'),
      dataIndex: 'kind',
      width: 120,
      render: (kind: UnifiedLicenseKind) => (
        <Tag color={kind === 'server' ? 'geekblue' : 'purple'}>
          {kind === 'server'
            ? t('license.management.kindServer')
            : t('license.management.kindTenant')}
        </Tag>
      ),
    },
    {
      title: t('license.mandant.licenseKey'),
      dataIndex: 'licenseKey',
      ellipsis: true,
      render: (key: string) => <Typography.Text code>{key}</Typography.Text>,
    },
    {
      title: t('license.management.customer'),
      dataIndex: 'displayName',
      ellipsis: true,
    },
    {
      title: t('license.extendModal.validUntilLabel'),
      dataIndex: 'validUntilUtc',
      width: 180,
      render: (value: string | null) => formatLicenseValidUntil(value),
    },
    {
      title: t('license.mandant.status'),
      dataIndex: 'status',
      width: 120,
      render: (status: string) => <Tag>{status}</Tag>,
    },
    {
      title: t('license.management.actions'),
      key: 'actions',
      width: 120,
      render: (_, row) => (
        <Button
          type="link"
          icon={<EyeOutlined />}
          onClick={() => setDetailKey(row.licenseKey)}
        >
          {t('license.management.viewDetails')}
        </Button>
      ),
    },
  ];

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('license.management.title')}
        subtitle={t('license.management.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.licenseManagement') },
        ]}
        actions={
          <Space wrap>
            {canSeeBillingSales ? (
              <>
                <Button onClick={() => router.push('/admin/billing/sales')}>
                  {t('license.management.openSales')}
                </Button>
                <Button onClick={() => router.push('/admin/billing/subscription-invoices')}>
                  {t('nav.subscriptionInvoices')}
                </Button>
                <Button
                  icon={<PlusOutlined />}
                  onClick={() => router.push('/admin/billing/sales/new')}
                >
                  {t('license.tenant.newSale')}
                </Button>
              </>
            ) : null}
            <Button
              icon={<ReloadOutlined />}
              onClick={() => {
                void issuedQuery.refetch();
                void salesQuery.refetch();
              }}
            >
              {t('common.buttons.refresh')}
            </Button>
          </Space>
        }
      />

      <UnifiedLicenseActivateCard tenantId={currentTenant.tenantId} />

      {visibility.showTenantLicenseSection ? <TenantLicenseSection /> : null}

      {showCombinedList ? (
        <Card variant="borderless" title={t('license.management.listTitle')}>
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Tabs
              activeKey={kindTab}
              onChange={(key) => setKindTab(key as KindTab)}
              items={[
                { key: 'all', label: t('license.management.tabAll') },
                { key: 'server', label: t('license.management.kindServer') },
                { key: 'tenant', label: t('license.management.kindTenant') },
              ]}
            />
            <Input.Search
              allowClear
              placeholder={t('license.management.searchPlaceholder')}
              onSearch={setSearch}
              onChange={(event) => {
                if (!event.target.value) setSearch('');
              }}
              style={{ maxWidth: 360 }}
            />
            <Table<UnifiedLicenseRow>
              rowKey="id"
              size="small"
              columns={columns}
              dataSource={rows}
              loading={issuedQuery.isFetching || salesQuery.isFetching}
              pagination={{ pageSize: 20 }}
              locale={{ emptyText: t('license.tenant.noResults') }}
            />
          </Space>
        </Card>
      ) : null}

      {canGenerate ? <LicenseGenerationCard canGenerate machineFingerprint="" /> : null}

      <UnifiedLicenseDetailDrawer licenseKey={detailKey} onClose={() => setDetailKey(null)} />
    </AdminPageShell>
  );
}
