'use client';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { hasPermission, PERMISSIONS } from '@/shared/auth/permissions';

export function useBillingAccess(): boolean {
    const { user, isAuthInitializing } = useAuth();
    if (isAuthInitializing) {
        return false;
    }
    return isSuperAdmin(user?.role) || hasPermission(user, PERMISSIONS.SYSTEM_CRITICAL);
}
