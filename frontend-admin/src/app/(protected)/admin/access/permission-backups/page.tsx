'use client';

import { Alert } from 'antd';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { PermissionConfigBackupsPanel } from '@/features/users/components/PermissionConfigBackupsPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function PermissionBackupsPage() {
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canView = isSuperAdmin || hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('access.hub.pageTitle'), href: '/admin/access' },
    { title: t('access.permissionBackups.pageTitle') },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('access.permissionBackups.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="warning"
          showIcon
          title={t('access.hub.accessDeniedTitle')}
          description={t('access.permissionBackups.accessDenied')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('access.permissionBackups.pageTitle')} breadcrumbs={breadcrumbs} />
      <PermissionConfigBackupsPanel />
    </AdminPageShell>
  );
}
