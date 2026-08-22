'use client';

import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import { Alert, Badge, Button, Space, Typography } from 'antd';
import dayjs from 'dayjs';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { useCallback, useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { ActivityLog } from '@/features/tenants/components/limit-dashboard/ActivityLog';
import { CriticalAlerts } from '@/features/tenants/components/limit-dashboard/CriticalAlerts';
import { CriticalUsersTable } from '@/features/tenants/components/limit-dashboard/CriticalUsersTable';
import { DashboardSummaryCards } from '@/features/tenants/components/limit-dashboard/DashboardSummaryCards';
import { exportLimitDashboardCsv } from '@/features/tenants/components/limit-dashboard/exportLimitDashboardCsv';
import { LimitDashboardContextBar } from '@/features/tenants/components/limit-dashboard/LimitDashboardContextBar';
import { LimitProgressList } from '@/features/tenants/components/limit-dashboard/LimitProgressList';
import {
  LIMIT_DASHBOARD_ALL_TENANTS_VALUE,
  buildLimitDashboardHref,
  formatLimitDashboardPersonName,
  parseLimitDashboardSearch,
} from '@/features/tenants/components/limit-dashboard/limitDashboardUrl';
import {
  limitDashboardLabelKey,
  type LimitStatusFilter,
} from '@/features/tenants/components/limit-dashboard/limitDashboardShared';
import { useLimitDashboard } from '@/features/tenants/hooks/useTenantLimits';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useCashRegisterSelection } from '@/hooks/useCashRegisterSelection';
import { useNotify } from '@/hooks/useNotify';
import { useTenant } from '@/hooks/useTenant';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { ApiErrorAlertDescription } from '@/shared/errors/ApiErrorAlertDescription';

