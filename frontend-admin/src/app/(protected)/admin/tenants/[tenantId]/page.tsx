'use client';

import {
  ArrowLeftOutlined,
  BgColorsOutlined,
  DeleteOutlined,
  EditOutlined,
  GlobalOutlined,
  ImportOutlined,
  LoginOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Space, Tabs, Tag, Typography } from 'antd';
import Link from 'next/link';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
/**
 * Super-admin tenant detail dashboard — overview, users, registers, license, settings.
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isDevelopment } from '@/features/auth/services/devTenant';
import {
  applyTenantImpersonationSession,
  getAdminTenantById,
  hardDeleteAdminTenantDevelopment,
  impersonateAdminTenant,
  restoreAdminTenant,
  updateAdminTenant,
} from '@/features/super-admin/api/adminTenants';
import { ImpersonationRedirectOverlay } from '@/features/super-admin/components/ImpersonationRedirectOverlay';
import { TenantDetailCashRegistersTab } from '@/features/super-admin/components/TenantDetailCashRegistersTab';
import { TenantDetailLicenseTab } from '@/features/super-admin/components/TenantDetailLicenseTab';
import { TenantDetailOverviewTab } from '@/features/super-admin/components/TenantDetailOverviewTab';
import { TenantDetailSettingsTab } from '@/features/super-admin/components/TenantDetailSettingsTab';
import {
  TENANT_DETAIL_LEGACY_USERS_TAB,
  TENANT_DETAIL_TAB_KEYS,
  type TenantDetailTabKey,
  parseTenantDetailTab,
} from '@/features/super-admin/components/TenantDetailTabs';
import {
  TENANT_DETAIL_QUERY_KEY,
  invalidateTenantLifecycleQueries,
} from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';
import { buildTenantDeletePreparationHref } from '@/features/super-admin/utils/tenantDeleteDependencyUi';
import { tenantStatusColor } from '@/features/super-admin/utils/tenantStatusLabel';
import { DemoImportModal } from '@/features/tenants/components/DemoImportModal';
import { buildAdminUsersPageHref } from '@/features/users/utils/adminUsersPageUrl';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useKeyboardShortcutListener } from '@/hooks/useKeyboardShortcutListener';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS, hasPermission } from '@/shared/auth/permissions';
import { KEYBOARD_SHORTCUT_EVENTS, type NavigateTabDetail } from '@/shared/keyboardShortcuts';

export default function SuperAdminTenantDetailPage() {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';
  const activeTab = parseTenantDetailTab(searchParams.get('tab'));
  const displayTab: TenantDetailTabKey =
    activeTab === TENANT_DETAIL_LEGACY_USERS_TAB ? 'overview' : activeTab;
  const [impersonationRedirecting, setImpersonationRedirecting] = useState(false);
  const [demoImportOpen, setDemoImportOpen] = useState(false);

  const canAccess = isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

  const tenantQuery = useQuery({
    queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId],
    queryFn: () => getAdminTenantById(tenantId),
    enabled: canAccess && !!tenantId,
  });

  const invalidateTenant = useCallback(() => {
    invalidateTenantLifecycleQueries(queryClient, tenantId);
  }, [queryClient, tenantId]);

  const refetchProducts = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['products', tenantId] });
    void queryClient.invalidateQueries({ queryKey: ['categories', tenantId] });
  }, [queryClient, tenantId]);

  const statusMutation = useMutation({
    mutationFn: (status: string) => updateAdminTenant(tenantId, { status }),
    onSuccess: () => {
      message.success(t('tenants.messages.updated'));
      invalidateTenant();
    },
    onError: () => message.error(t('tenants.messages.saveFailed')),
  });

  const restoreMutation = useMutation({
    mutationFn: () => restoreAdminTenant(tenantId),
    onSuccess: () => {
      message.success(t('tenants.messages.restored'));
      invalidateTenant();
    },
    onError: () => message.error(t('tenants.messages.restoreFailed')),
  });

  const developmentHardDeleteMutation = useMutation({
    mutationFn: () => hardDeleteAdminTenantDevelopment(tenantId),
    onSuccess: () => {
      message.success(t('tenants.messages.hardDeleted'));
      router.push('/admin/tenants');
    },
    onError: (err: unknown) => {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
          : null;
      message.error(msg ?? t('tenants.messages.hardDeleteFailed'));
    },
  });

  const impersonateMutation = useMutation({
    mutationFn: () => impersonateAdminTenant(tenantId),
    onSuccess: (res) => {
      setImpersonationRedirecting(true);
      applyTenantImpersonationSession(res);
    },
    onError: () => message.error(t('tenants.messages.impersonationFailed')),
  });

  const setTab = useCallback(
    (tab: string) => {
      router.replace(`/admin/tenants/${tenantId}?tab=${tab}`);
    },
    [router, tenantId]
  );

  const onNavigateTab = useCallback(
    (detail: NavigateTabDetail | undefined) => {
      if (detail?.index == null) return;
      const tabKey = TENANT_DETAIL_TAB_KEYS[detail.index];
      if (tabKey) setTab(tabKey);
    },
    [setTab]
  );
  useKeyboardShortcutListener(
    KEYBOARD_SHORTCUT_EVENTS.navigateTab,
    onNavigateTab,
    canAccess && !!tenantId
  );

  const closeDemoImport = useCallback(() => setDemoImportOpen(false), []);
  useKeyboardShortcutListener(KEYBOARD_SHORTCUT_EVENTS.closeModal, closeDemoImport, demoImportOpen);

  useEffect(() => {
    if (activeTab === TENANT_DETAIL_LEGACY_USERS_TAB && tenantId) {
      router.replace(buildAdminUsersPageHref(tenantId));
    }
  }, [activeTab, tenantId, router]);

  const tabItems = useMemo(() => {
    const tenant = tenantQuery.data;
    if (!tenant) return [];

    return [
      {
        key: 'overview',
        label: t('tenants.detail.tabs.overview'),
        children: (
          <TenantDetailOverviewTab
            tenant={tenant}
            suspendPending={statusMutation.isPending}
            onSuspend={() => statusMutation.mutate('suspended')}
            onReactivate={() => statusMutation.mutate('active')}
          />
        ),
      },
      {
        key: 'registers',
        label: t('tenants.detail.tabs.registers'),
        children: (
          <TenantDetailCashRegistersTab
            tenantId={tenantId}
            onImpersonate={() => impersonateMutation.mutate()}
            impersonatePending={impersonateMutation.isPending}
          />
        ),
      },
      {
        key: 'license',
        label: t('tenants.detail.tabs.license'),
        children: <TenantDetailLicenseTab tenant={tenant} onUpdated={invalidateTenant} />,
      },
      {
        key: 'settings',
        label: t('tenants.detail.tabs.settings'),
        children: (
          <TenantDetailSettingsTab
            tenant={tenant}
            onUpdated={invalidateTenant}
            saveShortcutEnabled={displayTab === 'settings'}
            restorePending={restoreMutation.isPending}
            developmentHardDeletePending={developmentHardDeleteMutation.isPending}
            onArchiveSuccess={invalidateTenant}
            onPermanentDeleteSuccess={() => router.push('/admin/tenants')}
            onRestore={() => restoreMutation.mutateAsync()}
            onDevelopmentHardDelete={() => developmentHardDeleteMutation.mutateAsync()}
          />
        ),
      },
    ];
  }, [
    t,
    tenantQuery.data,
    tenantId,
    statusMutation.isPending,
    restoreMutation,
    developmentHardDeleteMutation,
    impersonateMutation.isPending,
    invalidateTenant,
    router,
    displayTab,
  ]);

  if (!canAccess) {
    return (
      <AdminPageShell>
        <Alert
          type="error"
          title={t('tenants.accessDenied.title')}
          description={t('tenants.accessDenied.body')}
        />
      </AdminPageShell>
    );
  }

  if (!tenantId) {
    return (
      <AdminPageShell>
        <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
      </AdminPageShell>
    );
  }

  const tenant = tenantQuery.data;
  const title = tenant ? `${tenant.name} (${tenant.slug})` : tenantId;
  const showDevelopmentHardDeleteEntry = isDevelopment() && tenant?.status !== 'deleted';

  return (
    <AdminPageShell>
      {impersonationRedirecting ? <ImpersonationRedirectOverlay /> : null}
      <AdminPageHeader
        title={title}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
          { title: t('tenants.page.title'), href: '/admin/tenants' },
          { title, href: `/admin/tenants/${tenantId}` },
        ]}
        actions={
          <Space wrap>
            <Link href="/admin/tenants">
              <Button icon={<ArrowLeftOutlined />}>{t('tenants.users.actions.back')}</Button>
            </Link>
            <Button icon={<ReloadOutlined />} onClick={() => invalidateTenant()}>
              {t('common.refresh')}
            </Button>
            {tenant ? (
              <Link href={buildTenantDeletePreparationHref(tenantId)}>
                <Button>{t('tenants.deleteDependencies.checkDependencies')}</Button>
              </Link>
            ) : null}
            {tenant && tenant.status !== 'deleted' ? (
              <>
                <Button icon={<ImportOutlined />} onClick={() => setDemoImportOpen(true)}>
                  Demo Produkte importieren
                </Button>
                <Link href={buildAdminUsersPageHref(tenantId)}>
                  <Button>{t('tenants.detail.overview.manageUsers')}</Button>
                </Link>
                <Link href={`/tenant/${tenantId}/digital`}>
                  <Button icon={<GlobalOutlined />}>
                    {t('tenants.digitalServices.openAction')}
                  </Button>
                </Link>
                <Link href={`/tenant/${tenantId}/domain`}>
                  <Button icon={<GlobalOutlined />}>
                    {t('tenants.domainManagement.openAction')}
                  </Button>
                </Link>
                <Link href={`/tenant/${tenantId}/customize`}>
                  <Button icon={<BgColorsOutlined />}>
                    {t('tenants.customization.openAction')}
                  </Button>
                </Link>
                <Link href={`/tenant/${tenantId}/data-management`}>
                  <Button>{t('dataManagement.openAction')}</Button>
                </Link>
                <Link href={`/admin/tenants/${tenantId}?tab=settings`}>
                  <Button icon={<EditOutlined />}>{t('tenants.actions.edit')}</Button>
                </Link>
                {showDevelopmentHardDeleteEntry ? (
                  <Link href={`/admin/tenants/${tenantId}?tab=settings#danger-zone`}>
                    <Button danger size="small" icon={<DeleteOutlined />}>
                      {t('tenants.actions.developmentHardDelete')}
                    </Button>
                  </Link>
                ) : null}
                <Button
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => router.push(`/admin/tenants/${tenant.id}/decommission`)}
                >
                  {t('tenants.actions.decommission')}
                </Button>
                <Button
                  icon={<LoginOutlined />}
                  loading={impersonateMutation.isPending}
                  onClick={() => impersonateMutation.mutate()}
                >
                  {t('tenants.actions.impersonate')}
                </Button>
              </>
            ) : null}
          </Space>
        }
      />

      {tenant ? (
        <Space style={{ marginBottom: 16 }}>
          <Tag color={tenantStatusColor(tenant.status)}>{tenant.status}</Tag>
          {tenant.isDemoPreset ? <Tag color="purple">{t('tenants.detail.demoPreset')}</Tag> : null}
        </Space>
      ) : null}

      {tenantQuery.isError ? (
        <Alert
          type="error"
          title={t('tenants.users.errors.tenantNotFound')}
          style={{ marginBottom: 16 }}
        />
      ) : null}

      <Card loading={tenantQuery.isLoading && !tenant}>
        <Tabs activeKey={displayTab} onChange={setTab} items={tabItems} />
      </Card>

      {!tenantQuery.isLoading && !tenant && !tenantQuery.isError ? (
        <Typography.Paragraph type="secondary">
          {t('tenants.users.errors.tenantNotFound')}
        </Typography.Paragraph>
      ) : null}

      {tenant ? (
        <DemoImportModal
          open={demoImportOpen}
          tenantId={tenant.id}
          tenantName={tenant.name}
          tenantSlug={tenant.slug}
          onClose={() => setDemoImportOpen(false)}
          onSuccess={() => {
            refetchProducts();
            invalidateTenant();
          }}
        />
      ) : null}
    </AdminPageShell>
  );
}
