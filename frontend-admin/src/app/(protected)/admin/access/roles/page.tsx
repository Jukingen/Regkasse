'use client';

import { Alert, Button } from 'antd';
import Link from 'next/link';
import React, { useMemo } from 'react';
import { useSearchParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { useRoleManagementWorkspace } from '@/features/access/hooks/useRoleManagementWorkspace';
import { RoleManagementDrawer } from '@/features/users/components/RoleManagementDrawer';
import { CreateRoleModal } from '@/features/users/components/UsersPageActionModals';
import {
  buildUsersFormRulesContext,
  createUsersFormRules,
} from '@/features/users/constants/validation';
import { usePermissions } from '@/hooks/usePermissions';
import { useI18n } from '@/i18n';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function AccessRolesPage() {
  const { t } = useI18n();
  const searchParams = useSearchParams();
  const workspace = useRoleManagementWorkspace();
  const modalRules = useMemo(() => createUsersFormRules(buildUsersFormRulesContext(t)), [t]);
  const { hasPermission } = usePermissions();
  const canViewHistory = hasPermission(PERMISSIONS.AUDIT_VIEW);

  const initialMenuFilter = searchParams.get('menu');
  const initialPermissionFocus = searchParams.get('permission');

  const breadcrumbs = [
    adminOverviewCrumb(t),
    { title: t('access.hub.pageTitle'), href: '/admin/access' },
    { title: t('access.roles.pageTitle') },
  ];

  if (!workspace.enabled && !workspace.policy.canView) {
    return (
      <AdminPageShell>
        <AdminPageHeader title={t('access.roles.pageTitle')} breadcrumbs={breadcrumbs} />
        <Alert
          type="warning"
          showIcon
          title={t('access.hub.accessDeniedTitle')}
          description={t('access.hub.accessDeniedDescription')}
        />
      </AdminPageShell>
    );
  }

  return (
    <AdminPageShell>
      <AdminPageHeader
        title={t('access.roles.pageTitle')}
        breadcrumbs={breadcrumbs}
        actions={
          canViewHistory ? (
            <Link href="/admin/access/permission-history" prefetch={false}>
              <Button type="default">{t('users.roleDrawer.openHistory')}</Button>
            </Link>
          ) : null
        }
      />
      <RoleManagementDrawer
        open
        presentation="page"
        onClose={() => undefined}
        roles={workspace.roles}
        catalog={workspace.catalog}
        rolesLoading={workspace.rolesLoading}
        catalogLoading={workspace.catalogLoading}
        rolesError={workspace.rolesError}
        catalogError={workspace.catalogError}
        onRetry={workspace.onRetry}
        canCreateRole={workspace.policy.canCreateRole}
        canDeleteRole={workspace.policy.canDeleteRole}
        canEditRolePermissions={workspace.policy.canEditRolePermissions}
        onCreateRole={() => workspace.setCreateRoleOpen(true)}
        onSavePermissions={workspace.handleSaveRolePermissions}
        onDeleteRole={workspace.handleDeleteRole}
        saveLoading={workspace.saveLoading}
        deleteLoading={workspace.deleteLoading}
        initialMenuFilter={initialMenuFilter}
        initialPermissionFocus={initialPermissionFocus}
      />
      {workspace.createRoleOpen ? (
        <CreateRoleModal
          onCancel={() => workspace.setCreateRoleOpen(false)}
          onConfirm={workspace.handleCreateRoleConfirm}
          confirmLoading={workspace.createRoleLoading}
          roleNameRules={modalRules.roleName}
          inheritRoleOptions={workspace.inheritRoleOptions}
        />
      ) : null}
    </AdminPageShell>
  );
}
