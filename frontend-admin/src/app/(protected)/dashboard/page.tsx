'use client';

import React from 'react';
import Link from 'next/link';
import { Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { Dashboard } from '@/features/dashboard/components/Dashboard';
import { DashboardMonatsbelegSection } from '@/features/dashboard/components/DashboardMonatsbelegSection';
import { OfflineQueueDashboardCard } from '@/features/dashboard/components/OfflineQueueDashboardCard';
import { HospitalityQuickLinksCard } from '@/features/dashboard/components/HospitalityQuickLinksCard';
import { TimeSyncDriftAlertCard } from '@/features/dashboard/components/TimeSyncDriftAlertCard';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';
import { LicenseDashboardSection } from '@/features/dashboard/components/LicenseDashboardSection';
import { RksvReminderCard } from '@/features/dashboard/components/RksvReminderCard';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';
import { RKSV_HUB_PATH } from '@/shared/auth/rksvRoutePaths';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';

export default function DashboardPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canOpenRksvHub = useCanAccessPath(RKSV_HUB_PATH);

    const { isAuthorized: canFetchTenantLicense } = useAuthorizationGate({
        requiredRole: 'SuperAdmin',
    });
    const { isAuthorized: canSeeRksvReminder } = useAuthorizationGate({
        requiredPermission: AppPermissions.CashRegisterView,
    });

    const offlineQueueCardEnabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);
    const timeSyncDriftAlertEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
    const tseHealthCardEnabled = hasPermission(AppPermissions.CashRegisterView);

    const operationalHeader = (
        <>
            {canFetchTenantLicense ? <LicenseDashboardSection /> : null}
            {offlineQueueCardEnabled ? <OfflineQueueDashboardCard /> : null}
            {timeSyncDriftAlertEnabled ? <TimeSyncDriftAlertCard /> : null}
            {tseHealthCardEnabled ? <TseHealthCard /> : null}
            {canSeeRksvReminder ? <RksvReminderCard /> : null}
            {canSeeRksvReminder ? (
                <DashboardMonatsbelegSection enabled={canSeeRksvReminder} />
            ) : null}
            <HospitalityQuickLinksCard />
        </>
    );

    return (
        <div style={{ paddingBottom: 24 }}>
            <AdminPageHeader
                title={t('nav.overview')}
                breadcrumbs={[adminOverviewCrumb(t)]}
            >
                <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
                    Anpassbares Dashboard mit Widgets (ziehen zum Sortieren). Operative Kassenberichte:{' '}
                    <Link href="/reporting">{t('nav.reporting')}</Link>.
                    {canOpenRksvHub ? (
                        <>
                            {' '}
                            RKSV: Seitenleiste unter «RKSV».
                        </>
                    ) : null}
                </Typography.Paragraph>
            </AdminPageHeader>

            <Dashboard headerSlot={operationalHeader} />
        </div>
    );
}
