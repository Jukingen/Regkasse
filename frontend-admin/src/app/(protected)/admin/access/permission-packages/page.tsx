'use client';

import { Alert } from 'antd';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { PermissionPackagesPanel } from '@/features/users/components/PermissionPackagesPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function PermissionPackagesPage() {
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canView =
    isSuperAdmin ||
    hasPermission(PERMISSIONS.ROLE_VIEW) ||
    hasPermission(PERMISSIONS.ROLE_MANAGE) ||
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('access.hub.pageTitle'), href: '/admin/access' },
    { title: t('access.permissionPackages.pageTitle') },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('access.permissionPackages.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="warning"
          showIcon
          title={t('access.hub.accessDeniedTitle')}
          description={t('access.permissionPackages.accessDenied')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('access.permissionPackages.pageTitle')} breadcrumbs={breadcrumbs} />
      <PermissionPackagesPanel />
    </AdminPageShell>
  );
}
