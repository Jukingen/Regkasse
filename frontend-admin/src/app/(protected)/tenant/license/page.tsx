'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { TenantLicenseSection } from '@/features/license/components/TenantLicenseSection';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function TenantLicensePage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('license.management.tenantTitle')}
        subtitle={t('license.unified.formatHint')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.meinKonto'), href: '/tenant/portal' },
          { title: t('nav.license') },
        ]}
      />
      <TenantLicenseSection />
    </AdminPageShell>
  );
}
