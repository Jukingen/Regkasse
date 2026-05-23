'use client';

import { useMemo } from 'react';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { TenantUsersTabCore } from '@/features/users/components/TenantUsersTabCore';
import { getUsersPolicy } from '@/shared/auth/usersPolicy';

export type TenantDetailUsersTabProps = {
    tenantId: string;
    tenant?: AdminTenantListItem | null;
};

/** Super-admin tenant detail users tab — fixed tenant via shared core. */
export function TenantDetailUsersTab({ tenantId, tenant }: TenantDetailUsersTabProps) {
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const policy = useMemo(
        () => ({
            ...getUsersPolicy('SuperAdmin'),
            canProvisionTenantCredentials,
        }),
        [canProvisionTenantCredentials],
    );

    return (
        <TenantUsersTabCore
            tenantId={tenantId}
            tenant={tenant}
            policy={policy}
            roleDisplayLabel={(role) => role}
        />
    );
}
