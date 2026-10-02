'use client';

import { Alert, Typography } from 'antd';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { VerticalConfigHub } from '@/features/vertical-profiles/VerticalConfigHub';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { buildPlatformAdminBreadcrumbs } from '@/shared/adminPlatformBreadcrumbs';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function VerticalProfilesPage() {
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canAccess = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const breadcrumbs = buildPlatformAdminBreadcrumbs(t, 'administration', [
    { title: t('admin.verticalProfiles.pageTitle') },
  ]);

  if (!canAccess) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('admin.verticalProfiles.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert type="error" showIcon title={t('admin.verticalProfiles.accessDenied')} />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.verticalProfiles.pageTitle')} breadcrumbs={breadcrumbs} />
      <Typography.Paragraph type="secondary">
        {t('admin.verticalProfiles.subtitle')}
      </Typography.Paragraph>
      <VerticalConfigHub />
    </AdminPageShell>
  );
}
