'use client';

import React from 'react';
import { Typography } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { BackupDashboard } from '@/features/backup/pages/BackupDashboard';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';

export default function AdminBackupMonitoringPage() {
  const { t } = useI18n();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader
        title={t('backupDr.monitoring.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('backupDr.monitoring.pageTitle'), href: '/admin/backup' },
        ]}
      />
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        {t('backupDr.monitoring.pageSubtitle')}
      </Typography.Paragraph>
      <BackupDashboard />
    </div>
  );
}
