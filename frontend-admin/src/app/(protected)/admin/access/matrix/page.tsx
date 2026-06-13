'use client';

import React from 'react';
import { Alert } from 'antd';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { RoleMatrixOverview } from '@/features/access/components/RoleMatrixOverview';
import { useRolesWithPermissions } from '@/features/users/hooks/useRolesWithPermissions';
import { usePermissions } from '@/hooks/usePermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

export default function AccessMatrixPage() {
    const { t } = useI18n();
    const { hasPermission } = usePermissions();
    const canView = hasPermission(PERMISSIONS.ROLE_VIEW);

    const rolesQuery = useRolesWithPermissions({ enabled: canView });

    const breadcrumbs = [
        adminOverviewCrumb(t),
        { title: t('access.hub.pageTitle'), href: '/admin/access' },
        { title: t('access.matrix.pageTitle') },
    ];

    if (!canView) {
        return (
            <AdminPageShell>
                <AdminPageHeader title={t('access.matrix.pageTitle')} breadcrumbs={breadcrumbs} />
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
            <AdminPageHeader title={t('access.matrix.pageTitle')} breadcrumbs={breadcrumbs} />
            <RoleMatrixOverview
                roles={rolesQuery.data}
                loading={rolesQuery.isLoading}
                error={rolesQuery.isError}
                onRetry={() => void rolesQuery.refetch()}
            />
        </AdminPageShell>
    );
}
