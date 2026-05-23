'use client';

import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

/**
 * Tenant soft/restore/hard delete requires SuperAdmin on the API.
 * FA gate: SuperAdmin role or system.critical permission (same as /admin/tenants route).
 */
export function useCanManageTenantDeletion(): boolean {
    const { user, hasPermission } = usePermissions();
    return useMemo(
        () => isSuperAdmin(user?.role) || hasPermission(PERMISSIONS.SYSTEM_CRITICAL),
        [user?.role, hasPermission],
    );
}
