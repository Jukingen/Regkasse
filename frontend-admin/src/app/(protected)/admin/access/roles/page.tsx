'use client';

import React, { useMemo } from 'react';
import { Alert } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { RoleManagementDrawer } from '@/features/users/components/RoleManagementDrawer';
import { CreateRoleModal } from '@/features/users/components/UsersPageActionModals';
import { useRoleManagementWorkspace } from '@/features/access/hooks/useRoleManagementWorkspace';
import { createUsersFormRules } from '@/features/users/constants/validation';
import { usersCopy } from '@/features/users/constants/copy';

const modalFormRulesContext = {
    requiredMessage: usersCopy.validationRequired,
    emailInvalidMessage: usersCopy.validationEmail,
    passwordMinMessage: usersCopy.validationPasswordMin,
    passwordPolicyMessage: usersCopy.validationPasswordPolicy,
    maxLengthMessage: usersCopy.validationMaxLength,
    reasonRequiredMessage: usersCopy.reasonRequiredMessage,
    roleNameRequiredMessage: usersCopy.roleNameRequired,
};

export default function AccessRolesPage() {
    const { t } = useI18n();
    const workspace = useRoleManagementWorkspace();
    const modalRules = useMemo(() => createUsersFormRules(modalFormRulesContext), []);

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
            <AdminPageHeader title={t('access.roles.pageTitle')} breadcrumbs={breadcrumbs} />
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
            />
            {workspace.createRoleOpen ? (
                <CreateRoleModal
                    onCancel={() => workspace.setCreateRoleOpen(false)}
                    onConfirm={workspace.handleCreateRoleConfirm}
                    confirmLoading={workspace.createRoleLoading}
                    roleNameRules={modalRules.roleName}
                />
            ) : null}
        </AdminPageShell>
    );
}
