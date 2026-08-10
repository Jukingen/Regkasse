'use client';

import { Typography } from 'antd';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { BulkEmailForm } from '@/features/communication/components/BulkEmailForm';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';

export default function BulkEmailPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('communication.bulkEmail.title')}
        breadcrumbs={buildPlatformAdminBreadcrumbs(t, 'administration', {
          title: t('communication.bulkEmail.title'),
        })}
      />
      <Typography.Paragraph type="secondary">
        {t('communication.bulkEmail.subtitle')}
      </Typography.Paragraph>
      <BulkEmailForm />
    </AdminPageShell>
  );
}
