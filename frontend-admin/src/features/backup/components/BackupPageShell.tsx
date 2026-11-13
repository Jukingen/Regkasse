'use client';

import { Typography } from 'antd';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { BackupAccessBanner } from '@/features/backup/components/BackupAccessBanner';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { BACKUP_HUB_LANDING_PATH } from '@/shared/backupAreaRoutes';

export type BackupPageShellProps = {
  titleKey: string;
  sectionLabelKey: string;
  sectionHref: string;
  subtitleKey?: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
  showAccessBanner?: boolean;
};

export function BackupPageShell({
  titleKey,
  sectionLabelKey,
  sectionHref,
  subtitleKey,
  actions,
  children,
  showAccessBanner = true,
}: BackupPageShellProps) {
  const { t } = useI18n();

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <AdminPageHeader
        title={t(titleKey)}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('nav.backupDisasterRecovery'), href: BACKUP_HUB_LANDING_PATH },
          { title: t(sectionLabelKey), href: sectionHref },
        ]}
        actions={actions}
      />
      {subtitleKey ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t(subtitleKey)}
        </Typography.Paragraph>
      ) : null}
      {showAccessBanner ? <BackupAccessBanner /> : null}
      {children}
    </div>
  );
}
