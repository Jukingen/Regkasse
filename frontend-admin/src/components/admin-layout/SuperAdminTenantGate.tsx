'use client';

import type { ReactNode } from 'react';
import { usePathname } from 'next/navigation';

import { SelectTenantFirstPlaceholder } from '@/components/admin-layout/SelectTenantFirstPlaceholder';
import { TenantInfoCard } from '@/components/admin-layout/TenantInfoCard';
import {
    isPathAllowedWithoutTenant,
    useSuperAdminTenantMode,
} from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { isVerwaltungAdminPath, normalizeAdminPathname } from '@/shared/adminSidebarNavigation';

type SuperAdminTenantGateProps = {
    children: ReactNode;
};

/**
 * Blocks mandant-scoped pages until Super Admin selects a tenant (impersonation / dev slug).
 * Platform routes under `/admin/tenants`, `/admin/license`, `/admin/system` stay available.
 */
export function SuperAdminTenantGate({ children }: SuperAdminTenantGateProps) {
    const pathname = usePathname();
    const { requiresTenantSelection } = useSuperAdminTenantMode();

    if (!requiresTenantSelection) {
        return <>{children}</>;
    }

    if (isPathAllowedWithoutTenant(pathname)) {
        return <>{children}</>;
    }

    const p = normalizeAdminPathname(pathname);
    const isVerwaltung = isVerwaltungAdminPath(pathname);
    const showInfoCard = isVerwaltung && p !== '/admin' && !p.startsWith('/admin/');

    return (
        <>
            {showInfoCard ? <TenantInfoCard /> : null}
            <SelectTenantFirstPlaceholder />
        </>
    );
}
