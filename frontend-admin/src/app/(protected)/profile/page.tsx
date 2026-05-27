'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { SessionManager } from '@/components/SessionManager';
import { ADMIN_NAV_LABEL_KEYS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function ProfilePage() {
    const { t } = useI18n();

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t(ADMIN_NAV_LABEL_KEYS.myProfile)}
                breadcrumbs={[adminOverviewCrumb(t), { title: t(ADMIN_NAV_LABEL_KEYS.myProfile) }]}
            />
            <SessionManager />
        </AdminPageShell>
    );
}
