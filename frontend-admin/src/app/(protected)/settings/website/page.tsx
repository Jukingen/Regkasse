'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { WebsiteGeneratorPanel } from '@/features/website-generator/components/WebsiteGeneratorPanel';
import { TenantDomainsPanel } from '@/features/website-generator/components/TenantDomainsPanel';
import { TenantCustomizationPanel } from '@/features/website-generator/components/TenantCustomizationPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function WebsiteGeneratorPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.websiteGenerator.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('settings.websiteGenerator.pageTitle')} breadcrumbs={breadcrumbs} />
      <TenantCustomizationPanel surface="website" />
      <TenantCustomizationPanel surface="app" />
      <WebsiteGeneratorPanel />
      <TenantDomainsPanel />
    </div>
  );
}
