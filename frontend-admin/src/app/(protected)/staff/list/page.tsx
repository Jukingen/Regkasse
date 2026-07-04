'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { StaffList } from '@/features/staff/components/StaffList';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function StaffListPage() {
    const { t } = useI18n();

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('staff:list.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t('staff:hub.pageTitle'), href: '/staff' },
                    { title: t('staff:list.pageTitle') },
                ]}
            />
            <StaffList />
        </AdminPageShell>
    );
}
