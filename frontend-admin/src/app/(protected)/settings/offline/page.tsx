'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { OfflineSettings } from '@/features/settings/components/OfflineSettings';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function OfflineSettingsPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.offline.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('settings.offline.pageTitle')} breadcrumbs={breadcrumbs} />
      <OfflineSettings />
    </div>
  );
}
