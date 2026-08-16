'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { TenantInvoiceTable } from '@/features/tenant-invoices/components/TenantInvoiceTable';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export function TenantInvoicesPageContent() {
  const { t } = useI18n();
  const pageTitle = t('tenantPortal.invoices.title');
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.meinKonto'), href: '/tenant/portal' },
    { title: pageTitle },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={pageTitle} breadcrumbs={breadcrumbs} />
      <TenantInvoiceTable />
    </div>
  );
}
