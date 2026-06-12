'use client';

import { Card } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { PersonalizationSettings } from '@/features/settings/components/PersonalizationSettings';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function PersonalizationSettingsPage() {
  const { t } = useI18n();
  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('nav.settingsHub'), href: '/settings' },
    { title: t('settings.personalization.pageTitle') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      <AdminPageHeader
        title={t('settings.personalization.pageTitle')}
        breadcrumbs={breadcrumbs}
      />
      <Card variant="borderless" styles={{ body: { padding: 0 } }}>
        <PersonalizationSettings />
      </Card>
    </div>
  );
}
