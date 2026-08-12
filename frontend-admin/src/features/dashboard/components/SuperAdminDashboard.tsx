'use client';

import { Typography } from 'antd';
import Link from 'next/link';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { CustomerAnalyticsCards } from '@/features/dashboard/components/CustomerAnalyticsCards';
import { Dashboard } from '@/features/dashboard/components/Dashboard';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { TimeSyncDriftAlertCard } from '@/features/dashboard/components/TimeSyncDriftAlertCard';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import { ExportQuickActionsCard } from '@/features/exports/components/ExportQuickActionsCard';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { RKSV_HUB_PATH } from '@/shared/auth/rksvRoutePaths';

/** Platform Super Admin dashboard with customizable widget grid and operational header cards. */
export function SuperAdminDashboard() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canOpenRksvHub = useCanAccessPath(RKSV_HUB_PATH);

  const offlineQueueCardEnabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);
  const timeSyncDriftAlertEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
  const tseHealthCardEnabled = hasPermission(AppPermissions.CashRegisterView);

  // License analytics (calendar / usage / funnel / audit) live under /admin/license → Reports.
  // RKSV reminders / Monatsbeleg live in catalog widgets (action-required, manager-monatsbeleg).
  const operationalHeader = (
    <>
      <CustomerAnalyticsCards />
      {offlineQueueCardEnabled ? <OfflineQueueDashboardCard /> : null}
      {timeSyncDriftAlertEnabled ? <TimeSyncDriftAlertCard /> : null}
      {tseHealthCardEnabled ? <TseHealthCard /> : null}
      <HospitalityQuickLinksCard />
      <ExportQuickActionsCard />
    </>
  );

  return (
    <div style={{ paddingBottom: 24 }}>
      <AdminPageHeader title={t('nav.overview')} breadcrumbs={[adminOverviewCrumb(t)]}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Anpassbares Dashboard mit Widgets (ziehen zum Sortieren). Operative Kassenberichte:{' '}
          <Link href="/reporting">{t('nav.reporting')}</Link>.
          {canOpenRksvHub ? <> RKSV: Seitenleiste unter «RKSV».</> : null}
        </Typography.Paragraph>
      </AdminPageHeader>

      <Dashboard headerSlot={operationalHeader} />
    </div>
  );
}
