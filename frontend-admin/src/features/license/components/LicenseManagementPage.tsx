'use client';

import { DownloadOutlined, EyeOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Input, Space, Switch, Table, Tabs, Tag, Tooltip } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { useMemo, useState, type Key } from 'react';
import { useRouter } from 'next/navigation';

import { LicenseGenerationCard } from '@/app/(protected)/admin/license/LicenseGenerationCard';
import { LicenseReportsCard } from '@/app/(protected)/admin/license/LicenseReportsCard';
import type { LicenseSaleResponse } from '@/api/generated/model';
import {
  getIssuedLicensesList,
  licenseQueryKeys,
} from '@/api/manual/adminLicense';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { BillingSalesBulkBar } from '@/features/billing/components/BillingSalesBulkBar';
import {
  BillingSalesBulkConfirmModal,
  BillingSalesBulkProgressModal,
} from '@/features/billing/components/BillingSalesBulkModals';
import { useBillingSalesList } from '@/features/billing/hooks';
import { useLicenseSalesBulkActions } from '@/features/billing/hooks/useLicenseSalesBulkActions';
import { DEFAULT_BILLING_SALES_FILTERS } from '@/features/billing/utils/billingSalesFilters';
import { LicenseAutoRenewalCard } from '@/features/license/components/LicenseAutoRenewalCard';
import { LicenseBulkImportCard } from '@/features/license/components/LicenseBulkImportCard';
import { LicenseExtendModal } from '@/features/license/components/LicenseExtendModal';
import { LicenseKeyRevealText } from '@/features/license/components/LicenseKeyRevealText';
import { UnifiedLicenseActivateCard } from '@/features/license/components/UnifiedLicenseActivateCard';
import { UnifiedLicenseLayersCard } from '@/features/license/components/UnifiedLicenseLayersCard';
import { UnifiedLicenseDetailDrawer } from '@/features/license/components/UnifiedLicenseDetailDrawer';
import { UnifiedLicenseStatusBadge } from '@/features/license/components/UnifiedLicenseStatusBadge';
import { LicenseUsageAnalyticsCard } from '@/features/license/components/LicenseUsageAnalyticsCard';
import { TenantLicenseSection } from '@/features/license/components/TenantLicenseSection';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { resolveLicensePageSectionVisibility } from '@/features/license/utils/licensePageVisibility';
import { exportUnifiedLicensesCsv } from '@/features/license/utils/exportUnifiedLicensesCsv';
import {
  type UnifiedLicenseKind,
  type UnifiedLicenseRow,
  mergeUnifiedLicenseRows,
} from '@/features/license/utils/unifiedLicenseRows';
import { formatLicenseValidUntil } from '@/features/license/utils/licenseValidUntil';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import { useLicenseKeyReveal } from '@/features/license/hooks/useLicenseKeyReveal';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';

type KindTab = 'all' | UnifiedLicenseKind;

