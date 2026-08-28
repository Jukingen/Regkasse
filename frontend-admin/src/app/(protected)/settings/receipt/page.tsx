'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { ReceiptSettings } from '@/features/settings/ReceiptSettings';
import { useI18n } from '@/i18n';
import { buildAdminBreadcrumbs } from '@/shared/adminShellLabels';

export default function ReceiptSettingsPage() {
  const { t } = useI18n();
  const breadcrumbs = buildAdminBreadcrumbs(t, [
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.receiptPage.pageTitle') },
  ]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader title={t('settings.receiptPage.pageTitle')} breadcrumbs={breadcrumbs} />
      <ReceiptSettings />
    </div>
  );
}
