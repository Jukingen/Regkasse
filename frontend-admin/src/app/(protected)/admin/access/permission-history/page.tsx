'use client';

import { Alert, Button } from 'antd';
import Link from 'next/link';
import React from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { PermissionAuditHistoryPanel } from '@/features/users/components/PermissionAuditHistoryPanel';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/hooks/usePermissions';

export default function PermissionHistoryPage() {
  const { t } = useI18n();
  const { hasPermission } = usePermissions();
  const canView = hasPermission(PERMISSIONS.AUDIT_VIEW);
  const canManageRoles = hasPermission(PERMISSIONS.ROLE_MANAGE);

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('access.hub.pageTitle'), href: '/admin/access' },
    { title: t('access.permissionHistory.pageTitle') },
  ];

  if (!canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('access.permissionHistory.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="warning"
          showIcon
          title={t('access.hub.accessDeniedTitle')}
          description={t('users.permissionAudit.noAuditPermission')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('access.permissionHistory.pageTitle')}
        breadcrumbs={breadcrumbs}
        actions={
          canManageRoles ? (
            <Link href="/admin/access/roles" prefetch={false}>
              <Button type="default">{t('nav.rolesPermissions')}</Button>
            </Link>
          ) : null
        }
      />
      <PermissionAuditHistoryPanel showAllRoles canRevert={canManageRoles} />
    </AdminPageShell>
  );
}
