'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { DigitalServicesManagementPanel } from '@/features/digital-services/components/DigitalServicesManagementPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

/**
 * Super Admin digital-service lifecycle (activate / deactivate / price override).
 * Deactivation confirmation (modal.confirm + optional reason) is in DigitalServicesManagementPanel.
 */
export default function DigitalServicesManagementPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('superadmin.digital.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.platformAdminHub'), href: '/admin' },
          { title: t('superadmin.digital.pageTitle') },
        ]}
      />
      <DigitalServicesManagementPanel />
    </AdminPageShell>
  );
}
