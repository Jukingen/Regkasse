'use client';

import { useSearchParams } from 'next/navigation';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { UserActivityReport } from '@/features/reports/components/UserActivityReport';
import { userActivityReportCopy as copy } from '@/features/reports/constants/copy';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export function UserActivityReportPage() {
    const { t } = useI18n();
    const searchParams = useSearchParams();
    const initialUserId = searchParams.get('userId') ?? undefined;

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={copy.pageTitle}
                description={copy.pageIntro}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: copy.hubTitle, href: '/admin/reports' },
                    { title: copy.pageTitle },
                ]}
            />
            <UserActivityReport initialUserId={initialUserId} />
        </AdminPageShell>
    );
}
