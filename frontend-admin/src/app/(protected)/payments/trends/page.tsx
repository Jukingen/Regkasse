'use client';

import { PaymentTrendsDashboard } from '@/features/analytics/components/PaymentTrendsDashboard';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ADMIN_NAV_LABELS, adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function PaymentTrendsPage() {
    const { t } = useI18n();

    return (
        <>
            <AdminPageHeader
                title={t('payments.trendsDashboard.pageTitle')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: ADMIN_NAV_LABELS.payments, href: '/payments' },
                    { title: t('payments.trendsDashboard.pageTitle') },
                ]}
            />
            <PaymentTrendsDashboard />
        </>
    );
}