export function LicenseManagementPage() {
  const { t } = useI18n();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const currentTenant = useCurrentTenant();
  const [kindTab, setKindTab] = useState<KindTab>('all');
  const [search, setSearch] = useState('');
  const [detailKey, setDetailKey] = useState<string | null>(null);
  const [renewTenantId, setRenewTenantId] = useState<string | null>(null);
  const [selectedRowKeys, setSelectedRowKeys] = useState<Key[]>([]);
  const [selectedRows, setSelectedRows] = useState<UnifiedLicenseRow[]>([]);

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
  const superAdmin = isSuperAdmin(user?.role);
  const canRevealKeys =
    superAdmin || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
  const { showKeys, onShowKeysChange } = useLicenseKeyReveal(canRevealKeys);

  const issuedQuery = useQuery({
    queryKey: licenseQueryKeys.list({ pageNumber: 1, pageSize: 50 }),
    queryFn: () => getIssuedLicensesList({ pageNumber: 1, pageSize: 50 }),
    enabled: showCombinedList,
  });

  const salesQuery = useBillingSalesList({
    ...DEFAULT_BILLING_SALES_FILTERS,
    pageSize: 50,
  });

  const bulk = useLicenseSalesBulkActions();

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

  const salesByKey = useMemo(() => {
    const map = new Map<string, LicenseSaleResponse>();
    for (const sale of salesQuery.data?.items ?? []) {
      const key = sale.licenseKey?.trim().toUpperCase();
      if (key) map.set(key, sale);
    }
    return map;
  }, [salesQuery.data?.items]);

  const selectedSales = useMemo(
    () =>
      selectedRows
        .map((row) => salesByKey.get(row.licenseKey.trim().toUpperCase()))
        .filter((sale): sale is LicenseSaleResponse => sale != null),
    [selectedRows, salesByKey]
  );

  const refreshLists = () => {
    void issuedQuery.refetch();
    void salesQuery.refetch();
    void invalidateTenantLicenseQueries(queryClient, currentTenant.tenantId);
  };

  const columns: ColumnsType<UnifiedLicenseRow> = [
    {
      title: t('license.management.kind'),
      dataIndex: 'kind',
      width: 120,
      render: (kind: UnifiedLicenseKind) => (
        <Tag color={kind === 'system' ? 'geekblue' : 'purple'}>
          {kind === 'system'
            ? t('license.management.kindSystem')
            : t('license.management.kindTenant')}
        </Tag>
      ),
    },
    {
      title: t('license.mandant.licenseKey'),
      dataIndex: 'licenseKey',
      ellipsis: true,
      render: (key: string) => <LicenseKeyRevealText licenseKey={key} reveal={showKeys} />,
    },
    {
      title: t('license.management.slug'),
      dataIndex: 'slug',
      width: 140,
      render: (slug: string | null) => slug ?? '—',
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
      width: 160,
      render: (_status, row) => (
        <UnifiedLicenseStatusBadge
          status={row.status}
          validUntilUtc={row.validUntilUtc}
          showCountdown
        />
      ),
    },
    {
      title: t('license.management.actions'),
      key: 'actions',
      width: 220,
      render: (_, row) => (
        <Space wrap>
          <Button
            type="link"
            icon={<EyeOutlined />}
            onClick={() => setDetailKey(row.licenseKey)}
          >
            {t('license.management.viewDetails')}
          </Button>
          {row.tenantId ? (
            <Button type="link" onClick={() => setRenewTenantId(row.tenantId ?? null)}>
              {t('license.management.renewNow')}
            </Button>
          ) : null}
        </Space>
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
                <Button onClick={() => router.push('/admin/license/audit')}>
                  {t('license.management.openAudit')}
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
              onClick={() => refreshLists()}
            >
              {t('common.buttons.refresh')}
            </Button>
          </Space>
        }
      />

      <UnifiedLicenseLayersCard tenantId={currentTenant.tenantId} />

      <UnifiedLicenseActivateCard tenantId={currentTenant.tenantId} />

      {visibility.showTenantLicenseSection ? <TenantLicenseSection /> : null}

      {visibility.showTenantLicenseSection ? (
        <LicenseAutoRenewalCard
          validUntil={
            rows.find((row) => row.kind === 'tenant' && row.tenantId === currentTenant.tenantId)
              ?.validUntilUtc
          }
        />
      ) : null}

      {canSeeBillingSales ? (
        <LicenseBulkImportCard tenantId={currentTenant.tenantId} onActivated={refreshLists} />
      ) : null}

      {showCombinedList ? (
        <Card variant="borderless" title={t('license.management.listTitle')}>
          <Space orientation="vertical" size="middle" style={{ width: '100%' }}>
            <Tabs
              activeKey={kindTab}
              onChange={(key) => setKindTab(key as KindTab)}
              items={[
                { key: 'all', label: t('license.management.tabAll') },
                { key: 'system', label: t('license.management.kindSystem') },
                { key: 'tenant', label: t('license.management.kindTenant') },
              ]}
            />
            <Space wrap>
              <Input.Search
                allowClear
                placeholder={t('license.management.searchPlaceholder')}
                onSearch={setSearch}
                onChange={(event) => {
                  if (!event.target.value) setSearch('');
                }}
                style={{ maxWidth: 360 }}
              />
              {canRevealKeys ? (
                <Tooltip title={t('license.management.showKeysTooltip')}>
                  <Switch
                    checked={showKeys}
                    checkedChildren={t('license.management.hideKeys')}
                    unCheckedChildren={t('license.management.showKeys')}
                    onChange={onShowKeysChange}
                  />
                </Tooltip>
              ) : null}
              <Button
                icon={<DownloadOutlined />}
                onClick={() =>
                  exportUnifiedLicensesCsv(
                    rows,
                    {
                      kind: t('license.management.kind'),
                      licenseKey: t('license.mandant.licenseKey'),
                      slug: t('license.management.slug'),
                      displayName: t('license.management.customer'),
                      validUntil: t('license.extendModal.validUntilLabel'),
                      status: t('license.mandant.status'),
                      tenantId: t('license.management.slug'),
                    },
                    { maskLicenseKeys: !showKeys }
                  )
                }
              >
                {t('license.management.exportCsv')}
              </Button>
            </Space>
            {canSeeBillingSales ? (
              <BillingSalesBulkBar
                selectedCount={selectedSales.length}
                disabled={bulk.running}
                onAction={(action) => bulk.requestAction(action, selectedSales)}
              />
            ) : null}
            <Table<UnifiedLicenseRow>
              rowKey="id"
              size="small"
              columns={columns}
              dataSource={rows}
              loading={issuedQuery.isFetching || salesQuery.isFetching}
              pagination={{ pageSize: 20 }}
              locale={{ emptyText: t('license.tenant.noResults') }}
              rowSelection={
                canSeeBillingSales
                  ? {
                      selectedRowKeys,
                      onChange: (keys, selected) => {
                        setSelectedRowKeys(keys);
                        setSelectedRows(selected);
                      },
                    }
                  : undefined
              }
            />
          </Space>
        </Card>
      ) : null}

      {superAdmin ? <LicenseUsageAnalyticsCard /> : null}
      {superAdmin ? <LicenseReportsCard /> : null}

      {canGenerate ? <LicenseGenerationCard canGenerate machineFingerprint="" /> : null}

      <UnifiedLicenseDetailDrawer licenseKey={detailKey} onClose={() => setDetailKey(null)} />

      {renewTenantId ? (
        <LicenseExtendModal
          open
          tenantId={renewTenantId}
          status={null}
          resolvedStatus={null}
          onClose={() => setRenewTenantId(null)}
          onSuccess={() => {
            setRenewTenantId(null);
            refreshLists();
          }}
        />
      ) : null}

      <BillingSalesBulkConfirmModal
        open={bulk.confirmOpen}
        action={bulk.pendingAction}
        selectedCount={selectedSales.length}
        eligibleCount={bulk.eligibleCountForPending}
        loading={bulk.running}
        onCancel={bulk.closeConfirm}
        onConfirm={(reason) => void bulk.confirmPending(reason)}
      />
      <BillingSalesBulkProgressModal open={bulk.progressOpen} progress={bulk.progress} />
    </AdminPageShell>
  );
}