export default function LimitDashboard() {
  const { t } = useI18n();
  const notify = useNotify();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const superAdmin = isSuperAdmin(user?.role);
  const { tenant, tenants, tenantsLoading } = useTenant({ loadTenants: superAdmin });
  const [filter, setFilter] = useState<LimitStatusFilter>('all');

  const search = useMemo(() => parseLimitDashboardSearch(searchParams), [searchParams]);
  const allTenants = Boolean(
    superAdmin && (search.allTenants || (!search.tenantId && !tenant?.id))
  );
  const selectedTenantId = allTenants ? undefined : (search.tenantId || tenant?.id || undefined);
  const selectedTenant = tenants.find((row) => row.id === selectedTenantId);
  const tenantName = selectedTenant?.name ?? tenant?.name;
  const tenantSlug = selectedTenant?.slug ?? tenant?.slug;

  const registerSelection = useCashRegisterSelection({
    enabled: !allTenants,
    tenantId: selectedTenantId,
    value: search.registerId,
    controlled: true,
    autoSelect: false,
    autoSelectSingle: false,
    persistSelection: false,
  });
  const selectedRegister = registerSelection.selectedRegister;

  const query = useLimitDashboard({
    allTenants: superAdmin && allTenants,
    tenantId: superAdmin && !allTenants ? selectedTenantId : undefined,
  });
  const data = query.data;
  const unread = data?.unreadAlertCount ?? 0;
  const showTenant = superAdmin && allTenants;
  const viewerName = formatLimitDashboardPersonName(user);
  const registerLabel = selectedRegister
    ? `${selectedRegister.registerNumber} — ${selectedRegister.location}`.trim()
    : null;

  const replaceDashboardUrl = useCallback(
    (next: { allTenants?: boolean; tenantId?: string; registerId?: string }) => {
      const href = buildLimitDashboardHref({
        allTenants: next.allTenants === true,
        tenantId: next.allTenants ? undefined : next.tenantId,
        registerId: next.allTenants ? undefined : next.registerId,
      });
      if (href !== `${pathname}${searchParams.toString() ? `?${searchParams.toString()}` : ''}`) {
        router.push(href, { scroll: false });
      }
    },
    [pathname, router, searchParams]
  );

  const handleTenantChange = (value: string) => {
    if (value === LIMIT_DASHBOARD_ALL_TENANTS_VALUE) {
      replaceDashboardUrl({ allTenants: true });
      return;
    }
    replaceDashboardUrl({ tenantId: value });
  };

  const handleRegisterChange = (registerId: string | undefined) => {
    replaceDashboardUrl({ tenantId: selectedTenantId, registerId });
  };

  const limitName = (key: string, fallback: string) => {
    const i18nKey = limitDashboardLabelKey(key);
    const label = t(i18nKey);
    return label === i18nKey ? fallback : label;
  };

  const filteredLimits = useMemo(() => {
    const limits = data?.limits ?? [];
    if (filter === 'all') return limits;
    return limits.filter((row) => row.status === filter);
  }, [data?.limits, filter]);

  const criticalUsers = useMemo(() => {
    const rows = data?.criticalUsers ?? [];
    const assignedUserId = selectedRegister?.assignedUserId;
    if (!search.registerId || !assignedUserId) {
      return search.registerId ? [] : rows;
    }
    return rows.filter((row) => row.userId === assignedUserId);
  }, [data?.criticalUsers, search.registerId, selectedRegister?.assignedUserId]);

  const exportCsv = () => {
    if (!data || filteredLimits.length === 0) {
      notify.warning(t('tenants.limits.dashboard.exportEmpty'));
      return;
    }
    exportLimitDashboardCsv(
      { limits: filteredLimits },
      {
        tenant: t('tenants.limits.dashboard.tenant'),
        key: t('tenants.limits.dashboard.limit'),
        name: t('tenants.limits.dashboard.limit'),
        current: t('tenants.limits.dashboard.csv.current'),
        limit: t('tenants.limits.dashboard.csv.cap'),
        percentage: t('tenants.limits.dashboard.csv.percentage'),
        status: t('tenants.limits.dashboard.users.status'),
        trend: t('tenants.limits.dashboard.limits.trend'),
        changeCount: t('tenants.limits.dashboard.limits.change'),
        changeUnit: t('tenants.limits.dashboard.limits.changeUnit'),
      },
      limitName
    );
    notify.success(t('tenants.limits.dashboard.exported', { count: String(filteredLimits.length) }));
  };

  return (
    <Space orientation="vertical" size={16} style={{ width: '100%' }}>
      <AdminPageHeader
        title={
          <Space>
            <span>{t('tenants.limits.dashboard.title')}</span>
            <Badge count={unread} overflowCount={99} />
          </Space>
        }
        subtitle={
          <Space orientation="vertical" size={0}>
            <span>{t('tenants.limits.dashboard.subtitle')}</span>
            {superAdmin ? null : (
              <Typography.Text type="secondary">
                {t('tenants.limits.dashboard.context.mandantLine', {
                  name: tenantName || '—',
                  slug: tenantSlug || '—',
                })}
              </Typography.Text>
            )}
            {data?.lastUpdated ? (
              <Typography.Text type="secondary">
                {t('tenants.limits.dashboard.lastUpdated')}:{' '}
                {dayjs(data.lastUpdated).format('DD.MM.YYYY HH:mm:ss')}
              </Typography.Text>
            ) : null}
          </Space>
        }
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.licenseManagement'), href: '/admin/license-management' },
          { title: t('nav.limitDashboard') },
        ]}
        extra={
          <Space>
            <Button icon={<DownloadOutlined />} onClick={exportCsv}>
              {t('tenants.limits.dashboard.exportCsv')}
            </Button>
            <Button
              icon={<ReloadOutlined />}
              loading={query.isFetching}
              onClick={() => void query.refetch()}
            >
              {t('tenants.limits.dashboard.refresh')}
            </Button>
          </Space>
        }
      />

      <Alert
        type="info"
        showIcon
        title={t('tenants.limits.dashboard.context.signedIn', {
          name: viewerName,
          userName: user?.userName || '—',
        })}
        description={t('tenants.limits.dashboard.context.roleTenant', {
          role: formatRoleDisplayLabel(t, user?.role || ''),
          slug: allTenants ? t('tenants.limits.dashboard.allTenants') : tenantSlug || '—',
        })}
      />

      <LimitDashboardContextBar
        isSuperAdmin={superAdmin}
        allTenants={allTenants}
        tenantName={tenantName}
        tenantSlug={tenantSlug}
        tenants={tenants}
        tenantsLoading={tenantsLoading}
        selectedTenantId={selectedTenantId}
        onTenantChange={handleTenantChange}
        register={selectedRegister}
        registerOptions={registerSelection.registerOptions}
        registersLoading={registerSelection.isLoading}
        onRegisterChange={handleRegisterChange}
        viewerName={viewerName}
        viewerUserName={user?.userName}
      />

      {query.isError ? (
        <Alert
          type="error"
          showIcon
          title={t('tenants.limits.dashboard.loadFailed')}
          description={
            <ApiErrorAlertDescription
              t={t}
              error={query.error}
              logContext="LimitDashboard.load"
              fallbackKey="tenants.limits.dashboard.loadFailedHint"
            />
          }
          action={
            <Button size="small" loading={query.isFetching} onClick={() => void query.refetch()}>
              {t('common.buttons.retry')}
            </Button>
          }
        />
      ) : (
        <>
          <DashboardSummaryCards summary={data?.summary} />
          <CriticalAlerts limits={data?.limits ?? []} users={criticalUsers} />
          <LimitProgressList
            limits={data?.limits ?? []}
            loading={query.isLoading}
            showTenant={showTenant}
            isSuperAdmin={superAdmin}
            filter={filter}
            onFilterChange={setFilter}
            onOpenDetail={(href) => router.push(href)}
            registerLabel={registerLabel}
          />
          <CriticalUsersTable
            users={criticalUsers}
            showTenant={showTenant}
            isSuperAdmin={superAdmin}
            onOpenDetail={(href) => router.push(href)}
          />
          <ActivityLog rows={data?.recentActivity ?? []} showTenant={showTenant} />
        </>
      )}
    </Space>
  );
}
