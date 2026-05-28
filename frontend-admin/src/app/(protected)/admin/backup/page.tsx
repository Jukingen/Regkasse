'use client';

import React from 'react';
import { Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  AdminBackupPage,
  AdminBackupPageHeaderActions,
} from '@/features/backup/pages/AdminBackupPage';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function AdminBackupPageRoute() {
  const { t } = useI18n();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader
        title={t('backupDr.adminBackup.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('backupDr.adminBackup.pageTitle'), href: '/admin/backup' },
        ]}
        actions={<AdminBackupPageHeaderActions />}
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('backupDr.adminBackup.pageSubtitle')}
      </Typography.Paragraph>
      <AdminBackupPage />
    </div>
  );
}
