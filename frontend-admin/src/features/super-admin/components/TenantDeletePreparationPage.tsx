'use client';

import React, { useCallback } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { Alert, Button } from 'antd';
import { ArrowLeftOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { useI18n } from '@/i18n';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { TenantDeletePreparationPanel } from '@/features/super-admin/components/TenantDeletePreparationPanel';
import {
    invalidateTenantLifecycleQueries,
    TENANT_DETAIL_QUERY_KEY,
} from '@/features/super-admin/utils/invalidateTenantLifecycleQueries';

export function TenantDeletePreparationPage() {
    const { t } = useI18n();
    const params = useParams();
    const router = useRouter();
    const queryClient = useQueryClient();
    const { user } = useAuth();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

    const canAccess =
        isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);

    const tenantQuery = useQuery({
        queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId],
        queryFn: () => getAdminTenantById(tenantId),
        enabled: canAccess && !!tenantId,
    });

    const invalidateTenantViews = useCallback(() => {
        invalidateTenantLifecycleQueries(queryClient, tenantId);
    }, [queryClient, tenantId]);

    if (!canAccess) {
        return (
            <AdminPageShell>
                <Alert
                    type="error"
                    title={t('tenants.accessDenied.title')}
                    description={t('tenants.accessDenied.body')}
                />
            </AdminPageShell>
        );
    }

    if (!tenantId) {
        return (
            <AdminPageShell>
                <Alert type="error" title={t('tenants.users.errors.invalidTenant')} />
            </AdminPageShell>
        );
    }

    const tenant = tenantQuery.data;
    const title = tenant
        ? `${tenant.name} — ${t('tenants.deletePreparation.pageTitle')}`
        : t('tenants.deletePreparation.pageTitle');

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={title}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.settingsHub), href: '/settings' },
                    { title: t('tenants.page.title'), href: '/admin/tenants' },
                    tenant
                        ? {
                              title: `${tenant.name} (${tenant.slug})`,
                              href: `/admin/tenants/${tenantId}`,
                          }
                        : { title: tenantId },
                    { title: t('tenants.deletePreparation.pageTitle') },
                ]}
                actions={
                    <Link href={`/admin/tenants/${tenantId}?tab=settings`}>
                        <Button icon={<ArrowLeftOutlined />}>
                            {t('tenants.decommission.actions.backToTenant')}
                        </Button>
                    </Link>
                }
            />

            {tenantQuery.isError ? (
                <Alert type="error" title={t('tenants.users.errors.tenantNotFound')} style={{ marginBottom: 16 }} />
            ) : null}

            {tenant ? (
                <TenantDeletePreparationPanel
                    tenantId={tenant.id}
                    tenantName={tenant.name}
                    tenantSlug={tenant.slug}
                    tenantStatus={tenant.status}
                    onArchiveSuccess={invalidateTenantViews}
                    onPermanentDeleteSuccess={() => {
                        invalidateTenantViews();
                        router.push('/admin/tenants');
                    }}
                />
            ) : null}
        </AdminPageShell>
    );
}
