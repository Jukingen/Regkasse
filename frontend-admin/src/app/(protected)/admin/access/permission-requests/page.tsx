'use client';

import { Alert } from 'antd';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { PermissionRequestsPanel } from '@/features/users/components/PermissionRequestsPanel';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function PermissionRequestsPage() {
  const { t } = useI18n();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canView =
    isSuperAdmin ||
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL) ||
    hasPermission(PERMISSIONS.ROLE_MANAGE) ||
    hasPermission(PERMISSIONS.AUDIT_VIEW);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('access.hub.pageTitle'), href: '/admin/access' },
    { title: t('access.permissionRequests.pageTitle') },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('access.permissionRequests.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="warning"
          showIcon
          title={t('access.hub.accessDeniedTitle')}
          description={t('access.permissionRequests.accessDenied')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader title={t('access.permissionRequests.pageTitle')} breadcrumbs={breadcrumbs} />
      <PermissionRequestsPanel />
    </AdminPageShell>
  );
}
