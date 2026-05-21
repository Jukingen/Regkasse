'use client';

import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { adminOverviewCrumb, ADMIN_NAV_LABEL_KEYS } from '@/shared/adminShellLabels';
import { getAdminTenantById } from '@/features/super-admin/api/adminTenants';
import { TenantDetailUsersTab } from '@/features/super-admin/components/TenantDetailUsersTab';
import { useI18n } from '@/i18n';

const TENANT_DETAIL_QUERY_KEY = ['admin', 'tenant-detail'] as const;

/** Dedicated tenant user management route with quick-user generation. */
export default function TenantUsersPage() {
    const { t } = useI18n();
    const params = useParams();
    const tenantId = typeof params.tenantId === 'string' ? params.tenantId : '';

    const tenantQuery = useQuery({
        queryKey: [...TENANT_DETAIL_QUERY_KEY, tenantId],
        queryFn: () => getAdminTenantById(tenantId),
        enabled: !!tenantId,
    });

    const tenant = tenantQuery.data ?? null;

    return (
        <AdminPageShell>
            <AdminPageHeader
                title={t('tenants.users.page.title')}
                breadcrumbs={[
                    adminOverviewCrumb(t),
                    { title: t(ADMIN_NAV_LABEL_KEYS.tenants), href: '/admin/tenants' },
                    {
                        title: tenant?.name ?? tenantId,
                        href: tenantId ? `/admin/tenants/${tenantId}` : undefined,
                    },
                    { title: t('tenants.users.page.title') },
                ]}
            />
            <TenantDetailUsersTab tenantId={tenantId} tenant={tenant} />
        </AdminPageShell>
    );
}
