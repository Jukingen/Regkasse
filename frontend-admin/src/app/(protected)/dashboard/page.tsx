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
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { useI18n } from '@/i18n/I18nProvider';

export default function DashboardPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const offlineQueueCardEnabled = hasPermission(PERMISSIONS.PAYMENT_VIEW);
    const monatsbelegOverviewEnabled = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);
    const tseHealthCardEnabled = hasPermission(PERMISSIONS.CASHREGISTER_VIEW);
    const timeSyncDriftAlertEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);
    const licenseDashboardEnabled = hasPermission(PERMISSIONS.SETTINGS_MANAGE);

    const operationalHeader = (
        <>
            {licenseDashboardEnabled ? <LicenseDashboardSection /> : null}
            {offlineQueueCardEnabled ? <OfflineQueueDashboardCard /> : null}
            {timeSyncDriftAlertEnabled ? <TimeSyncDriftAlertCard /> : null}
            {tseHealthCardEnabled ? <TseHealthCard /> : null}
            {monatsbelegOverviewEnabled ? <RksvReminderCard /> : null}
            {monatsbelegOverviewEnabled ? (
                <DashboardMonatsbelegSection enabled={monatsbelegOverviewEnabled} />
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
                    <Link href="/reporting">{t('nav.reporting')}</Link>. RKSV: Seitenleiste unter «RKSV».
                </Typography.Paragraph>
            </AdminPageHeader>

            <Dashboard headerSlot={operationalHeader} />
        </div>
    );
}
