'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { PaymentTrendsDashboard } from '@/features/analytics/components/PaymentTrendsDashboard';
import { useI18n } from '@/i18n';
import { ADMIN_NAV_LABELS, adminOverviewCrumb } from '@/shared/adminShellLabels';

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
