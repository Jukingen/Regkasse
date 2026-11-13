'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { DigitalServiceAccess } from '@/features/digital/components/DigitalServiceAccess';
import { CustomerPortalPanel } from '@/features/website-generator/components/CustomerPortalPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function CustomerPortalPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('tenants.digitalServices.customerPortalTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={t('tenants.digitalServices.customerPortalTitle')}
        breadcrumbs={breadcrumbs}
      />
      <DigitalServiceAccess>
        <CustomerPortalPanel />
      </DigitalServiceAccess>
    </div>
  );
}
