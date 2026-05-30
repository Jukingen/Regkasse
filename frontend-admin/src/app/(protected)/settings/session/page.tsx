'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { SessionSettings } from '@/features/settings/components/SessionSettings';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function SessionSettingsPage() {
    const { t } = useI18n();
    const breadcrumbs = [
        adminOverviewCrumb(t),
        { title: t('nav.settingsHub'), href: '/settings' },
        { title: t('settings.session.pageTitle') },
    ];

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
            <AdminPageHeader title={t('settings.session.pageTitle')} breadcrumbs={breadcrumbs} />
            <SessionSettings />
        </div>
    );
}
