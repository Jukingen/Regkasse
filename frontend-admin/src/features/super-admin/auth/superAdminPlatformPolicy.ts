'use client';

import { useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Platform-level Super Admin capabilities (tenant provisioning, one-time passwords).
 * Credential handoff is never available to tenant Manager roles.
 */
export function getSuperAdminPlatformPolicy(
    role: string | undefined | null,
    permissions?: string[],
) {
    const superAdmin = isSuperAdmin(role);
    const usePerms = Boolean(permissions?.length);
    const canAccessTenantAdmin =
        superAdmin ||
        (usePerms && hasPermission({ permissions }, PERMISSIONS.SYSTEM_CRITICAL));

    return {
        isSuperAdmin: superAdmin,
        /** Create tenant users, show generated password, reset tenant-user password. */
        canProvisionTenantCredentials: superAdmin,
        canAccessTenantAdminRoutes: canAccessTenantAdmin,
        canShowPlatformAdminMenu: superAdmin,
    };
}

export function useSuperAdminPlatformPolicy() {
    const { user } = useAuth();
    const permissions = (user as { permissions?: string[] } | undefined)?.permissions;

    return useMemo(
        () => getSuperAdminPlatformPolicy(user?.role ?? null, permissions),
        [user?.role, permissions],
    );
}
