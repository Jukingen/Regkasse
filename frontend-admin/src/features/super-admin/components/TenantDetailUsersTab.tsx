'use client';

import { useCallback, useMemo } from 'react';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { useSuperAdminPlatformPolicy } from '@/features/super-admin/auth/superAdminPlatformPolicy';
import { TenantUsersTabCore } from '@/features/users/components/TenantUsersTabCore';
import { formatRoleDisplayLabel } from '@/features/users/utils/roleDisplayLabel';
import { useI18n } from '@/i18n';
import { getUsersPolicy } from '@/shared/auth/usersPolicy';

export type TenantDetailUsersTabProps = {
    tenantId: string;
    tenant?: AdminTenantListItem | null;
};

/** Super-admin tenant detail users tab — fixed tenant via shared core. */
export function TenantDetailUsersTab({ tenantId, tenant }: TenantDetailUsersTabProps) {
    const { t } = useI18n();
    const { canProvisionTenantCredentials } = useSuperAdminPlatformPolicy();
    const roleDisplayLabel = useCallback(
        (role: string) => formatRoleDisplayLabel(t, role),
        [t],
    );
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
            roleDisplayLabel={roleDisplayLabel}
        />
    );
}
