'use client';

import { Typography } from 'antd';
import { useSearchParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { UserActivityReport } from '@/features/reports/components/UserActivityReport';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export function UserActivityReportPage() {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const initialUserId = searchParams.get('userId') ?? undefined;

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('reporting.userActivity.pageTitle')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('reporting.userActivity.hubTitle'), href: '/admin/reports' },
          { title: t('reporting.userActivity.pageTitle') },
        ]}
      >
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('reporting.userActivity.pageIntro')}
        </Typography.Paragraph>
      </AdminPageHeader>
      <UserActivityReport initialUserId={initialUserId} />
    </AdminPageShell>
  );
}
