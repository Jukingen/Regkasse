'use client';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { DownloadAnalyticsDashboard } from '@/features/download-history/components/DownloadAnalyticsDashboard';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';

export default function DownloadAnalyticsPage() {
  const { t } = useI18n();

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('common.downloadAnalytics.title')}
        breadcrumbs={[
          adminOverviewCrumb(t),
          { title: t('adminShell.group.rksv'), href: '/rksv' },
          { title: t('common.downloadHistory.breadcrumb'), href: '/admin/download-history' },
          { title: t('common.downloadAnalytics.breadcrumb') },
        ]}
      />
      <DownloadAnalyticsDashboard />
    </AdminPageShell>
  );
}
