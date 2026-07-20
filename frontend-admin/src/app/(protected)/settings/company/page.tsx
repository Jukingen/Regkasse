'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { CompanySettingsForm } from '@/features/settings/components/CompanySettingsForm';
import { useI18n } from '@/i18n';
import { buildAdminBreadcrumbs } from '@/shared/adminShellLabels';

export default function CompanySettingsPage() {
    const { t } = useI18n();
    const breadcrumbs = buildAdminBreadcrumbs(t, [
        { title: t('nav.settingsHub'), href: '/settings' },
        { title: t('settings.companyPage.pageTitle') },
    ]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
            <AdminPageHeader title={t('settings.companyPage.pageTitle')} breadcrumbs={breadcrumbs} />
            <CompanySettingsForm />
        </div>
    );
}