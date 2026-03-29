'use client';

import React from 'react';
import { Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { BackupDrDashboard } from '@/features/backup-dr/components/BackupDrDashboard';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function BackupDrSettingsPage() {
  const { t } = useI18n();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader
        title={t('backupDr.page.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
          { title: t(ADMIN_NAV_LABEL_KEYS.backupDr), href: '/settings/backup-dr' },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('backupDr.page.subtitle')}
      </Typography.Paragraph>
      <BackupDrDashboard />
    </div>
  );
}
