'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { DigitalServiceRequestsPanel } from '@/features/digital-services/components/DigitalServiceRequestsPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

/**
 * Super Admin: digital service creation request queue (approve / reject).
 * Approve does not auto-generate — create via the tenant digital page afterwards.
 */
export default function DigitalRequestsPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('superadmin.digital.requestsPageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.platformAdminHub'), href: '/admin' },
          { title: t('superadmin.digital.pageTitle'), href: '/admin/digital' },
          { title: t('superadmin.digital.requestsPageTitle') },
        ]}
      />
      <DigitalServiceRequestsPanel showStatusFilter titleLevel={4} />
    </AdminPageShell>
  );
}
