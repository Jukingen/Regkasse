'use client';

import { useMemo } from 'react';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { canAccessPath } from '@/shared/auth/canAccessPath';
import { usePermissions } from '@/hooks/usePermissions';

export function useCanAccessPath(path: string): boolean {
    const { user } = useAuth();
    const { userPermissions } = usePermissions();
    return useMemo(() => {
        if (isSuperAdmin(user?.role)) return true;
        return canAccessPath(path, userPermissions);
    }, [path, userPermissions, user?.role]);
}
