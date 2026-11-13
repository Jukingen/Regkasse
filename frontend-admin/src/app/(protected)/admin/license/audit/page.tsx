'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { LicenseAuditLogCard } from '@/features/license/components/LicenseAuditLogCard';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function LicenseAuditPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('license.auditLog.title')}
        subtitle={t('license.auditLog.subtitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.licenseManagement'), href: '/admin/license-management' },
          { title: t('nav.licenseAudit') },
        ]}
      />
      <LicenseAuditLogCard />
    </AdminPageShell>
  );
}
