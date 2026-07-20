'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { DigitalBillingDashboard } from '@/features/billing/components/DigitalBillingDashboard';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function DigitalBillingPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('billing.digital.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.licenseHubSales'), href: '/admin/billing' },
          { title: t('billing.digital.pageTitle') },
        ]}
      />
      <DigitalBillingDashboard />
    </AdminPageShell>
  );
}
